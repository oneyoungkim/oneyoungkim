// ---------- environment: 한양도성 성곽길 한 토막 ----------
function buildEnv(S, kit, scene) {
  const env = new THREE.Group(); scene.add(env);
  const E = S.env, rnd = mulberry(42);
  const part = (p, g, m, o) => kit.part(p, g, m, o);

  // sky dome
  const sky = new THREE.Mesh(new THREE.SphereGeometry(120, 32, 16), new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false, fog: false, toneMapped: false,
    uniforms: { top: { value: new THREE.Color(S.sky[0]) }, mid: { value: new THREE.Color(S.sky[1]) }, bot: { value: new THREE.Color(S.sky[2]) } },
    vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
    fragmentShader: 'uniform vec3 top; uniform vec3 mid; uniform vec3 bot; varying vec3 vP; void main(){ float h = vP.y; vec3 c = h > 0.0 ? mix(mid, top, smoothstep(0.0, 0.5, h)) : mix(mid, bot, smoothstep(0.0, 0.2, -h)); gl_FragColor = vec4(c, 1.0);\n#include <colorspace_fragment>\n}',
  }));
  env.add(sky);
  scene.fog = new THREE.Fog(new THREE.Color(S.fog[0]), S.fog[1], S.fog[2]);

  // ground + paving
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(90, 44), kit.solid(E.ground, { rough: .95 }));
  ground.rotation.x = -Math.PI / 2; ground.position.set(0, 0, 19.7); ground.receiveShadow = true; env.add(ground);
  const pave = pavingTexture(S); pave.repeat.set(9, 1.4);
  const path = new THREE.Mesh(new THREE.PlaneGeometry(26, 3.4), kit.solid('#ffffff', { map: pave, rough: .9 }));
  path.rotation.x = -Math.PI / 2; path.position.set(0, .004, .05); path.receiveShadow = true; env.add(path);

  // fortress wall (body courses + 여장 parapet + coping)
  const wallZ = -1.75, x0 = -13, x1 = 13, bodyH = 1.05, depthW = .7;
  const stones = [];
  let y = 0, row = 0;
  while (y < bodyH - .05) {
    const h = Math.min(.3 + rnd() * .1, bodyH - y); let x = x0 - (row % 2) * .35;
    while (x < x1) { const w = .5 + rnd() * .45; stones.push([x + w / 2, y + h / 2, wallZ, w - .03, h - .03, depthW]); x += w; }
    y += h; row++;
  }
  for (let x = x0; x < x1; x += 1.34) {
    stones.push([x + .6, bodyH + .27, wallZ + .05, 1.22, .52, .52]);
    stones.push([x + .6, bodyH + .57, wallZ + .05, 1.3, .08, .62]);
  }
  const stoneMat = kit.solid(E.stone, { rough: .95 });
  const box = new THREE.BoxGeometry(1, 1, 1);
  const inst = new THREE.InstancedMesh(box, stoneMat, stones.length);
  const m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), col = new THREE.Color();
  stones.forEach((s, i) => {
    m4.compose(new THREE.Vector3(s[0], s[1], s[2]), q, new THREE.Vector3(s[3], s[4], s[5])); inst.setMatrixAt(i, m4);
    col.copy(styleColor(S, E.stone)).multiplyScalar(1 - E.stoneVar + rnd() * E.stoneVar * 2); inst.setColorAt(i, col);
  });
  inst.castShadow = true; inst.receiveShadow = true; env.add(inst);
  const om = kit.outline(stoneMat);
  if (om) {
    const o = new THREE.InstancedMesh(box, om, stones.length), e = S.outline.w * 1.6;
    stones.forEach((s, i) => { m4.compose(new THREE.Vector3(s[0], s[1], s[2]), q, new THREE.Vector3(s[3] + e, s[4] + e, s[5] + e)); o.setMatrixAt(i, m4); });
    env.add(o);
  }
  // land drops away behind the wall: a darker slope strip
  const slope = new THREE.Mesh(new THREE.PlaneGeometry(90, 8), kit.solid(E.ground, { rough: 1 }));
  slope.position.set(0, -2.6, wallZ - 4.4); slope.rotation.x = -Math.PI / 2 + .62; env.add(slope);

  // distant city (대학로 쪽) + N Seoul Tower silhouette
  const cityMat = kit.solid(E.city, { rough: 1 });
  const bN = 130, bg = new THREE.InstancedMesh(box, cityMat, bN);
  const wins = [];
  for (let i = 0; i < bN; i++) {
    const x = (rnd() * 2 - 1) * 110, z = -55 - rnd() * 70, w = 4 + rnd() * 8, h = 6 + rnd() * 18, d = 4 + rnd() * 8, yb = -12;
    m4.compose(new THREE.Vector3(x, yb + h / 2, z), q, new THREE.Vector3(w, h, d)); bg.setMatrixAt(i, m4);
    if (E.win) for (let k = 0; k < 4; k++) if (rnd() < .7) wins.push([x + (rnd() - .5) * w * .7, yb + 2 + rnd() * (h - 3), z + d / 2 + .05]);
  }
  env.add(bg);
  if (E.win && wins.length) {
    const wm = new THREE.MeshBasicMaterial({ color: new THREE.Color(E.win), fog: true });
    const wi = new THREE.InstancedMesh(new THREE.PlaneGeometry(.9, .7), wm, wins.length);
    wins.forEach((w, i) => { m4.compose(new THREE.Vector3(...w), q, new THREE.Vector3(1, 1, 1)); wi.setMatrixAt(i, m4); });
    env.add(wi);
  }
  const tower = new THREE.Group(); tower.position.set(-52, -12, -125); tower.scale.setScalar(1.3); env.add(tower);
  const tm = kit.solid(E.city, { rough: 1 });
  tower.add(new THREE.Mesh(new THREE.SphereGeometry(16, 20, 10, 0, TAU, 0, Math.PI / 2), tm));
  const tw = new THREE.Mesh(new THREE.CylinderGeometry(.7, 1.1, 14, 10), tm); tw.position.y = 22; tower.add(tw);
  const pod = new THREE.Mesh(new THREE.CylinderGeometry(2.1, 1.6, 2.4, 14), tm); pod.position.y = 29; tower.add(pod);
  const ant = new THREE.Mesh(new THREE.CylinderGeometry(.18, .3, 9, 6), tm); ant.position.y = 34.5; tower.add(ant);

  // ginkgo tree (10월 은행나무)
  const tree = new THREE.Group(); tree.position.set(-3.7, 0, -1.0); env.add(tree);
  part(tree, tCapsule(.12, .18, 2.4), kit.solid(E.trunk, { rough: 1 }), { pos: [0, 2.4, 0] });
  const leafM = kit.solid(E.leaf, { rough: .9 });
  [[0, 3.1, 0, 1.1], [.7, 2.7, .2, .8], [-.7, 2.8, -.1, .85], [.2, 3.7, -.2, .8], [-.3, 2.5, .5, .7], [.5, 3.3, .5, .65]]
    .forEach(([x, y, z, r]) => part(tree, new THREE.IcosahedronGeometry(r, 1), leafM, { pos: [x, y, z], ow: 1.2 }));
  const leafGeo = new THREE.CircleGeometry(.06, 6, Math.PI * .25, Math.PI * .5); leafGeo.rotateX(-Math.PI / 2);
  const leaves = new THREE.InstancedMesh(leafGeo, kit.solid(E.leaf, { rough: .9 }), 160);
  for (let i = 0; i < 160; i++) {
    const a = rnd() * TAU, r = Math.pow(rnd(), .6) * 3.2;
    const px = i < 110 ? -3.7 + Math.cos(a) * r : (rnd() * 2 - 1) * 6, pz = i < 110 ? -.8 + Math.sin(a) * r * .5 : (rnd() * 2 - 1) * 1.6;
    m4.compose(new THREE.Vector3(px, .01 + rnd() * .004, pz), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), rnd() * TAU), new THREE.Vector3(1, 1, 1));
    leaves.setMatrixAt(i, m4);
  }
  leaves.receiveShadow = true; env.add(leaves);

  // street lamp
  const lamp = new THREE.Group(); lamp.position.set(3.3, 0, -1.15); env.add(lamp);
  const lm = kit.solid(E.lamp, { rough: .5 });
  part(lamp, new THREE.CylinderGeometry(.045, .06, 3.3, 10), lm, { pos: [0, 1.65, 0] });
  part(lamp, new THREE.BoxGeometry(.5, .06, .06), lm, { pos: [-.2, 3.25, 0] });
  const head = part(lamp, new THREE.CylinderGeometry(.12, .2, .16, 12), lm, { pos: [-.42, 3.17, 0] });
  if (E.glow) {
    const bulb = new THREE.Mesh(new THREE.SphereGeometry(.08, 10, 8), new THREE.MeshBasicMaterial({ color: '#fff1c4' })); bulb.position.y = -.08; head.add(bulb);
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: fxTex().flash, color: '#ffd9a0', transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, opacity: .55 }));
    sp.scale.set(1.4, 1.4, 1); sp.position.y = -.1; head.add(sp);
  }
  // trail sign: 한양도성 순성길
  const sign = new THREE.Group(); sign.position.set(2.5, 0, -1.2); env.add(sign);
  const wood = kit.solid(E.sign, { rough: .9 });
  part(sign, new THREE.BoxGeometry(.08, 1.2, .08), wood, { pos: [0, .6, 0] });
  const st = labelTexture([['한양도성 순성길', 64, .38], ['혜화문 ← · → 와룡공원', 40, .72]], css(S, E.sign), '#fff8ec');
  part(sign, new THREE.BoxGeometry(.9, .36, .05), kit.solid('#ffffff', { map: st, rough: .9 }), { pos: [0, 1.15, .03] });

  // delivery bike with the 국밥 box
  const bike = new THREE.Group(); bike.position.set(-2.25, 0, -.95); bike.rotation.set(0, .35, .1); env.add(bike);
  const bm = kit.solid('#3b3f4d', { rough: .5 });
  for (const x of [-.48, .48]) part(bike, new THREE.TorusGeometry(.3, .035, 8, 24), kit.solid('#22252e', { rough: .7 }), { pos: [x, .31, 0] });
  part(bike, new THREE.CylinderGeometry(.025, .025, 1.0, 8), bm, { pos: [0, .5, 0], rot: [0, 0, Math.PI / 2 - .3] });
  part(bike, new THREE.CylinderGeometry(.025, .025, .55, 8), bm, { pos: [.42, .62, 0], rot: [0, 0, -.25] });
  part(bike, new THREE.BoxGeometry(.06, .04, .5), bm, { pos: [.48, .92, 0] });
  part(bike, new THREE.BoxGeometry(.24, .05, .12), kit.solid('#1b1d24'), { pos: [-.15, .8, 0] });
  const bt = labelTexture([['성대후문', 72, .36], ['국밥', 96, .7]], css(S, E.box), '#ffffff', 256, 256);
  const bmat = kit.solid('#ffffff', { map: bt, rough: .7 });
  part(bike, new THREE.BoxGeometry(.4, .34, .34), bmat, { pos: [-.5, .88, 0] });

  // 치즈냥이 on the parapet
  const cat = new THREE.Group(); cat.position.set(1.4, bodyH + .63, wallZ + .05); env.add(cat);
  const cm = kit.solid(E.cat, { rough: .9 });
  part(cat, new THREE.SphereGeometry(.12, 14, 10), cm, { pos: [0, .1, 0], scl: [1.35, .9, .9] });
  const ch = new THREE.Group(); ch.position.set(.15, .22, .02); cat.add(ch);
  part(ch, new THREE.SphereGeometry(.085, 14, 10), cm);
  for (const s of [-1, 1]) part(ch, new THREE.ConeGeometry(.03, .06, 6), cm, { pos: [.0, .08, s * .045], rot: [s * .25, 0, -.2], ow: .6 });
  const tail = new THREE.Group(); tail.position.set(-.16, .1, 0); cat.add(tail);
  part(tail, tCapsule(.025, .02, .22), cm, { rot: [0, 0, 2.4] });
  return { group: env, cat: { head: ch, tail }, dispose() { env.traverse(o => { if (o.geometry) o.geometry.dispose(); }); env.removeFromParent(); scene.fog = null; } };
}
let _fxTex = null; const fxTex = () => (_fxTex || (_fxTex = fxTextures()));
