// ---------- geometry helpers ----------
const V2 = (x, y) => new THREE.Vector2(Math.max(x, 1e-4), y);
// tapered capsule from y=0 (top cap centre) down to y=-L (bottom cap centre)
function tCapsule(rTop, rBot, L, seg = 16, capN = 6) {
  const pts = [];
  for (let i = 0; i <= capN; i++) { const a = -Math.PI / 2 + (i / capN) * (Math.PI / 2); pts.push(V2(rBot * Math.cos(a), -L + rBot * Math.sin(a))); }
  for (let i = 0; i <= capN; i++) { const a = (i / capN) * (Math.PI / 2); pts.push(V2(rTop * Math.cos(a), rTop * Math.sin(a))); }
  return new THREE.LatheGeometry(pts, seg);
}
// torso piece: ctrl = [[yFrac, r], ...] sampled evenly so canvas v maps linearly to height
function torsoGeo(ctrl, y0, y1, n = 22) {
  const pts = [];
  const rAt = f => { for (let i = 0; i < ctrl.length - 1; i++) { const [fa, ra] = ctrl[i], [fb, rb] = ctrl[i + 1]; if (f <= fb) { const t = (f - fa) / (fb - fa); return lerp(ra, rb, t * t * (3 - 2 * t)); } } return ctrl[ctrl.length - 1][1]; };
  for (let i = 0; i <= n; i++) { const f = i / n; let r = rAt(f); if (i === 0 || i === n) r = r * .35; pts.push(V2(r, lerp(y0, y1, f))); }
  return new THREE.LatheGeometry(pts, 28, Math.PI, TAU);
}
function extrude(geo, w) {
  const g = geo.clone(), p = g.attributes.position, n = g.attributes.normal;
  for (let i = 0; i < p.count; i++) p.setXYZ(i, p.getX(i) + n.getX(i) * w, p.getY(i) + n.getY(i) * w, p.getZ(i) + n.getZ(i) * w);
  p.needsUpdate = true; return g;
}

// ---------- materials ----------
function materialKit(S) {
  let ramp = null;
  if (S.shading === 'toon') { ramp = new THREE.DataTexture(new Uint8Array(S.ramp), S.ramp.length, 1, THREE.RedFormat); ramp.minFilter = ramp.magFilter = THREE.NearestFilter; ramp.needsUpdate = true; }
  const outlines = new Map();
  const kit = {
    S, flashables: [],
    solid(hex, o = {}) {
      const color = o.map ? new THREE.Color(0xffffff) : (o.raw ? new THREE.Color(hex) : styleColor(S, hex));
      let m;
      if (S.shading === 'pbr') m = new THREE.MeshStandardMaterial({ color, map: o.map || null, roughness: o.rough ?? .78, metalness: 0 });
      else m = new THREE.MeshToonMaterial({ color, map: o.map || null, gradientMap: ramp });
      if (o.emissive) { m.emissive = new THREE.Color(o.emissive); m.emissiveIntensity = o.ei ?? 1; }
      m.userData.base = hex;
      return m;
    },
    outline(mat) {
      if (!S.outline) return null;
      let col;
      if (S.outline.tint) { const c = styleColor(S, mat.userData.base || '#888888'); c.multiplyScalar(.42); col = c; }
      else col = new THREE.Color(S.outline.color);
      const k = col.getHexString();
      if (!outlines.has(k)) outlines.set(k, new THREE.MeshBasicMaterial({ color: col, side: THREE.BackSide }));
      return outlines.get(k);
    },
    part(parent, geo, mat, o = {}) {
      const m = new THREE.Mesh(geo, mat);
      if (o.pos) m.position.set(...o.pos);
      if (o.rot) m.rotation.set(...o.rot);
      if (o.scl) m.scale.set(...o.scl);
      m.castShadow = o.shadow !== false; m.receiveShadow = !!o.receive;
      parent.add(m);
      const om = o.outline === false ? null : kit.outline(mat);
      if (om) {
        const s = o.scl ? (o.scl[0] + o.scl[1] + o.scl[2]) / 3 : 1;
        const h = new THREE.Mesh(extrude(geo, (S.outline.w * (o.ow || 1)) / s), om);
        h.castShadow = false; m.add(h);
      }
      return m;
    },
  };
  return kit;
}

// ---------- fighter ----------
class Fighter {
  constructor(def, S, scene) {
    this.def = def; this.S = S; this.side = def.side;
    this.build(scene);
    this.initState();
  }

  // builds dims, materials, joint hierarchy and meshes. Subclasses (StylishFighter) override this.
  build(scene) {
    const def = this.def, S = this.S;
    const kit = materialKit(S); this.kit = kit;
    const B = S.body, H = def.H, hh = H / S.heads[def.id], headR = hh * .5;
    const neckL = hh * B.neck, torsoL = hh * B.torso;
    const legL = H - torsoL - neckL - headR * (1 + B.squash * .9);
    const footH = Math.max(legL * .075, .035);
    const thighL = (legL - footH) * .5, shinL = (legL - footH) * .5;
    const sh = hh * B.shoulder * def.build, hip = hh * B.hip * def.build, depth = B.depth * (def.build > 1 ? 1.08 : 1);
    const arm = def.arm || 1;
    const armR = hh * B.armR * def.build * arm, foreR = hh * B.foreR * def.build * arm;
    const upperL = hh * B.upperArm, foreL = hh * B.foreArm, handR = hh * B.handR * (def.build > 1 ? 1.08 : 1);
    const thighR = hh * B.thighR * def.build, shinR = hh * B.shinR * def.build;
    Object.assign(this, { hh, headR, neckL, torsoL, legL, footH, sh, hip, handR, upperL, foreL, thighL, shinL });

    // materials
    const M = {
      skin: kit.solid(def.skin, { rough: .6 }), hair: kit.solid(def.hair, { rough: .5 }), side: kit.solid(def.hairSide || def.hair, { rough: .7 }),
      blazer: kit.solid(def.blazer, { rough: .85 }), shirt: kit.solid(def.shirt, { rough: .9 }), pants: kit.solid(def.pants, { rough: .85 }),
      shoe: kit.solid(def.shoes, { rough: .6 }), sole: kit.solid(def.sole, { rough: .6 }), tape: kit.solid(def.tape || '#ffffff', { rough: .9 }),
    };
    const absTex = outfitTexture(def, S, -.25, .62), chestTex = outfitTexture(def, S, .3, 1.05);
    M.abs = kit.solid(def.blazerOn ? def.blazer : def.shirt, { map: absTex, rough: .85 });
    M.chest = kit.solid(def.blazerOn ? def.blazer : def.shirt, { map: chestTex, rough: .85 });
    this.faces = {}; for (const e of ['normal', 'hurt', 'ko', 'win']) this.faces[e] = faceTexture(def, S, e);
    M.face = kit.solid(def.skin, { map: this.faces.normal, rough: .6 });
    this.M = M;
    for (const k in M) if (M[k].emissive) { M[k].emissive.set(0xffffff); M[k].emissiveIntensity = 0; kit.flashables.push(M[k]); }

    // hierarchy: root(pos,yaw) > tilt(pivot) > off > hips ...
    const J = {}; this.J = J;
    const root = new THREE.Group(); root.rotation.order = 'YXZ'; this.root = root; scene.add(root);
    const tilt = new THREE.Group(), off = new THREE.Group(); root.add(tilt); tilt.add(off); this.tilt = tilt; this.off = off;
    const G = (name, parent, y = 0, x = 0, z = 0) => { const g = new THREE.Group(); g.position.set(x, y, z); parent.add(g); J[name] = g; return g; };
    G('hips', off, legL);
    const part = (p, geo, mat, o) => kit.part(p, geo, mat, o);

    // pelvis + abdomen
    const pelvisR = hip * 1.02;
    part(J.hips, new THREE.SphereGeometry(pelvisR, 20, 14), M.pants, { pos: [0, -pelvisR * .25, 0], scl: [1, .78, depth * 1.15] });
    G('spine', J.hips, 0);
    const absC = [[0, hip * 1.0], [.35, hip * 1.0], [.7, sh * .8], [1, sh * .82]];
    part(J.spine, torsoGeo(absC, -torsoL * .25, torsoL * .62), M.abs, { scl: [1, 1, depth] });
    if (!def.blazerOn) {
      // blazer tied around the waist
      const tied = new THREE.Group(); tied.position.set(0, torsoL * .02, 0); J.spine.add(tied);
      part(tied, new THREE.TorusGeometry(hip * 1.06, hip * .16, 10, 28), M.blazer, { rot: [Math.PI / 2, 0, 0], scl: [1, depth * 1.15, 1] });
      part(tied, new THREE.SphereGeometry(hip * .26, 12, 10), M.blazer, { pos: [0, -hip * .05, hip * depth * 1.2] });
      for (const s of [-1, 1]) part(tied, tCapsule(hip * .16, hip * .13, hip * 1.1), M.blazer, { pos: [s * hip * .12, -hip * .05, hip * depth * 1.22], rot: [-.15, 0, s * .22] });
    }
    // chest
    G('chest', J.spine, torsoL * .45);
    const chestC = [[0, sh * .8], [.25, sh * .9], [.55, sh * 1.0], [.78, sh * .98], [.9, sh * .66], [1, headR * .42]];
    part(J.chest, torsoGeo(chestC, -torsoL * .15, torsoL * .6), M.chest, { scl: [1, 1, depth] });

    // neck & head
    G('neck', J.chest, torsoL * .56);
    const neckR = headR * (S.face === 'chibi' ? .3 : .36) * (def.build > 1 ? 1.15 : 1);
    part(J.neck, new THREE.CylinderGeometry(neckR, neckR * 1.08, neckL + headR * .5, 14), M.skin, { pos: [0, neckL * .5, 0] });
    G('head', J.neck, neckL);
    const headGeo = new THREE.SphereGeometry(headR, 40, 28);
    if (B.jaw) { const p = headGeo.attributes.position; for (let i = 0; i < p.count; i++) { const y = p.getY(i); if (y < 0) { const t = -y / headR; p.setX(i, p.getX(i) * (1 - B.jaw * Math.pow(t, 1.3))); if (p.getZ(i) > 0) p.setY(i, y - B.jaw * .22 * headR * t * (p.getZ(i) / headR)); } } }
    const headC = headR * .86 * B.squash;
    this.headMesh = part(J.head, headGeo, M.face, { pos: [0, headC, 0], scl: [S.face === 'chibi' ? 1.05 : 1, B.squash, 1] });
    for (const s of [-1, 1]) {
      const cauli = def.cauli && s === 1;
      const ear = part(J.head, new THREE.SphereGeometry(headR * .27, 12, 10), M.skin, { pos: [s * headR * .96, headC - headR * .06, -headR * .04], scl: cauli ? [.6, .82, .72] : [.42, .74, .58], ow: .7 });
      if (cauli) for (const b of [[.1, .25, .2], [.05, -.2, .25]]) part(ear, new THREE.SphereGeometry(headR * .1, 8, 6), M.skin, { pos: [b[0] * headR, b[1] * headR, b[2] * headR], outline: false });
    }
    if (S.face === 'real') part(J.head, new THREE.SphereGeometry(headR * .12, 10, 8), M.skin, { pos: [0, headC - headR * .2, headR * .98], scl: [.8, 1.1, 1] });
    this.buildHair(J.head, headR, headC, M, part, B);

    // arms
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R';
      const shoulder = G('ua' + L, J.chest, torsoL * .47, s * sh * .92, 0);
      const sleeve = def.blazerOn ? M.blazer : M.shirt;
      part(shoulder, tCapsule(armR * 1.08, armR * .95, upperL), sleeve);
      const fa = G('fa' + L, shoulder, -upperL);
      part(fa, tCapsule(foreR * 1.12, foreR * 1.12, foreL * .22), sleeve, { scl: [1.15, 1, 1.15] }); // rolled cuff
      part(fa, tCapsule(foreR, foreR * .8, foreL), M.skin);
      const ha = G('ha' + L, fa, -foreL);
      part(ha, new THREE.SphereGeometry(handR, 16, 12), M.skin, { pos: [0, -handR * .7, handR * .05], scl: [.92, 1, 1.08] });
      part(ha, new THREE.SphereGeometry(handR * .4, 10, 8), M.skin, { pos: [-s * handR * .55, -handR * .62, handR * .72], outline: false });
      if (def.tape) part(ha, new THREE.CylinderGeometry(handR * 1.0, handR * 1.0, handR * .55, 16), M.tape, { pos: [0, -handR * .65, handR * .05], scl: [.95, 1, 1.1] });
      if (def.id === 'taeo' && s < 0) { this.thumb = part(ha, tCapsule(handR * .3, handR * .26, handR * .7), M.skin, { pos: [0, -handR * .55, handR * .55], rot: [-1.57 - .2, 0, 0] }); }
    }
    // legs
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R';
      const th = G('th' + L, J.hips, -hip * .1, s * hip * .52, 0);
      part(th, tCapsule(thighR * 1.05, thighR * .82, thighL), M.pants);
      const sn = G('sh' + L, th, -thighL);
      part(sn, tCapsule(shinR, shinR * .82, shinL), M.pants);
      const ft = G('ft' + L, sn, -shinL);
      const fr = footH * .62, fl = hh * B.footL * .55;
      const shoe = tCapsule(fr, fr * 1.12, fl); shoe.rotateX(-Math.PI / 2);
      part(ft, shoe, M.shoe, { pos: [0, -footH + fr * .85, -fr * .7], scl: [.9, .8, 1] });
      const sole = tCapsule(fr * 1.05, fr * 1.15, fl * 1.02); sole.rotateX(-Math.PI / 2);
      part(ft, sole, M.sole, { pos: [0, -footH + fr * .32, -fr * .72], scl: [.95, .36, 1.02], outline: false });
    }

  }

  initState() {
    const def = this.def, S = this.S;
    this.home = new THREE.Vector3(this.side * .5 * S.dist, 0, .2);
    this.pos = this.home.clone(); this.yaw = 0; this.mode = 'show'; this.blend = 0; this.t = Math.random() * 10;
    this.clip = null; this.ct = 0; this.queue = [];
    this.flinch = new Float32Array(PLEN); this.kb = 0; this.kbv = 0; this.kbIdle = 0;
    this.flashT = 0; this.exprT = 0; this.override = null; this.tiltA = 0; this.pivot = 0; this.adj = new THREE.Vector3();
    this.cur = new Float32Array(PLEN);
    this.basePose = { show: P(this.showPoseDef || SHOW[def.id]), fight: P(this.fightPoseDef || STANCE) };
    this.setExpr('normal');
  }

  buildHair(head, r, c, M, part, B) {
    const k = B.hair, g = new THREE.Group(); g.position.y = c; head.add(g);
    const sph = (rad, ts, tl, ps, pl) => new THREE.SphereGeometry(rad, 32, 16, ps, pl, ts, tl);
    const dir = (th, ph) => new THREE.Vector3(Math.sin(th) * Math.sin(ph), Math.cos(th), Math.sin(th) * Math.cos(ph));
    const front = Math.PI / 2, gap = 1.25;
    const sq = [this.S.face === 'chibi' ? 1.05 : 1, B.squash, 1];
    if (this.def.hairStyle === 'curly') {
      part(g, sph(r * k, 0, Math.PI * .38, 0, TAU), M.hair, { scl: sq });
      part(g, sph(r * k * .99, Math.PI * .25, Math.PI * .4, front + gap, TAU - gap * 2), M.hair, { scl: sq });
      const rnd = mulberry(11);
      for (let i = 0; i < 26; i++) {
        const th = .15 + rnd() * 1.3, ph = (rnd() * 2 - 1) * Math.PI;
        if (Math.abs(ph) < .9 && th > .95) continue;
        const d = dir(th, ph).multiply(new THREE.Vector3(...sq)).multiplyScalar(r * k * .98);
        part(g, new THREE.SphereGeometry(r * (.2 + rnd() * .12), 12, 10), M.hair, { pos: [d.x, d.y, d.z], ow: .8 });
      }
      for (let i = -2; i <= 2; i++) {
        const d = dir(1.02 + Math.abs(i) * .05, i * .3).multiply(new THREE.Vector3(...sq)).multiplyScalar(r * k * 1.0);
        part(g, new THREE.SphereGeometry(r * .21, 12, 10), M.hair, { pos: [d.x, d.y - r * .05, d.z], scl: [1, 1.25, .8], ow: .8 });
      }
    } else {
      part(g, sph(r * 1.025, Math.PI * .22, Math.PI * .36, front + gap * .9, TAU - gap * 1.8), M.side, { scl: sq });
      part(g, sph(r * k * 1.02, 0, Math.PI * .3, 0, TAU), M.hair, { scl: sq });
      const q = dir(.62, 0).multiply(new THREE.Vector3(...sq)).multiplyScalar(r * k);
      part(g, new THREE.SphereGeometry(r * .5, 16, 12), M.hair, { pos: [q.x, q.y + r * .06, q.z * .95], scl: [1.35, .55, .8], rot: [-.35, 0, 0] });
      const rnd = mulberry(5);
      for (let i = 0; i < 9; i++) {
        const th = .2 + rnd() * .55, ph = (rnd() * 2 - 1) * 2.2, d = dir(th, ph).multiply(new THREE.Vector3(...sq)).multiplyScalar(r * k * 1.0);
        const cone = new THREE.ConeGeometry(r * .16, r * .34, 8); const m = part(g, cone, M.hair, { pos: [d.x, d.y, d.z], ow: .7 });
        m.lookAt(g.localToWorld(d.clone().multiplyScalar(2))); m.rotateX(Math.PI / 2);
      }
    }
  }

  setExpr(e, dur = 0) { this.expr = e; this.exprT = dur; this.M.face.map = this.faces[e]; this.M.face.needsUpdate = true; }
  facingYaw() { return this.side < 0 ? Math.PI / 2 : -Math.PI / 2; }
  setMode(m) { this.mode = m; }
  play(clip, speed = 1) { this.clip = clip; this.ct = 0; this.speed = speed; this.fired = false; }
  busy() { return !!this.clip || !!this.override; }
  hitFlinch(name, k) { const f = P(FLINCH[name]); for (let i = 0; i < PLEN; i++) this.flinch[i] += f[i] * k; }
  knock(d) { this.kbv += d * 9; this.kbIdle = 0; }
  flash() { this.flashT = .1; }
  // move rotation pivot without moving the body
  setPivot(p) {
    const a = this.tiltA, c = Math.cos(a), s = Math.sin(a), d = this.pivot - p;
    const local = new THREE.Vector3(0, d - (c * d), -(s * d));
    local.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
    this.adj.add(local); this.pivot = p;
  }

  update(dt, rdt) {
    this.t += dt;
    const target = this.mode === 'show' ? 0 : 1;
    this.blend += (target - this.blend) * Math.min(1, dt * 10);
    const yawT = this.mode === 'show' ? 0 : this.facingYaw();
    if (!this.override || !this.override.yaw) this.yaw += (yawT - this.yaw) * Math.min(1, dt * 12);

    // base pose + idle motion
    const cur = this.cur, A = this.basePose.show, Bp = this.basePose.fight;
    for (let i = 0; i < PLEN; i++) cur[i] = lerp(A[i], Bp[i], this.blend);
    const bob = Math.sin(this.t * TAU * (this.blend > .5 ? 1.7 : .45));
    cur[NJ * 3] += bob * (this.blend > .5 ? .014 : .004);
    cur[2 * 3] += bob * .025; cur[4 * 3] -= bob * .015;

    // action clip
    if (this.clip) {
      const c = this.clip; this.ct += dt * (this.speed || 1);
      const t = Math.min(this.ct, c.dur), keys = c.keys;
      let i = 0; while (i < keys.length - 2 && t > keys[i + 1][0]) i++;
      const [ta, pa] = keys[i], [tb, pb] = keys[i + 1];
      const u = clamp((t - ta) / Math.max(1e-4, tb - ta), 0, 1), e = (keys[i + 1][2] || easeInOut)(u);
      const a = pa._arr || (pa._arr = P(pa)), b = pb._arr || (pb._arr = P(pb));
      const w = c.hold ? 1 : Math.min(1, this.ct / .04, Math.max(0, (c.dur - this.ct) / .08));
      for (let k = 0; k < PLEN; k++) cur[k] = lerp(cur[k], lerp(a[k], b[k], e), w);
      if (c.hit && !this.fired && this.ct >= c.hit[0]) { this.fired = true; c.onHit && c.onHit(c.hit[1]); }
      if (c.whoosh != null && !this.whooshed && this.ct >= c.whoosh) { this.whooshed = true; c.onWhoosh && c.onWhoosh(); }
      if (this.ct >= c.dur && !c.hold) { const done = c.onDone; this.clip = null; this.whooshed = false; done && done(); }
    }
    if (this.override && this.override.pose) { const o = this.override.pose, w = this.override.w ?? 1; for (let k = 0; k < PLEN; k++) cur[k] = lerp(cur[k], o[k], w); }

    // additive flinch (decays in real-ish time so hitstop keeps it)
    for (let k = 0; k < PLEN; k++) { cur[k] += this.flinch[k]; this.flinch[k] *= Math.exp(-dt * 9); }

    // apply joints
    const J = this.J;
    for (let j = 0; j < NJ; j++) J[JN[j]].rotation.set(cur[j * 3], cur[j * 3 + 1], cur[j * 3 + 2]);
    J.uaL.rotation.z += .1; J.uaR.rotation.z -= .1;
    J.hips.position.y = this.legL * (1 - cur[NJ * 3]);
    // optional IK for show poses (hands/feet targets); weight = how much of the show pose is active
    if (this.applyIK) this.applyIK(1 - this.blend, !!this.clip || !!this.override);

    // knockback spring
    this.kb += this.kbv * dt; this.kbv *= Math.exp(-dt * 10); this.kbIdle += dt;
    if (this.kbIdle > .5 && !this.override) this.kb += (0 - this.kb) * Math.min(1, dt * 4);

    // root
    const fy = this.facingYaw();
    if (this.override && this.override.root) this.override.root(this);
    else {
      const fwd = cur[NJ * 3 + 1];
      this.pos.set(this.home.x + Math.sin(this.yaw) * fwd - Math.sin(fy) * this.kb, 0, this.home.z + Math.cos(this.yaw) * fwd - Math.cos(fy) * this.kb);
    }
    this.root.position.copy(this.pos).add(this.adj);
    this.root.rotation.set(0, this.yaw, 0);
    this.tilt.position.y = this.pivot; this.off.position.y = -this.pivot; this.tilt.rotation.x = this.tiltA;
    if (this.jitter > 0) { this.root.position.x += (Math.random() - .5) * .03; this.root.position.z += (Math.random() - .5) * .02; }

    // hit flash + expression timers
    if (this.flashT > 0) this.flashT -= rdt;
    const fi = this.flashT > 0 ? .5 : 0;
    for (const m of this.kit.flashables) m.emissiveIntensity = fi;
    if (this.exprT > 0) { this.exprT -= dt; if (this.exprT <= 0) this.setExpr('normal'); }
    if (this.thumb) this.thumb.visible = this.blend < .5 && !this.override;
  }

  worldOf(name, out = new THREE.Vector3()) {
    const g = this.J[name]; g.updateWorldMatrix(true, false);
    const off = name.startsWith('ha') ? new THREE.Vector3(0, -this.handR * .8, 0) : name.startsWith('ft') ? new THREE.Vector3(0, -this.footH * .5, this.footH) : new THREE.Vector3(0, name === 'head' ? this.headR : 0, 0);
    return out.copy(off).applyMatrix4(g.matrixWorld);
  }
  dispose() { this.root.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material && o.material.map) o.material.map.dispose(); }); Object.values(this.faces).forEach(t => t.dispose()); this.root.removeFromParent(); }
}
