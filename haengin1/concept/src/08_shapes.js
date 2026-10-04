// ---------- stylish shapes: tubes, hair locks, open jacket, lapels, shoes, hands, props ----------
// All indexed with shared seams so normals are smooth and the outline hull never cracks.

// tube through `points` (CatmullRom). radius: number | t => r | t => [rx, rz]  (rx along frame X, rz along `up`)
// opts: radial, tubular, caps (rounded ends; an end with r≈0 becomes a sharp tip), up, twist (rad over length), capLen
function taperTube(points, radius, opts = {}) {
  const radial = opts.radial ?? 12, tubular = opts.tubular ?? 24, caps = opts.caps ?? true, twist = opts.twist || 0, capLen = opts.capLen ?? 1;
  const up = (opts.up || new THREE.Vector3(0, 0, 1)).clone().normalize();
  const pts = points.map(p => p.clone());
  const curve = pts.length > 2 ? new THREE.CatmullRomCurve3(pts, false, 'centripetal') : new THREE.LineCurve3(pts[0], pts[1]);
  const R = t => { const r = typeof radius === 'function' ? radius(t) : radius; return Array.isArray(r) ? r : [r, r]; };
  // parallel-transported frames
  const rings = [];
  let N = null, Tprev = null;
  for (let i = 0; i <= tubular; i++) {
    const t = i / tubular, c = curve.getPointAt(t), T = curve.getTangentAt(t).normalize();
    if (!N) { N = up.clone().addScaledVector(T, -up.dot(T)); if (N.lengthSq() < 1e-8) N = new THREE.Vector3(1, 0, 0).addScaledVector(T, -T.x); N.normalize(); }
    else { const q = new THREE.Quaternion().setFromUnitVectors(Tprev, T); N.applyQuaternion(q).addScaledVector(T, -N.dot(T)).normalize(); }
    Tprev = T.clone();
    let n = N.clone(); if (twist) n.applyAxisAngle(T, twist * t);
    const X = new THREE.Vector3().crossVectors(n, T).normalize();
    const [rx, rz] = R(t);
    rings.push({ c, T, X, N: n, rx, rz, t });
  }
  const pos = [], uv = [], idx = [];
  const ringStart = [];
  const pushRing = (r, v) => {
    ringStart.push(pos.length / 3);
    for (let j = 0; j < radial; j++) {
      const a = (j / radial) * TAU, ca = Math.cos(a), sa = Math.sin(a);
      pos.push(r.c.x + r.X.x * r.rx * ca + r.N.x * r.rz * sa, r.c.y + r.X.y * r.rx * ca + r.N.y * r.rz * sa, r.c.z + r.X.z * r.rx * ca + r.N.z * r.rz * sa);
      uv.push(j / radial, v);
    }
  };
  const pole = (p, v) => { const i = pos.length / 3; pos.push(p.x, p.y, p.z); uv.push(.5, v); return i; };
  const tiny = r => Math.max(r.rx, r.rz) < 1e-5;
  const capRings = (r, dir) => { // hemispherical cap rings beyond ring r along dir*T
    const out = [], k = 3, len = Math.max(r.rx, r.rz) * capLen;
    for (let s = 1; s <= k; s++) {
      const ph = (s / (k + 1)) * Math.PI / 2, f = Math.cos(ph);
      out.push({ c: r.c.clone().addScaledVector(r.T, dir * len * Math.sin(ph)), T: r.T, X: r.X, N: r.N, rx: r.rx * f, rz: r.rz * f });
    }
    return { rings: out, tip: r.c.clone().addScaledVector(r.T, dir * len) };
  };
  const seq = []; // list of ring descriptors or {pole}
  const r0 = rings[0], r1 = rings[rings.length - 1];
  if (tiny(r0)) seq.push({ pole: r0.c, v: 0 });
  else if (caps) { const cr = capRings(r0, -1); seq.push({ pole: cr.tip, v: 0 }); cr.rings.reverse().forEach(r => seq.push({ ring: r, v: 0 })); }
  for (let i = 0; i < rings.length; i++) { if ((i === 0 && tiny(r0)) || (i === rings.length - 1 && tiny(r1))) continue; seq.push({ ring: rings[i], v: rings[i].t }); }
  if (tiny(r1)) seq.push({ pole: r1.c, v: 1 });
  else if (caps) { const cr = capRings(r1, 1); cr.rings.forEach(r => seq.push({ ring: r, v: 1 })); seq.push({ pole: cr.tip, v: 1 }); }
  const ids = seq.map(s => (s.ring ? (pushRing(s.ring, s.v), { ring: ringStart[ringStart.length - 1] }) : { pole: pole(s.pole, s.v) }));
  for (let i = 0; i < ids.length - 1; i++) {
    const A = ids[i], B = ids[i + 1];
    for (let j = 0; j < radial; j++) {
      const j1 = (j + 1) % radial;
      if (A.ring != null && B.ring != null) { idx.push(A.ring + j, A.ring + j1, B.ring + j, A.ring + j1, B.ring + j1, B.ring + j); }
      else if (A.pole != null && B.ring != null) idx.push(A.pole, B.ring + j1, B.ring + j);
      else if (A.ring != null && B.pole != null) idx.push(A.ring + j, A.ring + j1, B.pole);
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.setIndex(idx); g.computeVertexNormals();
  return g;
}

// merge indexed geometries (position/normal/uv) into one
function mergeGeos(list) {
  const pos = [], nor = [], uv = [], idx = []; let off = 0;
  for (const g0 of list) {
    const g = g0.index ? g0 : g0.toNonIndexed();
    if (!g.attributes.normal) g.computeVertexNormals();
    const p = g.attributes.position, n = g.attributes.normal, u = g.attributes.uv;
    for (let i = 0; i < p.count; i++) { pos.push(p.getX(i), p.getY(i), p.getZ(i)); nor.push(n.getX(i), n.getY(i), n.getZ(i)); uv.push(u ? u.getX(i) : 0, u ? u.getY(i) : 0); }
    if (g.index) for (let i = 0; i < g.index.count; i++) idx.push(g.index.getX(i) + off); else for (let i = 0; i < p.count; i++) idx.push(i + off);
    off += p.count;
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.setIndex(idx); return g;
}

// flat pointed hair lock: starts at origin, runs along local -Y, curls toward +Z by `bend` (+ `flip` at the tip)
function hairLock({ len = .12, width = .03, thick = .012, bend = 0, flip = 0, twist = 0, taper = .9, root = .8, radial = 8, tubular = 10 } = {}) {
  const n = 6, pts = [new THREE.Vector3()];
  let p = new THREE.Vector3();
  for (let k = 1; k <= n; k++) {
    const s = (k - .5) / n, a = bend * s + flip * s * s * s;
    p = p.clone().add(new THREE.Vector3(0, -Math.cos(a), Math.sin(a)).multiplyScalar(len / n)); pts.push(p);
  }
  const prof = t => (root + (1 - root) * Math.min(1, t / .22)) * Math.pow(Math.max(0, 1 - t), taper);
  return taperTube(pts, t => [width * .5 * prof(t), thick * .5 * prof(t)], { radial, tubular, twist, caps: true, capLen: .6 });
}

// open-front jacket shell (closed solid: outer + lining + edges). ctrl = [[yFrac, halfWidth], ...] bottom→top
// open: total front opening angle (rad) or f => angle. Groups: 0 = outside + edges, 1 = lining.
function jacketGeo({ ctrl, y0, y1, open = .5, depth = .7, flare = 0, thick = .012, rows = 28, cols = 36, front = .9, zOff = 0, back = null } = {}) {
  const hwAt = f => { for (let i = 0; i < ctrl.length - 1; i++) { const [fa, ra] = ctrl[i], [fb, rb] = ctrl[i + 1]; if (f <= fb) { const t = (f - fa) / (fb - fa); return lerp(ra, rb, t * t * (3 - 2 * t)); } } return ctrl[ctrl.length - 1][1]; };
  const opAt = f => (typeof open === 'function' ? open(f) : open);
  const loop = [], M = cols + 1;
  const P = (f, phi, inset) => {
    let hw = hwAt(f) * (1 + flare * Math.pow(1 - f, 2)), hd = hw * depth * (back ? (Math.cos(phi) < 0 ? back : 1) : 1);
    hw -= inset; hd -= inset;
    const c = Math.cos(phi);
    return new THREE.Vector3(hw * Math.sin(phi), lerp(y0, y1, f), hd * c * (c > 0 ? front : 1) + zOff);
  };
  const pos = [], uv = [], idx = [], grpIn = [];
  const L = 2 * M; // loop length: outer arc then inner arc reversed
  for (let i = 0; i <= rows; i++) {
    const f = i / rows, ha = opAt(f) / 2;
    for (let j = 0; j < M; j++) { const phi = lerp(ha, TAU - ha, j / cols), v = P(f, phi, 0); pos.push(v.x, v.y, v.z); uv.push(j / cols, f); }
    for (let j = M - 1; j >= 0; j--) { const phi = lerp(ha, TAU - ha, j / cols), v = P(f, phi, thick); pos.push(v.x, v.y, v.z); uv.push(j / cols, f); }
  }
  const out = [], inn = [];
  for (let i = 0; i < rows; i++) for (let k = 0; k < L; k++) {
    const k1 = (k + 1) % L, a = i * L + k, b = i * L + k1, c = (i + 1) * L + k, d = (i + 1) * L + k1;
    const dst = k >= M && k1 >= M ? inn : out;
    dst.push(a, b, c, b, d, c);
  }
  // hem (bottom) and top caps between outer j and inner j
  for (const [i, s] of [[0, 1], [rows, -1]]) for (let j = 0; j < cols; j++) {
    const o0 = i * L + j, o1 = i * L + j + 1, n0 = i * L + (L - 1 - j), n1 = i * L + (L - 2 - j);
    if (s > 0) out.push(o0, n0, o1, o1, n0, n1); else out.push(o0, o1, n0, o1, n1, n0);
  }
  idx.push(...out, ...inn);
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.setIndex(idx); g.addGroup(0, out.length, 0); g.addGroup(out.length, inn.length, 1);
  g.computeVertexNormals();
  return g;
}

// one lapel as a thin plate in the XY plane, front = +Z. Roll line along x=0 from (0,0) (neck) down to (0,-len) (break point);
// the lapel widens toward +x with a notch near the top.
function lapelGeo({ len = .2, width = .07, notch = .02, depth = .006, peak = .3 } = {}) {
  const s = new THREE.Shape();
  s.moveTo(0, -len);
  s.lineTo(width, -len * peak);
  s.lineTo(width * .78, -len * peak + notch * .4);
  s.lineTo(width * .92, -len * peak + notch * 1.5);
  s.lineTo(width * .62, 0);
  s.lineTo(0, 0);
  s.lineTo(0, -len);
  const g = new THREE.ExtrudeGeometry(s, { depth, bevelEnabled: false, steps: 1 });
  g.translate(0, 0, -depth / 2);
  return g;
}

// shoe: origin = ground point under the ankle, toe toward +Z. Returns { upper, sole }.
function shoeGeo({ kind = 'hightop', len = .27, width = .095, height = .07 } = {}) {
  const V = (x, y, z) => new THREE.Vector3(x, y, z);
  const back = -len * .24, toe = len * .76;
  const loafer = kind === 'loafer';
  const solH = loafer ? height * .16 : height * .3;
  const body = taperTube([V(0, solH + height * .42, back), V(0, solH + height * .4, back + len * .35), V(0, solH + height * (loafer ? .22 : .3), toe - len * .1), V(0, solH + height * (loafer ? .16 : .24), toe)],
    t => [width * .5 * (.82 + .2 * Math.sin(Math.min(1, t * 1.25) * Math.PI)) * (loafer ? 1 - .25 * Math.pow(t, 3) : 1), height * (.48 - .2 * t) * (loafer ? .85 : 1)],
    { radial: 16, tubular: 16, up: V(0, 1, 0), capLen: .7 });
  let upper = body;
  if (!loafer) {
    const collar = taperTube([V(0, solH + height * .3, back + len * .08), V(0, solH + height * 1.25, back + len * .04), V(0, solH + height * 1.75, back + len * .02)], t => [width * .5 * (.95 - .12 * t), width * .5 * (1.02 - .1 * t)], { radial: 16, tubular: 8, capLen: .5 });
    upper = mergeGeos([body, collar]);
  }
  const sole = taperTube([V(0, solH * .5, back - .006), V(0, solH * .5, toe + .006)], t => [width * .5 * (loafer ? 1.0 : 1.06) * (.86 + .22 * Math.sin(Math.min(1, t * 1.2) * Math.PI)) * (loafer ? 1 - .2 * Math.pow(t, 3) : 1), solH * .5], { radial: 14, tubular: 12, up: V(0, 1, 0), capLen: .35 });
  return { upper, sole };
}

// hand: wrist at parent origin, fingers along -Y, palm facing the body (-side·X), thumb forward (+Z).
function buildHand(kit, parent, { size = .17, side = 1, curl = 0, skinMat, tapeMat, thick = 1 } = {}) {
  const V = (x, y, z) => new THREE.Vector3(x, y, z);
  const group = new THREE.Group(); parent.add(group);
  const palmL = size * .5, palmW = size * .44, palmT = size * .17 * thick, fr = size * .052 * thick;
  const palm = taperTube([V(0, .004, 0), V(-side * palmT * .1, -palmL * .55, 0), V(-side * palmT * .05, -palmL, 0)], t => [palmT * .5 * (1 + .15 * t), palmW * .5 * (.78 + .22 * Math.min(1, t * 1.6))], { radial: 14, tubular: 6, capLen: .45 });
  kit.part(group, palm, skinMat, { ow: .8 });
  const fingers = [], defs = [[.37, .96], [.12, 1.06], [-.13, 1.0], [-.36, .82]]; // [z across palm, length factor]
  const segG = new Map();
  const seg = (L, r0, r1, tip) => { const k = L.toFixed(4) + tip; if (!segG.has(k)) segG.set(k, taperTube([V(0, 0, 0), V(0, -L, 0)], t => lerp(r0, r1, t), { radial: 8, tubular: 2, capLen: tip ? .9 : .8 })); return segG.get(k); };
  for (const [zf, lf] of defs) {
    const L1 = size * .27 * lf, L2 = size * .22 * lf;
    const k1 = new THREE.Group(); k1.position.set(-side * palmT * .05, -palmL + fr * .4, zf * palmW); group.add(k1);
    kit.part(k1, seg(L1, fr, fr * .92, 0), skinMat, { ow: .55 });
    const k2 = new THREE.Group(); k2.position.y = -L1; k1.add(k2);
    kit.part(k2, seg(L2, fr * .9, fr * .72, 1), skinMat, { ow: .55 });
    fingers.push([k1, k2]);
  }
  // thumb: from the heel of the palm, angled forward and across the palm
  const tb = new THREE.Group(); tb.position.set(-side * palmT * .35, -palmL * .22, palmW * .4); group.add(tb);
  const tL1 = size * .26, tL2 = size * .2;
  kit.part(tb, seg(tL1, fr * 1.25, fr * 1.05, 0), skinMat, { ow: .55 });
  const tb2 = new THREE.Group(); tb2.position.y = -tL1; tb.add(tb2);
  kit.part(tb2, seg(tL2, fr * 1.05, fr * .8, 1), skinMat, { ow: .55 });
  if (tapeMat) {
    kit.part(group, taperTube([V(0, -palmL * .52, 0), V(0, -palmL * .98, 0)], [palmT * .58, palmW * .53], { radial: 14, tubular: 2, capLen: .25 }), tapeMat, { ow: .7 });
    kit.part(group, taperTube([V(0, .03, 0), V(0, -palmL * .2, 0)], [palmT * .62, palmW * .5], { radial: 14, tubular: 2, capLen: .25 }), tapeMat, { ow: .7 });
  }
  const api = {
    group, curl,
    setCurl(c) {
      api.curl = c;
      fingers.forEach(([a, b], i) => {
        const k = c * (1 + i * .04);
        a.rotation.set(0, 0, -side * (k * 1.5 + .08)); b.rotation.set(0, 0, -side * (k * 1.75 + .1));
      });
      // thumb folds across the palm when closed, splays a little when open
      tb.rotation.set(.35 - c * .15, 0, -side * (.25 + c * .55)); tb2.rotation.set(0, 0, -side * (.15 + c * .9));
    },
  };
  api.setCurl(curl);
  return api;
}

// 바나나우유: 노란 항아리 병 + 흰 뚜껑 + 빨대 (상표 글자 없음). origin = 병 바닥 중심
function buildBananaMilk(kit) {
  const g = new THREE.Group();
  const prof = [[0, 0], [.021, 0], [.029, .006], [.034, .022], [.0355, .04], [.033, .058], [.026, .074], [.0185, .083], [.0165, .09], [.018, .094], [.0175, .098], [0, .098]]
    .map(([r, y]) => new THREE.Vector2(Math.max(r, 1e-4), y));
  const body = new THREE.LatheGeometry(prof, 24);
  const yellow = kit.solid('#f2c641', { role: 'light' }), white = kit.solid('#f6f3ea', { role: 'light' });
  kit.part(g, body, yellow, { ow: .7 });
  const cap = new THREE.CylinderGeometry(.0185, .0185, .006, 20); cap.translate(0, .1, 0);
  kit.part(g, cap, white, { ow: .6 });
  const straw = taperTube([new THREE.Vector3(.004, .095, 0), new THREE.Vector3(.016, .17, -.012)], .0032, { radial: 8, tubular: 2, capLen: .3 });
  kit.part(g, straw, white, { ow: .5 });
  return g;
}
