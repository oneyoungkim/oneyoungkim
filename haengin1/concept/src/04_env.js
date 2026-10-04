// ---------- environment: 한양도성 성곽길 한 토막 ----------
// B/C: 기존 장면 그대로. A/I(S.ink 있음): REFERENCES.md 2장 '한옥 골목 일러스트' 룩 —
// 종이색 평면 하늘, 북쪽 산(북악·북한산) 판 + 도시 스카이라인 판, 성곽 너머로 이어지는 한옥 골목.

// 배경 팔레트 기본값. 메인이 STYLES.X.ink.env = { roof: '#..' } 식으로 일부만 덮어쓸 수 있다.
const INK_ENV = {
  color: {
    sky: '#e9e0cc', mtnBack: '#aba59a', mtn: '#8f8a80', mtnInk: '#4e4a43', rock: '#c4beb2',
    city: '#cdc6b8', cityFar: '#d9d2c3', cityInk: '#8f897e',
    roof: '#3a3a3a', soffit: '#4f3a33', ridgeWhite: '#e6e0d2', mak: '#f6f2e8', rafter: '#8a4436',
    wood: '#8e4b3d', plaster: '#f3efe4', hanji: '#efe4cc', stone: '#a49e94', door: '#8a3f31',
    ground: '#d9c9a8', speck: '#3d362d', path: '#e0d1b2', joint: '#5d5244',
    fort: '#c4bcab', leaf: '#ddb03f', trunk: '#4a3a30', lamp: '#2a2b30', sign: '#5b4030', box: '#d4552c', boxText: '#ffffff', cat: '#e3a04a',
    wire: '#2a2a2e', pole: '#77736b', shop: '#2f6aa8', shopText: '#ffffff', tunnel: '#2b2826', dancheong: '#5d7b70',
  },
  mono: {
    sky: null, mtnBack: '#e4e2dc', mtn: '#cfccc5', mtnInk: '#111111', rock: '#f2f1ec',
    city: '#e3e0da', cityFar: '#ebe9e4', cityInk: '#6a6762',
    roof: '#2a2a2a', soffit: '#3a3632', ridgeWhite: '#f2f0ea', mak: '#f7f5f0', rafter: '#3d3935',
    wood: '#3d3935', plaster: '#f7f5f0', hanji: '#efede7', stone: '#cdc9c1', door: '#34302c',
    ground: '#ebe7df', speck: '#111111', path: '#f1eee8', joint: '#2a2622',
    fort: '#e2ded6', leaf: '#ece8df', trunk: '#2a2622', lamp: '#1a1a1a', sign: '#2a2622', box: '#e4e0d8', boxText: '#111111', cat: '#ece6dc',
    wire: '#111111', pole: '#c9c5bd', shop: '#2a2622', shopText: '#f7f5f0', tunnel: '#111111', dancheong: '#3d3935',
  },
};
function inkEnvPalette(S) {
  const mono = S.ink.mode === 'mono';
  const P = Object.assign({}, INK_ENV[mono ? 'mono' : 'color'], S.ink.env || {});
  if (!P.sky) P.sky = S.ink.paper;
  return P;
}

function buildEnv(S, kit, scene) {
  const env = new THREE.Group(); scene.add(env);
  const ink = S.ink ? inkEnvPalette(S) : null;
  const E = ink ? Object.assign({}, S.env, { stone: ink.fort, stoneVar: .05, leaf: ink.leaf, trunk: ink.trunk, lamp: ink.lamp, sign: ink.sign, box: ink.box, cat: ink.cat, ground: ink.ground }) : S.env;
  const rnd = mulberry(42);
  const part = (p, g, m, o) => kit.part(p, g, m, o);
  const owned = []; // textures/materials made here (ink branch) to dispose with the env

  if (ink) {
    // 종이색 평면 하늘 (그라데이션 없음)
    const skyM = new THREE.MeshBasicMaterial({ color: new THREE.Color(ink.sky), side: THREE.BackSide, depthWrite: false, fog: false, toneMapped: false });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(320, 24, 12), skyM); sky.renderOrder = -10; env.add(sky); owned.push(skyM);
    scene.fog = new THREE.Fog(new THREE.Color(ink.sky), 30, 220);
  } else {
    // sky dome
    const sky = new THREE.Mesh(new THREE.SphereGeometry(120, 32, 16), new THREE.ShaderMaterial({
      side: THREE.BackSide, depthWrite: false, fog: false, toneMapped: false,
      uniforms: { top: { value: new THREE.Color(S.sky[0]) }, mid: { value: new THREE.Color(S.sky[1]) }, bot: { value: new THREE.Color(S.sky[2]) } },
      vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
      fragmentShader: 'uniform vec3 top; uniform vec3 mid; uniform vec3 bot; varying vec3 vP; void main(){ float h = vP.y; vec3 c = h > 0.0 ? mix(mid, top, smoothstep(0.0, 0.5, h)) : mix(mid, bot, smoothstep(0.0, 0.2, -h)); gl_FragColor = vec4(c, 1.0);\n#include <colorspace_fragment>\n}',
    }));
    env.add(sky);
    scene.fog = new THREE.Fog(new THREE.Color(S.fog[0]), S.fog[1], S.fog[2]);
  }

  // ground + paving
  if (ink) {
    const gt = inkGroundTexture(S, ink, false); gt.repeat.set(40, 18); owned.push(gt);
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(160, 72), kit.solid('#ffffff', { map: gt, rough: .95, role: 'face' }));
    ground.rotation.x = -Math.PI / 2; ground.position.set(0, 0, 33.9); ground.receiveShadow = true; env.add(ground);
    const pt = inkGroundTexture(S, ink, true); pt.repeat.set(6.5, 1); owned.push(pt);
    const path = new THREE.Mesh(new THREE.PlaneGeometry(26, 3.4), kit.solid('#ffffff', { map: pt, rough: .9, role: 'face' }));
    path.rotation.x = -Math.PI / 2; path.position.set(0, .004, .05); path.receiveShadow = true; env.add(path);
  } else {
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(90, 44), kit.solid(E.ground, { rough: .95 }));
    ground.rotation.x = -Math.PI / 2; ground.position.set(0, 0, 19.7); ground.receiveShadow = true; env.add(ground);
    const pave = pavingTexture(S); pave.repeat.set(9, 1.4);
    const path = new THREE.Mesh(new THREE.PlaneGeometry(26, 3.4), kit.solid('#ffffff', { map: pave, rough: .9 }));
    path.rotation.x = -Math.PI / 2; path.position.set(0, .004, .05); path.receiveShadow = true; env.add(path);
  }

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
  // A/I: 성곽은 캐릭터 바로 뒤 큰 면이라 평면('face': 종이 + 그늘에만 해칭). 굵은 먹 줄눈은 외곽선이 만든다.
  const stoneMat = ink ? kit.solid(E.stone, { rough: .95, role: 'face' }) : kit.solid(E.stone, { rough: .95 });
  const box = new THREE.BoxGeometry(1, 1, 1);
  const inst = new THREE.InstancedMesh(box, stoneMat, stones.length);
  const m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), col = new THREE.Color();
  stones.forEach((s, i) => {
    m4.compose(new THREE.Vector3(s[0], s[1], s[2]), q, new THREE.Vector3(s[3], s[4], s[5])); inst.setMatrixAt(i, m4);
    if (ink) col.setScalar(1 - E.stoneVar + rnd() * E.stoneVar * 2);
    else col.copy(styleColor(S, E.stone)).multiplyScalar(1 - E.stoneVar + rnd() * E.stoneVar * 2);
    inst.setColorAt(i, col);
  });
  inst.castShadow = true; inst.receiveShadow = true; env.add(inst);
  const om = kit.outline(stoneMat);
  if (om) {
    const o = new THREE.InstancedMesh(box, om, stones.length), e = S.outline.w * (ink ? 3.2 : 1.6);
    stones.forEach((s, i) => { m4.compose(new THREE.Vector3(s[0], s[1], s[2]), q, new THREE.Vector3(s[3] + e, s[4] + e, s[5] + e)); o.setMatrixAt(i, m4); });
    env.add(o);
  }

  if (ink) {
    // 성곽 너머: 한옥 골목 + 도시 판 + 북쪽 산 판
    buildInkAlley(env, S, kit, ink, owned);
    buildInkBackdrop(env, S, ink, owned);
  } else {
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
  }

  // ginkgo tree (10월 은행나무)
  const tree = new THREE.Group(); tree.position.set(-3.7, 0, -1.0); env.add(tree);
  part(tree, tCapsule(.12, .18, 2.4), kit.solid(E.trunk, { rough: 1 }), { pos: [0, 2.4, 0] });
  const leafM = kit.solid(E.leaf, { rough: .9 });
  [[0, 3.1, 0, 1.1], [.7, 2.7, .2, .8], [-.7, 2.8, -.1, .85], [.2, 3.7, -.2, .8], [-.3, 2.5, .5, .7], [.5, 3.3, .5, .65]]
    .forEach(([x, y, z, r]) => part(tree, new THREE.IcosahedronGeometry(r, 1), leafM, { pos: [x, y, z], ow: ink ? 2.4 : 1.2 }));
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
  const st = labelTexture([['한양도성 순성길', 64, .38], ['혜화문 ← · → 와룡공원', 40, .72]], css(S, E.sign), ink && S.ink.mode === 'mono' ? S.ink.paper : '#fff8ec');
  part(sign, new THREE.BoxGeometry(.9, .36, .05), kit.solid('#ffffff', { map: st, rough: .9 }), { pos: [0, 1.15, .03] });

  // delivery bike with the 국밥 box
  const bike = new THREE.Group(); bike.position.set(-2.25, 0, -.95); bike.rotation.set(0, .35, .1); env.add(bike);
  const bm = kit.solid('#3b3f4d', { rough: .5 });
  for (const x of [-.48, .48]) part(bike, new THREE.TorusGeometry(.3, .035, 8, 24), kit.solid('#22252e', { rough: .7 }), { pos: [x, .31, 0] });
  part(bike, new THREE.CylinderGeometry(.025, .025, 1.0, 8), bm, { pos: [0, .5, 0], rot: [0, 0, Math.PI / 2 - .3] });
  part(bike, new THREE.CylinderGeometry(.025, .025, .55, 8), bm, { pos: [.42, .62, 0], rot: [0, 0, -.25] });
  part(bike, new THREE.BoxGeometry(.06, .04, .5), bm, { pos: [.48, .92, 0] });
  part(bike, new THREE.BoxGeometry(.24, .05, .12), kit.solid('#1b1d24'), { pos: [-.15, .8, 0] });
  const bt = labelTexture([['성대후문', 72, .36], ['국밥', 96, .7]], css(S, E.box), ink ? ink.boxText : '#ffffff', 256, 256);
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
  return { group: env, cat: { head: ch, tail }, dispose() { env.traverse(o => { if (o.geometry) o.geometry.dispose(); }); owned.forEach(x => x.dispose()); env.removeFromParent(); scene.fog = null; } };
}
let _fxTex = null; const fxTex = () => (_fxTex || (_fxTex = fxTextures()));

// ---------- ink style: ground textures (모래 베이지 + 드문 먹 점 / 박석 길) ----------
function inkGroundTexture(S, P, paved) {
  const W = paved ? 1024 : 512, H = paved ? 512 : 512, c = mkCanvas(W, H), g = c.getContext('2d'), r = mulberry(paved ? 11 : 5);
  if (paved) {
    // 큰 박석: 연한 모래색 판석 + 굵지 않은 먹 줄눈 (한 장 = 4m x 3.4m)
    g.fillStyle = css(S, P.joint); g.fillRect(0, 0, W, H);
    const rows = 4, rh = H / rows;
    for (let j = 0; j < rows; j++) {
      let x = -r() * 60;
      while (x < W) {
        const w = 120 + r() * 110, k = .97 + r() * .05, inset = 1.7;
        const base = styleColor(S, P.path).multiplyScalar(k);
        g.fillStyle = '#' + base.getHexString();
        g.beginPath();
        const x0 = x + inset, x1 = x + w - inset, y0 = j * rh + inset, y1 = (j + 1) * rh - inset, jt = () => (r() - .5) * 4;
        g.moveTo(x0 + jt(), y0 + jt()); g.lineTo(x1 + jt(), y0 + jt()); g.lineTo(x1 + jt(), y1 + jt()); g.lineTo(x0 + jt(), y1 + jt()); g.closePath(); g.fill();
        x += w;
      }
    }
  } else {
    g.fillStyle = css(S, P.ground); g.fillRect(0, 0, W, H);
  }
  // 드문드문 먹 점 (크기 섞어서)
  g.fillStyle = css(S, P.speck);
  const n = paved ? 70 : 26;
  for (let i = 0; i < n; i++) {
    const x = r() * W, y = r() * H, s = .8 + r() * 1.6;
    g.globalAlpha = .55 + r() * .45; g.beginPath(); g.ellipse(x, y, s * (1 + r()), s, r() * Math.PI, 0, TAU); g.fill();
    if (r() < .25) { g.beginPath(); g.ellipse(x + 5 + r() * 6, y + (r() - .5) * 6, s * .6, s * .5, 0, 0, TAU); g.fill(); }
  }
  g.globalAlpha = 1;
  const t = canvasTex(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.anisotropy = 8; return t;
}

// ---------- ink style: hanok alley beyond the wall ----------
// 성곽 뒤(북쪽, -Z)로 한 점 투시 골목. 양옆 한옥(맞배지붕, 처마 막새 흰 점 띠, 서까래 끝, 주칠 기둥·창살, 아랫벽 돌 블록, 흰 회벽).
// 모든 반복 요소는 InstancedMesh. 무대(성곽 앞)는 건드리지 않는다.
function inkRoofGeo(a, b, H, thick, lift, grow) {
  // 맞배지붕: 용마루는 z축, 처마 쪽은 오목하게 처지고 양 끝이 살짝 들린다. 원점 = 처마 높이 중앙.
  const NU = 18, NV = 7, P = 1.55;
  const pos = [], uv = [], idx = [], grp = [];
  const top = (u, v) => { const av = Math.abs(v); return [v * (b + grow), H * Math.pow(1 - av, P) + lift * Math.pow(Math.abs(u), 4) * Math.pow(av, 1.5) + grow, u * (a + grow)]; };
  const addGrid = (fn, flip) => {
    const s = pos.length / 3;
    for (let i = 0; i <= NU; i++) for (let j = 0; j <= NV; j++) { const p = fn(i / NU * 2 - 1, j / NV); pos.push(...p); uv.push(p[2], Math.abs(p[0])); }
    for (let i = 0; i < NU; i++) for (let j = 0; j < NV; j++) {
      const A = s + i * (NV + 1) + j, B = A + 1, C = A + NV + 1, D = C + 1;
      if (flip) idx.push(A, C, B, B, C, D); else idx.push(A, B, C, B, D, C);
    }
  };
  const start0 = 0;
  for (const side of [1, -1]) {
    // top surface (side: +x or -x), bottom (soffit) surface offset down by thickness
    addGrid((u, t) => top(u, side * t), side > 0);
    addGrid((u, t) => { const p = top(u, side * t); p[1] -= thick + grow * 2; return p; }, side < 0);
  }
  const topCount = idx.length; // groups: we flag soffit triangles separately below
  // edges: eave fascia (v=±1) and gable rakes (u=±1)
  const edge = (pts, flip) => {
    const s = pos.length / 3;
    pts.forEach(p => { pos.push(p[0], p[1], p[2]); uv.push(p[2], p[1]); pos.push(p[0], p[1] - thick - grow * 2, p[2]); uv.push(p[2], p[1] - thick); });
    for (let k = 0; k < pts.length - 1; k++) { const A = s + k * 2, B = A + 1, C = A + 2, D = A + 3; if (flip) idx.push(A, C, B, B, C, D); else idx.push(A, B, C, B, D, C); }
  };
  for (const side of [1, -1]) {
    const eave = [], rake = [];
    for (let i = 0; i <= NU; i++) eave.push(top(i / NU * 2 - 1, side));
    edge(eave, side > 0);
    for (let j = -NV; j <= NV; j++) rake.push(top(side, j / NV));
    edge(rake, side < 0);
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  // reorder indices into groups: 0 = top + edges (기와), 1 = soffit (처마 밑)
  const quadsPerGrid = NU * NV * 6, gridOrder = [0, 1, 0, 1]; // top,+soffit for side +1, then side -1
  const g0 = [], g1 = [];
  for (let k = 0; k < 4; k++) (gridOrder[k] ? g1 : g0).push(...idx.slice(start0 + k * quadsPerGrid, start0 + (k + 1) * quadsPerGrid));
  g0.push(...idx.slice(topCount));
  geo.setIndex(g0.concat(g1));
  geo.addGroup(0, g0.length, 0); geo.addGroup(g0.length, g1.length, 1);
  geo.computeVertexNormals();
  return geo;
}
function inkRoofTexture(S, P) {
  // 기와골: 짙은 먹회색 바탕에 경사 방향 골 선 (1m 반복, 골 간격 25cm)
  const c = mkCanvas(256, 256), g = c.getContext('2d');
  g.fillStyle = css(S, P.roof); g.fillRect(0, 0, 256, 256);
  const dark = styleColor(S, P.roof).multiplyScalar(.55), lite = styleColor(S, P.roof).lerp(new THREE.Color(1, 1, 1), .16);
  for (let k = 0; k < 4; k++) {
    const x = k * 64;
    g.fillStyle = '#' + dark.getHexString(); g.fillRect(x, 0, 9, 256);
    g.fillStyle = '#' + lite.getHexString(); g.fillRect(x + 22, 0, 6, 256);
  }
  const t = canvasTex(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.anisotropy = 8; return t;
}
function inkLatticeTexture(S, P, door) {
  // 창살(띠살) 창 / 판문: 한지 바탕 + 주칠 살
  const W = 256, H = door ? 384 : 192, c = mkCanvas(W, H), g = c.getContext('2d');
  const wood = css(S, P.wood), paper = css(S, P.hanji);
  g.fillStyle = wood; g.fillRect(0, 0, W, H);
  if (door) {
    // 두 짝 판문 + 문고리
    for (const x of [10, W / 2 + 4]) { g.fillStyle = css(S, P.door); g.fillRect(x, 10, W / 2 - 14, H - 20); }
    g.fillStyle = paper; for (const x of [W / 2 - 22, W / 2 + 14]) { g.beginPath(); g.arc(x + 4, H * .52, 7, 0, TAU); g.fill(); }
    g.strokeStyle = wood; g.lineWidth = 5; for (const x of [10, W / 2 + 4]) for (const yy of [.3, .7]) { g.beginPath(); g.moveTo(x, H * yy); g.lineTo(x + W / 2 - 14, H * yy); g.stroke(); }
  } else {
    const fr = 14; g.fillStyle = paper; g.fillRect(fr, fr, W - fr * 2, H - fr * 2);
    g.fillStyle = wood;
    g.fillRect(W / 2 - 4, 0, 8, H);
    for (let x = fr + 16; x < W - fr; x += 19) if (Math.abs(x - W / 2) > 10) g.fillRect(x - 1.5, fr, 3, H - fr * 2);
    for (const yy of [.3, .5, .7]) g.fillRect(fr, H * yy - 2.5, W - fr * 2, 5);
  }
  return canvasTex(c);
}
function inkShopTexture(S, P, chars, aspect) {
  // 돌출 간판 (세로 글씨). 실존 상호 금지: 가상 이름만.
  const W = 128, H = Math.round(W * aspect), c = mkCanvas(W, H), g = c.getContext('2d');
  g.fillStyle = css(S, P.shop); g.fillRect(0, 0, W, H);
  g.strokeStyle = css(S, P.shopText); g.lineWidth = 5; g.strokeRect(9, 9, W - 18, H - 18);
  g.fillStyle = css(S, P.shopText); g.textAlign = 'center'; g.textBaseline = 'middle';
  g.font = '62px "Black Han Sans", "Gothic A1", sans-serif';
  const step = (H - 40) / chars.length; [...chars].forEach((ch, i) => g.fillText(ch, W / 2, 20 + step * (i + .5)));
  return canvasTex(c);
}

function buildInkAlley(env, S, kit, P, owned) {
  const rnd = mulberry(1907), mono = S.ink.mode === 'mono';
  // 배경 재질 역할: 평면 채색 + 아주 옅은 한 단계 그림자('face'), mono에선 돌 = 스크린톤('mid'), 목재 = 먹('dark')
  const R = { flat: 'face', stone: mono ? 'mid' : 'face', wood: mono ? 'dark' : 'face', roof: 'dark' };
  const BOX = new THREE.BoxGeometry(1, 1, 1);
  const m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), v3 = new THREE.Vector3(), sc = new THREE.Vector3(), col = new THREE.Color();
  const yAxis = new THREE.Vector3(0, 1, 0);
  const CAM = new THREE.Vector3(.45, 1.95, 4.4); // 기본 카메라 근처: 거리별 외곽선 굵기 계산용
  const slopeK = .03, wallBack = -2.15, dip = -.6;
  const gy = z => (z < wallBack ? dip + (wallBack - z) * slopeK : 0); // 성곽 바깥은 한 단 낮고, 북쪽(산 쪽)으로 완만한 오르막

  // 골목 바닥 (성곽 뒤)
  const gt = inkGroundTexture(S, P, false); gt.repeat.set(36, 18); owned.push(gt);
  const gg = new THREE.PlaneGeometry(150, 75, 1, 1); gg.rotateX(-Math.PI / 2); gg.translate(0, 0, wallBack - 37.5);
  { const p = gg.attributes.position; for (let i = 0; i < p.count; i++) p.setY(i, gy(p.getZ(i)) - .02); p.needsUpdate = true; gg.computeVertexNormals(); }
  const back = new THREE.Mesh(gg, kit.solid('#ffffff', { map: gt, rough: 1, role: 'face' })); back.receiveShadow = true; env.add(back);
  // 성곽 뒷면 축대 (바깥 땅이 낮아서 생기는 단차)
  kit.part(env, new THREE.BoxGeometry(26, -dip + .1, .3), kit.solid(P.stone, { rough: 1, role: R.stone }), { pos: [0, dip / 2 - .05, wallBack + .1], outline: false, shadow: false });

  // ---- collectors ----
  const B = {}; // key -> [[x,y,z,w,h,d,ry,shade,rz]]
  const box = (key, x, y, z, w, h, d, ry = 0, shade = 1, rz = 0) => (B[key] || (B[key] = [])).push([x, y, z, w, h, d, ry, shade, rz]);
  const roofs = [], gables = [], dots = [], rafters = [], wins = [], doors = []; // [x, y, z, yaw]

  // 한 채의 로컬 틀: lx = 골목 쪽(+), lz = 줄 방향, 원점 = 정면 선 가운데. yaw th: 로컬 x → 월드 (cos th, -sin th), 로컬 z → (sin th, cos th)
  const frame = (ox, oz, th) => { const c = Math.cos(th), s = Math.sin(th); return { th, P: (lx, lz) => [ox + c * lx + s * lz, oz - s * lx + c * lz] }; };
  const D = 4.6, L = 7.0, eaveY = 2.78, ridgeH = 1.75, ovB = .88, ovA = .5, thick = .22, lift = .17;
  const RA = L / 2 + ovA, RB = D / 2 + ovB;
  const lowY = (F, pts) => Math.min(...pts.map(([lx, lz]) => gy(F.P(lx, lz)[1]))); // 집 바닥 = 발치에서 가장 낮은 땅
  // 처마 막새 점 + 서까래 끝 (양쪽 처마)
  function eaveDots(F, cl, y, a, b, lf, th, len, far) {
    for (const side of far ? [1] : [1, -1]) { // far: 뒷줄은 골목 쪽 처마만, 서까래 생략
      const n = Math.round(a * 2 / .3);
      for (let i = 0; i <= n; i++) { const u = (i / n * 2 - 1) * .985, [x, z] = F.P(cl + side * (b + .008), u * a); dots.push([x, y + lf * Math.pow(Math.abs(u), 4) - th * .52, z, F.th]); }
      const m = far ? -1 : Math.round(len / .3);
      for (let i = 0; i <= m; i++) { const u = (i / m * 2 - 1) * (len / 2 + .2) / a, [x, z] = F.P(cl + side * (b - .32), u * a); rafters.push([x, y + lf * Math.pow(Math.abs(u), 4) - th - .045, z, F.th]); }
    }
  }
  function house(F, o = {}) {
    const end = o.end || 1; // 카메라 쪽 마구리: 로컬 +lz(1) / -lz(-1)
    const y0 = o.y0 ?? lowY(F, [[0, L / 2], [0, -L / 2], [-D, L / 2], [-D, -L / 2]]);
    const fb = (key, lx, ly, lz, w, h, d, shade) => { const [x, z] = F.P(lx, lz); box(key, x, ly, z, w, h, d, F.th, shade); };
    const at = (list, lx, ly, lz, yaw) => { const [x, z] = F.P(lx, lz); list.push([x, ly, z, yaw]); };
    // 몸채 회벽 + 기단
    fb('plaster', -D / 2, y0 + eaveY / 2, 0, D, eaveY, L);
    fb('stone', .05 - D / 2, y0 + .09, 0, D + .5, .18, L + .4, .92);
    // 기둥 4개 + 창방(위) + 중인방
    const bays = 3, bw = L / bays;
    for (let k = 0; k <= bays; k++) fb('wood', .07, y0 + eaveY / 2, -L / 2 + k * bw, .22, eaveY, .22);
    fb('wood', .06, y0 + eaveY - .14, 0, .2, .28, L + .1);
    fb('wood', .05, y0 + 1.16, 0, .16, .13, L);
    // 칸마다: 아랫벽 돌 블록(굵은 줄눈은 외곽선이 만든다) + 창살 창 / 대문
    const doorBay = o.door ?? -1;
    for (let k = 0; k < bays; k++) {
      const z0 = -L / 2 + k * bw + .11, z1 = z0 + bw - .22;
      if (k === doorBay) { at(doors, .03, y0 + 1.12, (z0 + z1) / 2, F.th); continue; }
      let yy = y0 + .18;
      for (const rh of [.46, .48]) {
        let z = z0;
        while (z < z1 - .05) { const w = Math.min(.42 + rnd() * .42, z1 - z); fb('stone', .05, yy + rh / 2, z + w / 2, .1, rh - .045, w - .045, .9 + rnd() * .16); z += w; }
        yy += rh;
      }
      at(wins, .035, y0 + 1.86, (z0 + z1) / 2, F.th);
    }
    // 마구리(카메라 쪽 끝면) 목재 틀 + 창
    { const zf = end * (L / 2 + .06);
      for (const t of [-D + .07, -D / 2]) fb('wood', t, y0 + eaveY / 2, zf, .22, eaveY, .2);
      fb('wood', -D / 2, y0 + eaveY - .12, zf, D + .1, .24, .18);
      fb('wood', -D / 2, y0 + 1.16, zf, D - .1, .13, .16);
      at(wins, -D * .27, y0 + 1.86, zf + end * .03, F.th - end * Math.PI / 2); } // 골목 쪽 칸에 창 → 기본 시점에서 보인다
    // 마구리 아랫벽 돌 (뒷줄은 카메라 쪽만)
    for (const e of o.far ? [end] : [1, -1]) {
      const zf = e * (L / 2 + .04); let yy = y0 + .18;
      for (const rh of [.46, .48]) { let t = -D + .1; while (t < -.12) { const w = Math.min(.45 + rnd() * .4, -.12 - t); fb('stone', t + w / 2, yy + rh / 2, zf, w - .045, rh - .045, .1, .9 + rnd() * .16); t += w; } yy += rh; }
    }
    // 지붕 + 용마루(먹 + 흰 회 띠) + 박공 회벽
    at(roofs, -D / 2, y0 + eaveY, 0, F.th);
    fb('roof', -D / 2, y0 + eaveY + ridgeH + .1, 0, .34, .32, RA * 2 - .3);
    fb('ridgeW', -D / 2, y0 + eaveY + ridgeH - .08, 0, .4, .1, RA * 2 - .4);
    for (const e of [1, -1]) at(gables, -D / 2, y0 + eaveY, e * (L / 2 - .02), F.th);
    eaveDots(F, -D / 2, y0 + eaveY, RA, RB, lift, thick, L, o.far);
  }
  // 집 사이 담장: 돌 아랫벽 + 회벽 + 기와 갓 (로컬 lz a~b)
  function fence(F, za, zb) {
    const len = zb - za; if (len < .3) return;
    const y0 = lowY(F, [[0, za], [0, zb]]), zc = (za + zb) / 2;
    const fb = (key, lx, ly, lz, w, h, d, shade) => { const [x, z] = F.P(lx, lz); box(key, x, ly, z, w, h, d, F.th, shade); };
    let yy = y0; for (const rh of [.42, .42]) { let z = za; while (z < zb - .05) { const w = Math.min(.36 + rnd() * .4, zb - z); fb('stone', -.2, yy + rh / 2, z + w / 2, .34, rh - .045, w - .045, .9 + rnd() * .16); z += w; } yy += rh; }
    fb('plaster', -.2, y0 + 1.3, zc, .3, .62, len);
    fb('roof', -.2, y0 + 1.68, zc, .62, .16, len + .1);
    fb('roof', -.2, y0 + 1.8, zc, .2, .12, len + .1);
  }
  // 한 줄: start에서 dir 방향으로 n채, 집 가운데 좌표 목록을 돌려준다
  function row(n, start, dir, gapMin, gapMax, mk) {
    let t = start; const cs = [];
    for (let i = 0; i < n; i++) { cs.push(t + dir * L / 2); t += dir * (L + gapMin + rnd() * (gapMax - gapMin)); }
    cs.forEach((c, i) => mk(c, i));
    return cs;
  }

  // 성곽 너머 골목: 왼쪽 줄(골목이 +x, yaw 0), 오른쪽 줄(골목이 -x, yaw π). 소실점은 화면 가운데 근처.
  const laneL = -2.25, laneR = 2.85;
  const leftZ = row(5, -3.6, -1, .8, 1.6, (zc, i) => house(frame(laneL, zc, 0), { door: i % 2 ? 1 : -1, end: 1 }));
  const rightZ = row(5, -4.1, -1, .8, 1.6, (zc, i) => house(frame(laneR, zc, Math.PI), { door: i % 2 ? -1 : 2, end: -1 }));
  for (let i = 0; i < leftZ.length - 1; i++) fence(frame(laneL, 0, 0), leftZ[i + 1] + L / 2, leftZ[i] - L / 2);
  for (let i = 0; i < rightZ.length - 1; i++) fence(frame(laneR, 0, Math.PI), -(rightZ[i] - L / 2), -(rightZ[i + 1] + L / 2));
  // 뒷줄 (지붕 바다): 엇갈려 배치
  row(4, -6.5, -1, 1.4, 2.6, zc => house(frame(laneL - D - 1.6, zc, 0), { far: true }));
  row(4, -5.8, -1, 1.4, 2.6, zc => house(frame(laneR + D + 1.6, zc, Math.PI), { end: -1, far: true }));
  row(4, -6.4, -1, 2.2, 3.5, zc => house(frame(laneL - 2 * D - 3.4, zc, 0), { far: true }));
  row(4, -4.8, -1, 2.2, 3.5, zc => house(frame(laneR + 2 * D + 3.4, zc, Math.PI), { end: -1, far: true }));
  // 성곽 안쪽(카메라 쪽) 양 끝: 순성길을 따라 늘어선 한옥 — 시점을 옆으로 돌리면 성곽과 한옥 사이 골목이 된다. 무대(|x|<9)는 비움.
  const inZ = 2.7, th90 = Math.PI / 2;
  row(4, -9.6, -1, .9, 1.8, (xc, i) => house(frame(xc, inZ, th90), { end: 1, door: i % 2 ? 1 : -1 }));
  row(4, 9.6, 1, .9, 1.8, (xc, i) => house(frame(xc, inZ, th90), { end: -1, door: i % 2 ? -1 : 1 }));
  row(3, -12, -1, 2, 3, xc => house(frame(xc, inZ + D + 2.4, th90), { end: 1, far: true }));
  row(3, 12, 1, 2, 3, xc => house(frame(xc, inZ + D + 2.4, th90), { end: -1, far: true }));

  // ---- 성곽 연장 (무대 밖 양 끝) + 혜화문 ----
  // 기존 성곽(x -13~13)은 그대로 두고, 서쪽은 혜화문 너머로, 동쪽은 와룡공원 쪽으로 더 이어 그린다.
  const fortW = [];
  const fortRun = (xa, xb) => {
    let y = 0, rw = 0;
    while (y < 1.0) { const h = Math.min(.3 + rnd() * .1, 1.05 - y); let x = xa - (rw % 2) * .35; while (x < xb) { const w = .5 + rnd() * .45; fortW.push([x + w / 2, y + h / 2, -1.75, w - .03, h - .03, .7]); x += w; } y += h; rw++; }
    for (let x = xa; x < xb; x += 1.34) { fortW.push([x + .6, 1.32, -1.7, 1.22, .52, .52]); fortW.push([x + .6, 1.62, -1.7, 1.3, .08, .62]); }
  };
  fortRun(13.2, 40); fortRun(-42, -24.6);
  // 혜화문: 홍예(아치) 석축 + 문루. 실존 랜드마크라 실명 사용.
  const gx = -19, gz = -1.6, gw = 10.8, gd = 4.0, gh = 4.2, ar = 1.75, ay = 2.1; // 석축 폭·깊이·높이, 아치 반지름·아치 기둥 높이
  const gate = [];
  for (const fz of [gz + gd / 2, gz - gd / 2]) { // 석축 앞·뒷면 돌 (아치 구멍 제외)
    let y = 0, rw = 0;
    while (y < gh - .05) {
      const h = Math.min(.42 + rnd() * .14, gh - y); let x = gx - gw / 2 - (rw % 2) * .3;
      while (x < gx + gw / 2) {
        const x0 = Math.max(x, gx - gw / 2), w = Math.min(.7 + rnd() * .55, gx + gw / 2 - x0), cx = x0 + w / 2, cy = y + h / 2, dx = Math.abs(cx - gx);
        const inArch = dx < ar + .5 && (cy < ay || Math.hypot(dx, cy - ay) < ar + .5);
        if (!inArch && w > .15) gate.push([cx, cy, fz, w - .04, h - .04, .3]);
        x = x0 + w;
      }
      y += h; rw++;
    }
    // 홍예 쐐기돌(반원) + 아치 기둥 돌
    const n = 13;
    for (let i = 0; i < n; i++) { const a = Math.PI * (i + .5) / n, r = ar + .24; box('gateStone', gx + Math.cos(a) * r, ay + Math.sin(a) * r, fz, .5, .4, .32, 0, .95 + rnd() * .1, a - Math.PI / 2); }
    for (const sx of [-1, 1]) for (let k = 0; k < 4; k++) box('gateStone', gx + sx * (ar + .24), .27 + k * .52, fz, .5, .48, .32, 0, .95 + rnd() * .1);
  }
  for (const sx of [-1, 1]) { // 석축 옆면 돌
    const fx = gx + sx * gw / 2; let y = 0, rw = 0;
    while (y < gh - .05) { const h = Math.min(.42 + rnd() * .14, gh - y); let z = gz - gd / 2 + .15 + (rw % 2) * .25; while (z < gz + gd / 2 - .15) { const w = Math.min(.6 + rnd() * .5, gz + gd / 2 - .15 - z); if (w > .15) gate.push([fx, y + h / 2, z + w / 2, .3, h - .04, w - .04]); z += w; } y += h; rw++; }
  }
  // 석축 속(통로 양옆·위) + 통로 안 먹 그늘
  box('gateCore', gx - (gw / 2 + ar) / 2 - .05, gh / 2, gz, gw / 2 - ar - .2, gh, gd - .3);
  box('gateCore', gx + (gw / 2 + ar) / 2 + .05, gh / 2, gz, gw / 2 - ar - .2, gh, gd - .3);
  box('gateCore', gx, ay + ar + (gh - ay - ar) / 2 + .05, gz, ar * 2 + .2, gh - ay - ar, gd - .3);
  box('tunnel', gx, (ay + ar) / 2, gz, ar * 2, ay + ar, gd - .7);
  // 석축 위 여장(성가퀴)
  for (let x = gx - gw / 2 + .3; x < gx + gw / 2 - .3; x += 1.2) for (const fz of [gz + gd / 2 - .2, gz - gd / 2 + .2]) box('gateStone', x + .45, gh + .3, fz, .9, .6, .4, 0, .95 + rnd() * .1);
  // 문루: 기둥 4x2, 창방(단청), 난간, 지붕
  const pw = 6.4, pd = 2.9, ph = 2.5, py = gh + .1;
  box('gateFloor', gx, py - .05, gz, pw + .6, .14, pd + .6);
  for (const sx of [-1.5, -.5, .5, 1.5]) for (const sz of [-1, 1]) box('wood', gx + sx * pw / 3, py + ph / 2, gz + sz * pd / 2, .26, ph, .26);
  for (const sz of [-1, 1]) { box('dancheong', gx, py + ph - .16, gz + sz * pd / 2, pw + .3, .32, .24); box('wood', gx, py + .55, gz + sz * pd / 2, pw, .07, .1); }
  for (const sx of [-1, 1]) { box('dancheong', gx + sx * pw / 2, py + ph - .16, gz, .24, .32, pd + .3); box('wood', gx + sx * pw / 2, py + .55, gz, .1, .07, pd); }
  const GR = { a: pw / 2 + 1.25, b: pd / 2 + 1.2, H: 2.1, th: .26, lift: .55 };
  const gateRoof = { pos: [gx, py + ph, gz], F: frame(gx, gz, th90) };
  eaveDots(gateRoof.F, 0, py + ph, GR.a, GR.b, GR.lift, GR.th, pw);
  box('roof', gx, py + ph + GR.H + .12, gz, GR.a * 2 - .9, .4, .4);
  box('ridgeW', gx, py + ph + GR.H - .1, gz, GR.a * 2 - 1, .12, .46);
  for (const sx of [-1, 1]) box('roof', gx + sx * (GR.a - .55), py + ph + GR.H + .3, gz, .35, .55, .42, 0, 1, sx * .35); // 용마루 끝 치켜올림
  const hyeonpan = labelTexture([['惠化門', 150, .54]], css(S, mono ? P.door : '#2b2a33'), mono ? S.ink.paper : '#e9d9a8', 512, 200, 'Noto Serif KR');
  owned.push(hyeonpan);
  kit.part(env, new THREE.BoxGeometry(1.5, .58, .06), kit.solid('#ffffff', { map: hyeonpan, rough: .8, role: R.flat }), { pos: [gx, py + ph - .62, gz + pd / 2 + .18], ow: 2.5 });

  // ---- 전봇대 + 전깃줄 (골목 위를 가로지르는 먹 선) ----
  const poles = [[laneR - .45, -9.3], [laneL + .4, -19.6], [laneR - .45, -30.5]];
  for (const [x, z] of poles) {
    const y0 = gy(z);
    box('pole', x, y0 + 3.0, z, .2, 6.0, .2);
    box('pole', x, y0 + 5.6, z, 1.3, .1, .1);
  }
  const wireMat = kit.solid(P.wire, { rough: 1, role: 'dark' });
  const wire = (a, b, sag, r) => {
    const pts = []; for (let i = 0; i <= 14; i++) { const t = i / 14; const p = new THREE.Vector3().lerpVectors(a, b, t); p.y -= Math.sin(Math.PI * t) * sag; pts.push(p); }
    const m = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3(pts), 18, r, 4), wireMat); m.castShadow = false; env.add(m);
  };
  for (let i = 0; i < poles.length - 1; i++) {
    const [xa, za] = poles[i], [xb, zb] = poles[i + 1];
    for (const dx of [-.55, .55]) wire(new THREE.Vector3(xa + dx, gy(za) + 5.58, za), new THREE.Vector3(xb + dx, gy(zb) + 5.58, zb), .45, .022);
    wire(new THREE.Vector3(xa, gy(za) + 5.1, za), new THREE.Vector3(xb, gy(zb) + 5.1, zb), .6, .03);
  }
  // 집으로 들어가는 인입선
  wire(new THREE.Vector3(poles[0][0], gy(poles[0][1]) + 5.1, poles[0][1]), new THREE.Vector3(laneL - .2, gy(-6) + eaveY + .3, -6), .5, .018);
  wire(new THREE.Vector3(poles[1][0], gy(poles[1][1]) + 5.1, poles[1][1]), new THREE.Vector3(laneR + .2, gy(-15) + eaveY + .3, -15), .5, .018);

  // ---- 돌출 간판: 전통 한옥 + 스트릿 (가상 상호) ----
  const signs = [[laneR, -1, rightZ[0] + 1.4, '하루편의점'], [laneL, 1, leftZ[1] + 2.2, '반촌공방']];
  for (const [xf, s, z, name] of signs) {
    const h = name.length * .22 + .16, y0 = gy(z);
    const t = inkShopTexture(S, P, name, h / .5); owned.push(t);
    const g = new THREE.Group(); g.position.set(xf + s * .55, y0 + 1.95, z); env.add(g);
    kit.part(g, new THREE.BoxGeometry(.5, h, .08), kit.solid('#ffffff', { map: t, rough: .8, role: R.flat }), { ow: 3 });
    kit.part(g, new THREE.BoxGeometry(.5, .04, .04), kit.solid(P.wire, { role: 'dark' }), { pos: [-s * .5, h * .35, 0], outline: false });
    kit.part(g, new THREE.BoxGeometry(.5, .04, .04), kit.solid(P.wire, { role: 'dark' }), { pos: [-s * .5, -h * .35, 0], outline: false });
    // 간판은 골목과 직각 → 정면이 카메라 쪽(+z)을 본다
  }

  // ---- build instanced meshes ----
  const SPEC = {
    plaster: { hex: P.plaster, ow: 1, role: R.flat }, stone: { hex: P.stone, ow: 1.25, role: R.stone }, wood: { hex: P.wood, ow: 1, role: R.wood },
    roof: { hex: P.roof, ow: 1, role: R.roof }, ridgeW: { hex: P.ridgeWhite, ow: 0, role: 'glow' }, pole: { hex: P.pole, ow: 1, role: R.flat },
    gateStone: { hex: P.fort, ow: 1.4, role: R.flat }, gateCore: { hex: P.fort, ow: 0, role: R.flat }, tunnel: { hex: P.tunnel, ow: 0, role: 'glow' },
    gateFloor: { hex: P.stone, ow: 1, role: R.stone }, dancheong: { hex: P.dancheong, ow: 1, role: R.wood }, fortX: { hex: P.fort, ow: 1.6, role: R.flat },
  };
  const eul = new THREE.Euler();
  const setQ = b => q.setFromEuler(eul.set(0, b[6], b[8] || 0, 'YXZ'));
  // 성곽 연장 돌 + 혜화문 석축 돌
  for (const s of fortW) box('fortX', s[0], s[1], s[2], s[3], s[4], s[5], 0, .95 + rnd() * .1);
  for (const s of gate) box('gateStone', s[0], s[1], s[2], s[3], s[4], s[5], 0, .95 + rnd() * .1);
  for (const key in B) {
    const list = B[key], sp = SPEC[key], mat = kit.solid(sp.hex, { rough: .95, role: sp.role });
    const im = new THREE.InstancedMesh(BOX, mat, list.length);
    list.forEach((b, i) => {
      setQ(b); m4.compose(v3.set(b[0], b[1], b[2]), q, sc.set(b[3], b[4], b[5])); im.setMatrixAt(i, m4);
      im.setColorAt(i, col.setScalar(b[7]));
    });
    im.castShadow = true; im.receiveShadow = true; env.add(im);
    const om = sp.ow ? kit.outline(mat) : null;
    if (om) {
      const o = new THREE.InstancedMesh(BOX, om, list.length);
      list.forEach((b, i) => {
        const e = (.016 + .0019 * v3.set(b[0], b[1], b[2]).distanceTo(CAM)) * sp.ow;
        setQ(b); m4.compose(v3.set(b[0], b[1], b[2]), q, sc.set(b[3] + e, b[4] + e, b[5] + e)); o.setMatrixAt(i, m4);
      });
      env.add(o);
    }
  }
  // 지붕 (기와 + 처마 밑)
  const rt = inkRoofTexture(S, P); owned.push(rt);
  const roofMat = kit.solid('#ffffff', { map: rt, rough: .9, role: R.roof });
  const soffitMat = kit.solid(P.soffit, { rough: .9, role: R.roof });
  const rg = inkRoofGeo(RA, RB, ridgeH, thick, lift, 0);
  const roofIM = new THREE.InstancedMesh(rg, [roofMat, soffitMat], roofs.length);
  const rOut = kit.outline(soffitMat);
  const rgo = rOut ? inkRoofGeo(RA, RB, ridgeH, thick, lift, .045) : null;
  const roofO = rOut ? new THREE.InstancedMesh(rgo, rOut, roofs.length) : null;
  const yawM = r => m4.compose(v3.set(r[0], r[1], r[2]), q.setFromAxisAngle(yAxis, r[3] || 0), sc.set(1, 1, 1));
  roofs.forEach((r, i) => { yawM(r); roofIM.setMatrixAt(i, m4); if (roofO) roofO.setMatrixAt(i, m4); });
  roofIM.castShadow = true; roofIM.receiveShadow = true; env.add(roofIM); if (roofO) env.add(roofO);
  // 혜화문 문루 지붕 (처마 끝이 더 들린 큰 지붕)
  { const g = inkRoofGeo(GR.a, GR.b, GR.H, GR.th, GR.lift, 0), m = new THREE.Mesh(g, [roofMat, soffitMat]);
    yawM([...gateRoof.pos, th90]); m.applyMatrix4(m4); m.castShadow = true; m.receiveShadow = true; env.add(m);
    if (rOut) { const h = new THREE.Mesh(inkRoofGeo(GR.a, GR.b, GR.H, GR.th, GR.lift, .06), rOut); h.applyMatrix4(m4); env.add(h); } }
  // 박공 회벽 삼각
  const gs = new THREE.Shape(); gs.moveTo(-D / 2 - .1, 0); gs.lineTo(D / 2 + .1, 0); gs.lineTo(0, ridgeH * .92); gs.closePath();
  const gGeo = new THREE.ExtrudeGeometry(gs, { depth: .1, bevelEnabled: false }); gGeo.translate(0, 0, -.05);
  const gIM = new THREE.InstancedMesh(gGeo, kit.solid(P.plaster, { rough: .95, role: R.flat }), gables.length);
  gables.forEach((g, i) => { yawM(g); gIM.setMatrixAt(i, m4); });
  gIM.receiveShadow = true; env.add(gIM);
  // 막새 흰 점 (처마 끝 띠)
  const dGeo = new THREE.CylinderGeometry(.052, .052, .03, 7); dGeo.rotateZ(Math.PI / 2);
  const dIM = new THREE.InstancedMesh(dGeo, kit.solid(P.mak, { rough: .8, role: 'glow' }), dots.length);
  dots.forEach((d, i) => { yawM(d); dIM.setMatrixAt(i, m4); });
  env.add(dIM);
  // 서까래 끝 (붉은 갈색)
  const rfGeo = new THREE.CylinderGeometry(.06, .06, .5, 5); rfGeo.rotateZ(Math.PI / 2);
  const rfIM = new THREE.InstancedMesh(rfGeo, kit.solid(P.rafter, { rough: .9, role: R.wood }), rafters.length);
  rafters.forEach((d, i) => { yawM(d); rfIM.setMatrixAt(i, m4); });
  env.add(rfIM);
  // 창살 창 / 대문 (골목 쪽을 보는 판)
  const panel = (list, tex, w, h) => {
    if (!list.length) return; owned.push(tex);
    const pg = new THREE.PlaneGeometry(w, h); pg.rotateY(Math.PI / 2);
    const im = new THREE.InstancedMesh(pg, kit.solid('#ffffff', { map: tex, rough: .9, role: R.flat }), list.length);
    list.forEach((p, i) => { yawM(p); im.setMatrixAt(i, m4); });
    im.receiveShadow = true; env.add(im);
  };
  panel(wins, inkLatticeTexture(S, P, false), 1.62, .98);
  panel(doors, inkLatticeTexture(S, P, true), 1.45, 2.1);
}

// ---------- ink style: far backdrop (도시 스카이라인 판 + 북쪽 산 판) ----------
// 원통 띠 두 장에 캔버스로 그린 그림을 붙인다. mono에선 회색 단계를 화면 공간 스크린톤 점으로 바꾼다.
const _inkBackdropCache = new Map();
const INK_RING = {
  span: 3.7, // 원통 각도 (rad), 북쪽(-Z) 중심. 궤도 회전 ±1.2 + 시야 반폭까지 덮음
  city: { R: 150, y0: -7, y1: 13, W: 4096, H: 256 },
  mtn: { R: 260, y0: -36, y1: 62, W: 4096, H: 640 },
};
function inkBackdropMat(tex, S, P) {
  const mono = S.ink.mode === 'mono', dpr = Math.min(window.devicePixelRatio || 1, 2);
  return new THREE.ShaderMaterial({
    side: THREE.BackSide, fog: false, toneMapped: false, transparent: false,
    uniforms: { map: { value: tex }, ink: { value: new THREE.Color(S.ink.ink) }, paper: { value: new THREE.Color(P.sky) }, cell: { value: (S.ink.tone && S.ink.tone.size || 4.5) * dpr }, mono: { value: mono ? 1 : 0 } },
    vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
    fragmentShader: [
      'uniform sampler2D map; uniform vec3 ink; uniform vec3 paper; uniform float cell; uniform float mono; varying vec2 vUv;',
      'void main(){',
      '  vec4 t = texture2D(map, vUv); if (t.a < .5) discard;',
      '  vec3 c = t.rgb;',
      '  if (mono > .5) {',
      '    float l = dot(c, vec3(.2126, .7152, .0722));',
      '    float tone = clamp((0.95 - l) * 0.8, 0.0, 0.9);', // 덮는 비율 (0 = 종이)
      '    vec2 p = mat2(.7071, -.7071, .7071, .7071) * gl_FragCoord.xy / cell;',
      '    float d = length(fract(p) - .5);',
      '    float on = l < .045 ? 1.0 : step(d, sqrt(tone / 3.14159)) * step(.03, tone);',
      '    c = mix(paper, ink, on);',
      '  }',
      '  gl_FragColor = vec4(c, 1.0);',
      '#include <colorspace_fragment>',
      '}'].join('\n'),
  });
}
function inkBackdropTextures(S, P) {
  const key = S.key + JSON.stringify(P);
  if (_inkBackdropCache.has(key)) return _inkBackdropCache.get(key);
  const span = INK_RING.span, EYE = 1.95;
  // 화면 기준 좌→우 각도 phi(-span/2..span/2, +가 화면 오른쪽 = +x) 와 높이각 el(rad) -> 캔버스 좌표
  const mk = (ring, draw) => {
    const { W, H, R, y0, y1 } = ring, c = mkCanvas(W, H), g = c.getContext('2d');
    const X = phi => (phi / span + .5) * W;
    const Y = el => (1 - ((EYE + R * Math.tan(el)) - y0) / (y1 - y0)) * H;
    g.lineCap = 'round'; g.lineJoin = 'round';
    draw(g, X, Y, W, H);
    // 원통 안쪽에서 보면 좌우가 뒤집히므로 한 번 뒤집어 둔다
    const f = mkCanvas(W, H), fg = f.getContext('2d'); fg.translate(W, 0); fg.scale(-1, 1); fg.drawImage(c, 0, 0);
    const t = canvasTex(f); t.anisotropy = 8; return t;
  };
  const deg = Math.PI / 180;
  const r = mulberry(77);
  // --- 산: 앞 = 북악(둥근 봉우리, 회갈), 뒤 = 북한산(바위 봉우리, 더 옅게) ---
  const bump = (x, c, w, h, p = 2) => h * Math.exp(-Math.pow(Math.abs(x - c) / w, p));
  const noise = (() => { const a = [...Array(9)].map(() => [r() * 40 + 6, r() * TAU, r()]); return x => a.reduce((s, [f, ph, m]) => s + Math.sin(x * f + ph) * m, 0); })();
  // 기본 시점 화면 위쪽 끝이 높이각 약 5도라서, 능선이 화면 안에 들어오도록 낮게(정면 3~4.5도) 잡는다. 옆으로 돌리면 더 높은 봉우리가 보인다.
  const backH = x => Math.max(.9, 2.0 + .62 * (bump(x, .22, .2, 4.6, 1.6) + bump(x, .36, .08, 3.4, 1.3) + bump(x, .05, .14, 2.2) + bump(x, -.7, .45, 2.4) + bump(x, 1.05, .35, 2.6)) + noise(x * 1.3) * .22 + (Math.abs(Math.sin(x * 61)) * .3));
  const frontH = x => Math.max(.4, 1.0 + .72 * (bump(x, -.28, .26, 3.9, 2.2) + bump(x, -.62, .16, 1.6) + bump(x, .55, .3, 1.5) + bump(x, -1.25, .3, 1.9)) + noise(x) * .1);
  const mtn = mk(INK_RING.mtn, (g, X, Y, W, H) => {
    const ridge = (fn, fill, line, lw, hatch, rocks) => {
      const pts = []; for (let px = 0; px <= W; px += 4) { const phi = (px / W - .5) * span; pts.push([px, Y(fn(phi) * deg)]); }
      g.fillStyle = fill; g.beginPath(); g.moveTo(0, H); pts.forEach(p => g.lineTo(p[0], p[1])); g.lineTo(W, H); g.closePath(); g.fill();
      // 능선 결 해칭: 그늘 쪽 비탈(오른쪽으로 내려가는 면)에 경사 따라 짧은 붓선
      g.strokeStyle = line; g.lineWidth = hatch.w;
      for (let i = 2; i < pts.length - 2; i++) {
        const [x, y] = pts[i], dy = pts[i + 1][1] - pts[i - 1][1];
        if (dy > .6 * hatch.k || (r() < .12 && Math.abs(dy) < 1.5)) {
          if (r() > hatch.p) continue;
          const len = (14 + r() * 26) * hatch.len, ang = Math.atan2(Math.max(dy, 2), 8) * .8 + .35;
          g.globalAlpha = .55 + r() * .4; g.beginPath(); g.moveTo(x, y + 3); g.lineTo(x + Math.cos(ang) * len * .35, y + 3 + Math.sin(ang) * len); g.stroke();
        }
      }
      // 골짜기 선 (봉우리에서 내려오는 지능선)
      g.globalAlpha = .7; g.lineWidth = hatch.w * 1.1;
      for (let i = 6; i < pts.length - 6; i++) {
        const [x, y] = pts[i]; if (!(y < pts[i - 5][1] && y < pts[i + 5][1]) || r() > .55) continue;
        let cx = x, cy = y + 4; g.beginPath(); g.moveTo(cx, cy);
        const dir = r() < .5 ? -1 : 1, n = 3 + (r() * 4 | 0);
        for (let k = 0; k < n; k++) { cx += dir * (6 + r() * 12); cy += 10 + r() * 16; g.lineTo(cx, cy); }
        g.stroke();
      }
      g.globalAlpha = 1;
      if (rocks) {
        // 화강암 봉우리: 옅은 바위 면 + 세로 결
        for (let i = 0; i < pts.length; i++) {
          const [x, y] = pts[i], phi = (x / W - .5) * span; if (fn(phi) < rocks.min || r() > .5) continue;
          g.fillStyle = rocks.fill; g.beginPath(); g.moveTo(x - 6, y + 2); g.lineTo(x + 7, y + 2); g.lineTo(x + 4, y + 22 + r() * 30); g.lineTo(x - 4, y + 18 + r() * 20); g.closePath(); g.fill();
          g.strokeStyle = line; g.lineWidth = 1.2; g.globalAlpha = .6; g.beginPath(); g.moveTo(x + 2, y + 4); g.lineTo(x + 3, y + 18 + r() * 24); g.stroke(); g.globalAlpha = 1;
        }
      }
      // 먹 외곽선 (능선 윗선)
      g.strokeStyle = line; g.lineWidth = lw; g.beginPath(); pts.forEach((p, i) => (i ? g.lineTo(p[0], p[1]) : g.moveTo(p[0], p[1]))); g.stroke();
    };
    ridge(backH, css(S, P.mtnBack), css(S, P.mtnInk), 1.6, { w: 1.1, k: 1, p: .5, len: .8 }, { min: 5.2, fill: css(S, P.rock) });
    ridge(frontH, css(S, P.mtn), css(S, P.mtnInk), 2.2, { w: 1.3, k: 1, p: .75, len: 1 }, null);
  });
  // --- 도시: 옅은 회색 빌딩 실루엣 (두 겹), 가는 선과 창 띠만 ---
  const city = mk(INK_RING.city, (g, X, Y, W, H) => {
    const layer = (fill, line, hMin, hMax, wMin, wMax, winP) => {
      let x = 0;
      while (x < W) {
        const w = wMin + r() * (wMax - wMin), phi = (x / W - .5) * span;
        const gap = Math.abs(phi) < .05 ? .3 : 1; // 소실점 바로 뒤는 조금 낮게
        const el = (hMin + r() * (hMax - hMin)) * gap * deg, y = Y(el);
        g.fillStyle = fill; g.fillRect(x, y, w, H - y);
        g.strokeStyle = line; g.lineWidth = 1.4; g.strokeRect(x + .7, y + .7, w - 1.4, H);
        if (r() < winP) { g.lineWidth = 1; g.globalAlpha = .55; for (let yy = y + 6; yy < Y(0) - 2; yy += 6) { g.beginPath(); g.moveTo(x + 4, yy); g.lineTo(x + w - 4, yy); g.stroke(); } g.globalAlpha = 1; }
        if (r() < .15) { g.lineWidth = 1.2; g.beginPath(); g.moveTo(x + w * .6, y); g.lineTo(x + w * .6, y - 8 - r() * 8); g.stroke(); }
        x += w + (r() < .3 ? 2 + r() * 10 : 0);
      }
    };
    layer(css(S, P.cityFar), css(S, P.cityInk), .5, 1.6, 14, 46, .3);
    layer(css(S, P.city), css(S, P.cityInk), .2, 1.1, 18, 60, .5);
    g.fillStyle = css(S, P.city); g.fillRect(0, Y(-.4 * deg), W, H);
  });
  const res = { mtn, city };
  _inkBackdropCache.set(key, res);
  return res;
}
function buildInkBackdrop(env, S, P, owned) {
  const T = inkBackdropTextures(S, P), span = INK_RING.span;
  for (const [k, tex, order] of [['mtn', T.mtn, -9], ['city', T.city, -8]]) {
    const ring = INK_RING[k], h = ring.y1 - ring.y0;
    const geo = new THREE.CylinderGeometry(ring.R, ring.R, h, 96, 1, true, Math.PI - span / 2, span);
    const mat = inkBackdropMat(tex, S, P); owned.push(mat);
    const m = new THREE.Mesh(geo, mat); m.position.y = ring.y0 + h / 2; m.renderOrder = order; env.add(m);
  }
}
