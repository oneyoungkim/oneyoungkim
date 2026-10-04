// ---------- sculpted heads + face line textures (후지모토풍: 형태는 사실, 선은 최소) ----------
// Head frame F: units = head height hh (skull top → chin), origin ≈ between the ear canals,
// +Y up, +Z front, +X = the character's left. Skull top y = .56, chin y = -.44 (bible 3-2: t = .56 - y).
//   brow t .44 → y .12   eye line t .53 → y .03   nose tip y -.133 / base -.17   mouth y -.258
//   (anatomy pass 2: nasion → subnasale ≈ .23 hh, subnasale → mouth ≈ .09 hh, mouth → chin ≈ .19 hh)
// The head mesh is a lofted section stack (front superellipse + back ellipse per height) with local
// displacements for brow, sockets, cheekbones, nose, lips and chin; rays from a centre are projected onto it.
// The face lines are painted in the same F coordinates and front-projected (planar UV), so lines sit
// exactly on the sculpted landmarks.
const HEAD_F = { top: .56, bot: -.452, dome: .16, joint: [0, -.10, -.06], c0: [0, .04, -.02] };
// face texture window (F units): x ∈ [-XS/2, XS/2], y ∈ [YB, YT]
const FACE_TEX = { XS: .9, YT: .45, YB: -.56 };

const HEAD_SCULPT = {
  siwoo: {
    // half width of the head per height (front view silhouette), below the dome. The width holds to the jaw angle
    // (≈ -.345) and then turns in to a small blunt chin (bible 3-3: small chin, live jaw angle — not a V line)
    W: [[-.452, 0], [-.44, .042], [-.42, .072], [-.39, .125], [-.36, .19], [-.345, .222], [-.32, .236], [-.28, .246], [-.22, .26], [-.16, .28], [-.1, .3], [-.04, .315], [.02, .322], [.08, .33], [.16, .334]],
    // centre-line front depth (side profile without nose / lips) and back depth
    Zf: [[-.452, .24], [-.437, .31], [-.415, .35], [-.385, .358], [-.345, .352], [-.28, .366], [-.2, .372], [-.1, .368], [0, .366], [.08, .372], [.16, .37]],
    Zb: [[-.452, .24], [-.41, .15], [-.36, .07], [-.3, -.02], [-.24, -.12], [-.18, -.215], [-.12, -.3], [-.06, -.36], [0, -.395], [.08, -.418], [.16, -.43]],
    // depth of the widest point of each section (cheekbone in front of the ear, jaw angle under it)
    zw: [[-.452, .24], [-.42, .19], [-.38, .12], [-.33, .03], [-.26, .03], [-.15, .05], [-.05, .045], [.05, .0], [.16, -.04]],
    nF: [[-.452, 2], [-.36, 2.05], [-.26, 2.1], [-.1, 2.15], [.04, 2.3], [.16, 2.2]],  // front flatness (rounder mid face: cheeks sit back of the nose)
    wb: [[-.452, .55], [-.3, .55], [-.2, .64], [-.1, .86], [-.02, 1], [.16, 1]],          // back narrowing (nape)
    brow: .016, socket: .028, cheek: .014, eyeX: .137,
    // short nose (nasion → subnasale ≈ .23 hh), the tip proud enough to break the far cheek line in 3/4
    nose: { p: [[-.17, 0], [-.158, .022], [-.146, .064], [-.133, .08], [-.115, .077], [-.082, .058], [-.035, .035], [.01, .018], [.045, .009], [.08, 0]], w: [[-.166, .046], [-.13, .041], [-.07, .032], [0, .026], [.08, .024]], ala: .024 },
    lips: 1, chin: .012, chinW: .07, lipW: .09,
    // face lines (F units). eye width .142 ≈ face width × .21, gap between the eyes ≈ one eye, iris Ø = eye width × .36
    eye: { x0: .07, x1: .205, y: .03, h: .045, tilt: .006, lid: .36, ir: .024, look: [.008, 0] },
    browY: .108, browW: .0068, mouthW: .09, mouthY: -.258, ear: { x: .315, y: -.025, z: -.065, h: .25 },
  },
  taeo: {
    // longer face, square jaw: the width holds down to a wide jaw angle and a broad chin
    W: [[-.452, 0], [-.444, .066], [-.43, .106], [-.41, .152], [-.38, .206], [-.345, .262], [-.31, .292], [-.26, .31], [-.2, .324], [-.14, .33], [-.08, .342], [-.02, .346], [.04, .348], [.1, .348], [.16, .348]],
    Zf: [[-.452, .26], [-.437, .33], [-.415, .368], [-.39, .378], [-.35, .362], [-.28, .37], [-.2, .376], [-.1, .372], [0, .37], [.08, .378], [.16, .376]],
    Zb: [[-.452, .26], [-.415, .15], [-.37, .04], [-.32, -.06], [-.26, -.14], [-.18, -.225], [-.12, -.31], [-.06, -.37], [0, -.4], [.08, -.425], [.16, -.435]],
    zw: [[-.452, .26], [-.425, .19], [-.39, .1], [-.345, .0], [-.3, -.03], [-.22, .0], [-.15, .03], [-.05, .04], [.05, 0], [.16, -.04]],
    nF: [[-.452, 2.4], [-.38, 2.6], [-.28, 2.3], [-.1, 2.25], [.04, 2.4], [.16, 2.2]],
    wb: [[-.452, .6], [-.3, .6], [-.2, .68], [-.1, .88], [-.02, 1], [.16, 1]],
    brow: .024, socket: .034, cheek: .02, eyeX: .138, chinW: .086,
    nose: { p: [[-.17, 0], [-.158, .024], [-.146, .068], [-.133, .085], [-.115, .082], [-.082, .062], [-.035, .042], [.01, .026], [.045, .015], [.08, 0]], w: [[-.166, .05], [-.13, .044], [-.07, .034], [0, .029], [.08, .027]], ala: .026 },
    lips: 1, chin: .02, lipW: .1,
    eye: { x0: .07, x1: .205, y: .03, h: .046, tilt: .01, lid: .27, ir: .025, look: [.004, 0] },
    browY: .096, browW: .017, mouthW: .1, mouthY: -.258, ear: { x: .335, y: -.025, z: -.065, h: .26 },
  },
};

// C1 cubic interpolation through table rows [k, v] (k ascending)
function spline1(tbl, k) {
  const n = tbl.length;
  if (k <= tbl[0][0]) return tbl[0][1];
  if (k >= tbl[n - 1][0]) return tbl[n - 1][1];
  let i = 0; while (k > tbl[i + 1][0]) i++;
  const [k0, v0] = tbl[i], [k1, v1] = tbl[i + 1], h = k1 - k0, t = (k - k0) / h;
  const m = j => { const a = tbl[Math.max(0, j - 1)], b = tbl[Math.min(n - 1, j + 1)]; return (b[1] - a[1]) / (b[0] - a[0]); };
  const m0 = m(i) * h, m1 = m(i + 1) * h, t2 = t * t, t3 = t2 * t;
  return (2 * t3 - 3 * t2 + 1) * v0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * v1 + (t3 - t2) * m1;
}
const _g = (v, m, s) => Math.exp(-((v - m) / s) * ((v - m) / s));

// section at height y: half width W, front depth Zf, back depth Zb, widest-point depth zw, front exponent nF, back narrowing wb
function headSection(P, y) {
  const F = HEAD_F;
  if (y >= F.dome) {
    const s = clamp((y - F.dome) / (F.top - F.dome), 0, 1), q = Math.sqrt(Math.max(0, 1 - s * s));
    const W0 = spline1(P.W, F.dome), Zf0 = spline1(P.Zf, F.dome), Zb0 = spline1(P.Zb, F.dome), zw0 = spline1(P.zw, F.dome), zc = (Zf0 + Zb0) / 2;
    return { W: W0 * q, Zf: zc + (Zf0 - zc) * q, Zb: zc + (Zb0 - zc) * q, zw: zc + (zw0 - zc) * q, nF: spline1(P.nF, F.dome), wb: 1 };
  }
  return { W: spline1(P.W, y), Zf: spline1(P.Zf, y), Zb: spline1(P.Zb, y), zw: spline1(P.zw, y), nF: spline1(P.nF, y), wb: spline1(P.wb, y) };
}
// front relief (added to the front depth)
function headDisp(P, x, y) {
  const ax = Math.abs(x);
  let d = 0;
  d += P.brow * _g(y, .125, .032) * (1 - smooth01(.17, .27, ax));                       // brow ridge
  d -= P.socket * _g(ax, P.eyeX, .07) * _g(y, .033, .045);                               // eye sockets
  d += P.cheek * _g(ax, .235, .055) * _g(y, P.cheekY ?? -.035, .05);                    // cheekbones (win: raised)
  d -= .008 * _g(ax, .2, .05) * _g(y, -.17, .05);                                        // flat plane under the cheekbone
  const N = P.nose, np = spline1(N.p, y), nw = spline1(N.w, y);
  d += np * Math.exp(-Math.pow(ax / nw, 2.6));                                           // nose ridge + tip
  d += N.ala * _g(ax, .052, .02) * _g(y, -.15, .02);                                      // nostril wings
  d -= .004 * _g(x, 0, .012) * smooth01(-.212, -.197, y) * (1 - smooth01(-.178, -.163, y)); // philtrum
  const L = P.lips, lw = P.lipW || .095;
  d += .008 * _g(y, -.238, .05) * Math.exp(-Math.pow(ax / .14, 2));                      // maxilla / teeth arch (lips sit proud of the nose-chin line)
  d += .02 * L * _g(y, -.218, .024) * Math.exp(-Math.pow(ax / lw, 4));                   // upper lip
  d -= .007 * L * _g(y, -.258, .007) * Math.exp(-Math.pow(ax / (lw * .96), 4));          // mouth line
  d += .018 * L * _g(y, -.285, .019) * Math.exp(-Math.pow(ax / (lw * .85), 4));          // lower lip
  d -= .005 * _g(ax, lw, .014) * _g(y, -.258, .016);                                     // mouth corners
  d -= .011 * _g(y, -.335, .018) * Math.exp(-Math.pow(ax / .075, 2));                    // labiomental fold
  d += P.chin * _g(y, -.395, .03) * Math.exp(-Math.pow(ax / (P.chinW || .07), 2.6));       // chin
  return d;
}
function headInside(P, x, y, z) {
  if (y > HEAD_F.top || y < HEAD_F.bot) return false;
  const s = headSection(P, y), ax = Math.abs(x);
  if (ax >= s.W) return false;
  if (z >= s.zw) {
    const zf = s.zw + (s.Zf - s.zw) * Math.pow(Math.max(0, 1 - Math.pow(ax / s.W, s.nF)), 1 / s.nF) + headDisp(P, x, y);
    return z < zf;
  }
  const dd = s.zw - s.Zb; if (dd <= 1e-6) return false;
  const t = (s.zw - z) / dd; if (t >= 1) return false;
  return ax < s.W * lerp(1, s.wb, smooth01(0, .4, t)) * Math.sqrt(1 - t * t);
}
// distance from c0 to the head surface along unit dir (F units). hint = a nearby ray's distance (warm start)
function headRay(P, dx, dy, dz, hint = 0) {
  const c = HEAD_F.c0, ins = r => headInside(P, c[0] + dx * r, c[1] + dy * r, c[2] + dz * r);
  let a = 0, b = 0;
  const step = .04;
  if (hint > .12 && ins(hint - .07)) a = hint - .07;
  for (let r = a + step; r < 1.2; r += step) { if (!ins(r)) { b = r; break; } a = r; }
  if (!b) return a;
  for (let i = 0; i < 13; i++) { const m = (a + b) / 2; if (ins(m)) a = m; else b = m; }
  return (a + b) / 2;
}
// point on the head surface (F units) for latitude / longitude (deg) seen from c0. lon 0 = front, + toward +X
function headPoint(P, lat, lon, lift = 0) {
  const la = lat * Math.PI / 180, lo = lon * Math.PI / 180, c = HEAD_F.c0;
  const dx = Math.cos(la) * Math.sin(lo), dy = Math.sin(la), dz = Math.cos(la) * Math.cos(lo);
  const r = headRay(P, dx, dy, dz) + lift;
  return new THREE.Vector3(c[0] + dx * r, c[1] + dy * r, c[2] + dz * r);
}

// expression variants of the sculpt (bible 7-2: only jaw opening and cheek raise as shape changes).
// jaw = rotation (deg) of everything under the mouth line round the jaw hinge; lips = lip relief ×; lipK = lip width ×
const HEAD_EXPR = {
  siwoo: { hurt: { jaw: 0, lips: .35, lipK: 1.2 }, ko: { jaw: 9.5 }, win: { jaw: 2.5, lips: .7, cheek: .004, cheekY: .008 } },
  taeo: { hurt: { jaw: 0, lips: .35, lipK: 1.25 }, ko: { jaw: 10 }, win: { jaw: 5.5, lips: .35, lipK: 1.25, cheek: .008, cheekY: .015 } },
};
// cel normals on the face (anatomy pass 2, bible 7-1-2): the front-face normals lean 70 % toward an ellipsoid round
// the skull, so the shadow terminator follows the big head mass in one clean curve instead of the sculpt's small
// relief. The nose sides and the under-chin keep their sculpt normals (small nose shadow, chin band onto the neck).
function headCelNormal(x, y, z, n) {
  const c = HEAD_F.c0, ex = (x - c[0]) / (.34 * .34), ey = (y - .05) / (.5 * .5), ez = (z + .03) / (.4 * .4);
  const l = Math.hypot(ex, ey, ez) || 1, ax = Math.abs(x);
  let k = .7 * smooth01(-.03, .12, z) * smooth01(-.45, -.39, y) * (1 - smooth01(.3, .42, y));
  k *= 1 - smooth01(.075, .045, ax) * smooth01(-.2, -.165, y) * (1 - smooth01(.04, .08, y));
  const v = [lerp(n[0], ex / l, k), lerp(n[1], ey / l, k), lerp(n[2], ez / l, k)], m = Math.hypot(v[0], v[1], v[2]) || 1;
  return [v[0] / m, v[1] / m, v[2] / m];
}
// head geometry in F units scaled by hh (metres). Front faces get planar face UVs; the rest maps to plain skin.
const _headGeoCache = new Map();
// lod 0 = the visible head (80 × 112), lod 1 = a coarser copy the outline hull is built from (44 × 64): the hull only
// draws the silhouette, so it does not need the full sculpt density (half the head's triangle cost)
const HEAD_RES = [[80, 112], [44, 64]];
function sculptHeadGeo(id, hh, expr = 'normal', lod = 0) {
  // A and A′ share the same sculpt: build once per (id, hh) and reuse the geometry (a disposed geometry is simply
  // uploaded again by three on its next render, so the fighter's dispose() stays safe)
  const key = id + ':' + hh.toFixed(5) + ':' + lod;
  if (!_headGeoCache.has(key)) _headGeoCache.set(key, buildSculptHeadGeo(id, hh, ...HEAD_RES[lod]));
  const X = HEAD_EXPR[id] && HEAD_EXPR[id][expr];
  if (!X) return _headGeoCache.get(key);
  const k2 = key + ':' + expr;
  if (!_headGeoCache.has(k2)) _headGeoCache.set(k2, exprHeadGeo(id, hh, _headGeoCache.get(key), X));
  return _headGeoCache.get(k2);
}
// expression head: same topology / UVs as the neutral head (the face texture rides on it), lips and cheeks re-relieved
// and the jaw opened round the hinge (F x axis through y -.04, z 0, just in front of the ear canal)
function exprHeadGeo(id, hh, base, X) {
  const P = HEAD_SCULPT[id], P2 = Object.assign({}, P, { lips: X.lips ?? P.lips, lipW: (P.lipW || .095) * (X.lipK || 1), cheek: P.cheek + (X.cheek || 0), cheekY: -.035 + (X.cheekY || 0) });
  const g = base.clone(), p = g.attributes.position, n = p.count, th = (X.jaw || 0) * Math.PI / 180, my = P.mouthY;
  const moved = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    let x = p.getX(i) / hh, y = p.getY(i) / hh, z = p.getZ(i) / hh;
    const fz = smooth01(.12, .24, z);
    if (fz > 0) { const dd = (headDisp(P2, x, y) - headDisp(P, x, y)) * fz; if (Math.abs(dd) > 1e-6) { z += dd; moved[i] = 1; } }
    if (th) {
      const w = smooth01(my + .012, my - .024, y) * smooth01(-.14, .04, z);
      if (w > 0) { const a = th * w, dy = y + .04, dz = z, c = Math.cos(a), s = Math.sin(a); y = -.04 + dy * c - dz * s; z = dy * s + dz * c; moved[i] = Math.max(moved[i], w); }
    }
    p.setXYZ(i, x * hh, y * hh, z * hh);
  }
  const n0 = g.attributes.normal.clone();
  g.computeVertexNormals();
  const nn = g.attributes.normal;
  for (let i = 0; i < n; i++) {
    const x = p.getX(i) / hh, y = p.getY(i) / hh, z = p.getZ(i) / hh;
    // keep the neutral normals away from the moved area and near the front / back split (duplicated vertices)
    if (!moved[i] || z < .06) { nn.setXYZ(i, n0.getX(i), n0.getY(i), n0.getZ(i)); continue; }
    const q = headCelNormal(x, y, z, [nn.getX(i), nn.getY(i), nn.getZ(i)]);
    nn.setXYZ(i, q[0], q[1], q[2]);
  }
  return g;
}
function buildSculptHeadGeo(id, hh, NL = 88, NC = 120) {
  const P = HEAD_SCULPT[id], c = HEAD_F.c0, T = FACE_TEX;
  const pos = [], idx = [];
  const lonOf = u => { const s = u * 2 - 1; return Math.PI * s * (.55 + .45 * s * s); };  // denser at the front
  pos.push(c[0], c[1] + headRay(P, 0, 1, 0), c[2]);                                         // top pole
  for (let i = 1; i < NL; i++) {
    const la = Math.PI / 2 - (i / NL) * Math.PI;
    let hint = 0;
    for (let j = 0; j < NC; j++) {
      const lo = lonOf(j / NC), dx = Math.cos(la) * Math.sin(lo), dy = Math.sin(la), dz = Math.cos(la) * Math.cos(lo);
      const r = hint = headRay(P, dx, dy, dz, hint);
      pos.push(c[0] + dx * r, c[1] + dy * r, c[2] + dz * r);
    }
  }
  pos.push(c[0], c[1] - headRay(P, 0, -1, 0), c[2]);                                        // bottom pole
  const vid = (i, j) => 1 + (i - 1) * NC + ((j % NC) + NC) % NC, bottom = 1 + (NL - 1) * NC;
  for (let j = 0; j < NC; j++) idx.push(0, vid(1, j), vid(1, j + 1));
  for (let i = 1; i < NL - 1; i++) for (let j = 0; j < NC; j++) { const a = vid(i, j), b = vid(i, j + 1), d = vid(i + 1, j), e = vid(i + 1, j + 1); idx.push(a, d, b, b, d, e); }
  for (let j = 0; j < NC; j++) idx.push(vid(NL - 1, j), bottom, vid(NL - 1, j + 1));
  const g0 = new THREE.BufferGeometry();
  g0.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g0.setIndex(idx); g0.computeVertexNormals();
  // split: front triangles keep projected UVs, the others get duplicated vertices on a plain-skin texel
  const n0 = g0.attributes.normal, nv = pos.length / 3;
  const front = i => pos[i * 3 + 2] > -.03 && pos[i * 3 + 1] > -.5;
  const P2 = [], N2 = [], UV = [], W2 = [], WP = [], I2 = [], dup = new Map();
  const push = (i, plain) => {
    const x = pos[i * 3], y = pos[i * 3 + 1], z = pos[i * 3 + 2];
    P2.push(x * hh, y * hh, z * hh);
    const nq = plain ? [n0.getX(i), n0.getY(i), n0.getZ(i)] : headCelNormal(x, y, z, [n0.getX(i), n0.getY(i), n0.getZ(i)]);
    N2.push(nq[0], nq[1], nq[2]);
    if (plain) UV.push(.015, .985); else UV.push(.5 + x / T.XS, (y - T.YB) / (T.YT - T.YB));
    // outline weight (bible 7-4): face-on, no hull over the nose, philtrum and lips (it pokes out under the nose as
    // a dark wedge), thin over the eye sockets; the chin and jaw keep the full line. inkP = profile weight the hull
    // grows back to as the head turns away from the camera (07_ink INK_P): the G-pen profile line in 3/4 and side
    const ax = Math.abs(x), fz = smooth01(.2, .27, z);
    const noseZ = smooth01(.1, .075, ax) * smooth01(-.205, -.185, y) * smooth01(.09, .06, y);
    const mouthZ = smooth01(.135, .11, ax) * smooth01(-.345, -.325, y) * smooth01(-.155, -.175, y);
    const mz = Math.max(noseZ, mouthZ) * fz;
    let w = 1 - mz;
    w *= 1 - .5 * smooth01(.3, .2, ax) * smooth01(.2, .12, y) * smooth01(-.08, 0, y) * fz;
    W2.push(w * .8); WP.push(mz * .56);
    return P2.length / 3 - 1;
  };
  const map = new Int32Array(nv).fill(-1);
  for (let t = 0; t < idx.length; t += 3) {
    const tri = [idx[t], idx[t + 1], idx[t + 2]], isF = tri.every(front);
    for (const i of tri) {
      if (isF) { if (map[i] < 0) map[i] = push(i, false); I2.push(map[i]); }
      else { if (!dup.has(i)) dup.set(i, push(i, true)); I2.push(dup.get(i)); }
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(P2, 3));
  g.setAttribute('normal', new THREE.Float32BufferAttribute(N2, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(UV, 2));
  g.setAttribute('inkW', new THREE.Float32BufferAttribute(W2, 1));
  g.setAttribute('inkP', new THREE.Float32BufferAttribute(WP, 1));
  g.setIndex(I2);
  g0.dispose();
  return g;
}

// ---------- face line textures ----------
// expr: normal / hurt / ko / win. far = bold LOD for small on-screen heads (bible 7-2: upper lid ≥ 2px at 1080p)
function stylishFaceTexture(def, S, expr = 'normal', far = false) {
  const T = FACE_TEX, CW = far ? 512 : 1024, PPH = CW / T.XS, CH = Math.round((T.YT - T.YB) * PPH);
  const c = mkCanvas(CW, CH), g = c.getContext('2d');
  const P = HEAD_SCULPT[def.id], E = P.eye, mono = S.ink && S.ink.mode === 'mono', siwoo = def.id === 'siwoo';
  const X = x => (x / T.XS + .5) * CW, Y = y => (T.YT - y) * PPH, Lw = w => Math.max(1, w * PPH);
  const B = far ? 2.5 : 1;                          // line weight boost for the far LOD
  const NB = far ? 1 : 1.4;                         // near LOD: lid / brow / mouth heavier so they hold against the silhouette (bible 3-3)
  const NBL = (far ? 1 : 1.65) * (siwoo ? 1 : 1.2); // upper lid: the darkest line on the face (≈ 1.5 × the outline), Taeo's a touch heavier
  const inkC = mono ? S.ink.ink : '#1a1417';
  const paper = mono ? S.ink.paper : '#f4efe6';
  const skinHex = siwoo ? '#be8c6a' : '#f3d2bc', shHex = siwoo ? '#8f6252' : '#c29787';
  // mono: skin is a flat 10% grey so the paper-white shirt and the skin stay apart (07_ink mono skin uses the same value)
  const skin = mono ? '#' + new THREE.Color(S.ink.paper).lerp(new THREE.Color(S.ink.ink), .1).getHexString() : celCss(S, skinHex);
  const skinSh = mono ? '#9a958c' : celCss(S, shHex);
  const soft = mono ? '#55514b' : '#' + new THREE.Color(celCss(S, shHex)).lerp(new THREE.Color(inkC), .3).getHexString(); // light lines
  const blush = siwoo ? '#b86e60' : '#eba79c';
  const iris = mono ? inkC : (siwoo ? '#2b1e18' : '#3a2a20');
  g.fillStyle = skin; g.fillRect(0, 0, CW, CH);
  g.lineCap = 'round'; g.lineJoin = 'round';

  // smooth polyline through F points (Catmull-Rom), returns canvas points
  const curve = (pts, n = 10) => {
    const out = [];
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[Math.max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.min(pts.length - 1, i + 2)];
      for (let k = 0; k < n; k++) {
        const t = k / n, t2 = t * t, t3 = t2 * t;
        const f = (a, b, cc, d) => .5 * ((2 * b) + (-a + cc) * t + (2 * a - 5 * b + 4 * cc - d) * t2 + (-a + 3 * b - 3 * cc + d) * t3);
        out.push([f(p0[0], p1[0], p2[0], p3[0]), f(p0[1], p1[1], p2[1], p3[1])]);
      }
    }
    out.push(pts[pts.length - 1]);
    return out.map(([x, y]) => [X(x), Y(y)]);
  };
  // tapered brush along F points; w(t) = width in F units
  const brush = (pts, w, col = inkC) => {
    const q = curve(pts), n = q.length, L = [], R = [];
    for (let i = 0; i < n; i++) {
      const a = q[Math.max(0, i - 1)], b = q[Math.min(n - 1, i + 1)];
      let tx = b[0] - a[0], ty = b[1] - a[1]; const l = Math.hypot(tx, ty) || 1; tx /= l; ty /= l;
      const hw = Lw(w(i / (n - 1)) * B) / 2;
      L.push([q[i][0] - ty * hw, q[i][1] + tx * hw]); R.push([q[i][0] + ty * hw, q[i][1] - tx * hw]);
    }
    g.fillStyle = col; g.beginPath(); g.moveTo(L[0][0], L[0][1]);
    for (const p of L) g.lineTo(p[0], p[1]);
    for (let i = n - 1; i >= 0; i--) g.lineTo(R[i][0], R[i][1]);
    g.closePath(); g.fill();
    for (const i of [0, n - 1]) { g.beginPath(); g.arc(q[i][0], q[i][1], Lw(w(i / (n - 1)) * B) / 2, 0, TAU); g.fill(); }
  };
  const line = (pts, w, col = inkC) => brush(pts, () => w, col);
  const taper = (w0, w1, wm = null) => t => wm == null ? lerp(w0, w1, t) : (t < .5 ? lerp(w0, wm, t * 2) : lerp(wm, w1, t * 2 - 2 * .5));
  const fillPath = (pts, col) => { const q = curve(pts, 8); g.fillStyle = col; g.beginPath(); g.moveTo(q[0][0], q[0][1]); for (const p of q) g.lineTo(p[0], p[1]); g.closePath(); g.fill(); };
  const ellipse = (x, y, rx, ry, col, rot = 0) => { g.fillStyle = col; g.beginPath(); g.ellipse(X(x), Y(y), rx * PPH, ry * PPH, rot, 0, TAU); g.fill(); };
  // soft-edged flush: radial gradient squashed to an ellipse
  const softBlob = (x, y, rx, ry, col, a) => {
    const cc = new THREE.Color(col), rgb = Math.round(cc.r * 255) + ',' + Math.round(cc.g * 255) + ',' + Math.round(cc.b * 255);
    g.save(); g.translate(X(x), Y(y)); g.scale(1, ry / rx);
    const gr = g.createRadialGradient(0, 0, 0, 0, 0, rx * PPH);
    gr.addColorStop(0, 'rgba(' + rgb + ',' + a + ')'); gr.addColorStop(.55, 'rgba(' + rgb + ',' + (a * .7) + ')'); gr.addColorStop(1, 'rgba(' + rgb + ',0)');
    g.fillStyle = gr; g.beginPath(); g.arc(0, 0, rx * PPH, 0, TAU); g.fill(); g.restore();
  };

  // ---- under-fringe shadow (2nd tone) on the forehead and over the top half of the right eye (-X), Siwoo only ----
  if (siwoo) {
    const pts = [[-.4, .45], [.4, .45], [.4, .2]];
    for (let k = 0; k <= 12; k++) { const x = .36 - k * .06; pts.push([x, .17 + .025 * Math.sin(k * 2.1) - (x < -.05 ? .04 : 0)]); }
    fillPath(pts, skinSh);
    // the long fringe clump's shadow: a jagged edge across the right eye at about its middle
    const ey = [[-.02, .16], [-.04, .1], [-.075, .062], [-.11, .05], [-.15, .036], [-.19, .044], [-.23, .03], [-.27, .05], [-.31, .07], [-.34, .12], [-.36, .2]];
    g.globalAlpha = mono ? 1 : .9; fillPath(ey.concat([[-.36, .3], [-.02, .3]]), mono ? '#9a958c' : skinSh); g.globalAlpha = 1;
  }
  // ---- blush (color: faint soft-edged pink, never a doll's disc; mono: a few hatch lines) ----
  const blushK = siwoo ? (expr === 'win' ? .14 : 0) : (expr === 'win' ? .34 : expr === 'hurt' ? .12 : .07);
  if (blushK > 0) {
    for (const s of [-1, 1]) {
      if (mono) { if (blushK > .25) for (let k = 0; k < 4; k++) line([[s * (.17 + k * .022) + .012, -.07], [s * (.17 + k * .022) - .01, -.1]], .0028); }
      else softBlob(s * .195, -.08, .07, .028, blush, blushK);
    }
    if (!mono && !siwoo) softBlob(0, -.13, .022, .014, blush, blushK * .8);
  }

  // ---- eyes ----
  const ew = E.x1 - E.x0, ecx = (E.x0 + E.x1) / 2;
  // almond for side s (+1 = character's left = +X). open: 0..1.3 scales the opening, lidK = iris cover
  const almond = (s, o = {}) => {
    const open = o.open ?? 1, x0 = s * E.x0, x1 = s * E.x1, cx = s * ecx;
    const yi = E.y - .004, yo = E.y + E.tilt;
    const top = E.y + E.h * .52 * open, bot = E.y - E.h * .48 * Math.min(1.15, .55 + .45 * open);
    const upper = [[x0, yi], [lerp(x0, x1, .3), top - .004 * open], [lerp(x0, x1, .62), top], [x1, yo]];
    const lower = [[x1, yo], [lerp(x0, x1, .68), bot + .006], [lerp(x0, x1, .35), bot], [x0, yi]];
    return { x0, x1, cx, yi, yo, top, bot, upper, lower };
  };
  const eyeOpen = (s, o = {}) => {
    const A = almond(s, o), lidK = o.lid ?? E.lid, ir = (o.ir ?? E.ir) * (far ? 1.18 : 1), look = o.look || E.look;
    // white
    // white (o.shade: under the fringe -> the white takes the 2nd skin tone)
    const wcol = o.shade ? '#' + new THREE.Color(paper).lerp(new THREE.Color(skinSh), mono ? .5 : .62).getHexString() : far && !mono ? '#' + new THREE.Color(paper).lerp(new THREE.Color(skin), .35).getHexString() : paper;
    fillPath(A.upper.concat(A.lower.slice(1)), wcol);
    g.save(); g.beginPath(); { const q = curve(A.upper.concat(A.lower.slice(1)), 8); g.moveTo(q[0][0], q[0][1]); for (const p of q) g.lineTo(p[0], p[1]); g.closePath(); } g.clip();
    // shadow of the upper lid on the white (top 10%)
    if (!mono && !o.shade) { g.globalAlpha = .9; fillPath([[A.x0, A.top + .02], [A.x1, A.top + .02], [A.x1, A.yo - .002], [A.cx, A.top - E.h * .22], [A.x0, A.yi]], '#d9cfc6'); g.globalAlpha = 1; }
    // iris: top covered by the lid by lidK, bottom just above the lower lid
    const icx = A.cx + s * .006 + look[0], icy = A.top - ir + 2 * ir * lidK + look[1];
    if (o.noIris !== true) {
      ellipse(icx, icy, ir, ir * 1.04, iris);
      if (!mono) { g.strokeStyle = inkC; g.lineWidth = Lw(.004 * B); g.beginPath(); g.ellipse(X(icx), Y(icy), ir * PPH, ir * 1.04 * PPH, 0, 0, TAU); g.stroke(); }
      if (!o.noPupil) ellipse(icx, icy, ir * .3, ir * .32, inkC);
    }
    g.restore();
    // upper lid: darkest line, thickest over the middle-to-outer third, short tail (≤10% of the eye width)
    const tail = [A.x1 + s * ew * .09, A.yo + .003];
    brush(A.upper.concat([tail]), t => .011 * NBL * (t < .25 ? lerp(.35, .8, t / .25) : t < .8 ? 1 : lerp(1, .3, (t - .8) / .2)));
    // crease (double lid), thin, skin shadow colour
    if (!o.noCrease) brush([[lerp(A.x0, A.x1, .25), A.top + .011], [lerp(A.x0, A.x1, .6), A.top + .016], [A.x1 + s * .004, A.yo + .012]], t => .0042 * (1 - .5 * Math.abs(t - .5)), soft);
    // lower lid: outer two thirds, thin, + lash ticks
    const lo = [[lerp(A.x0, A.x1, .36), A.bot + .001], [lerp(A.x0, A.x1, .7), A.bot + .006], [A.x1 - s * .004, A.yo - .006]];
    brush(lo, t => .0036 * (.6 + .4 * t));
    const ticks = siwoo ? 3 : 2;
    for (let k = 0; k < ticks; k++) {
      const t = .55 + k * .17, px = lerp(A.x0, A.x1, t), py = A.bot + .002 + (t - .36) * .012;
      line([[px, py], [px + s * .005, py - .009]], .0026);
    }
    return { A, icx, icy };
  };
  const eyeClosed = (s, kind) => {
    const A = almond(s);
    if (kind === 'squeeze') {        // squeezed shut: two strokes meeting at the outer corner
      brush([[A.x0, A.y + .004], [A.cx, A.y + .018], [A.x1 + s * .01, E.y + .002]], taper(.006, .011));
      brush([[A.x0 + s * .01, E.y - .008], [A.cx, E.y - .006], [A.x1 + s * .01, E.y + .002]], taper(.004, .009));
      line([[A.x0 + s * .02, E.y + .03], [A.cx, E.y + .038]], .0035, soft);
    } else if (kind === 'smile') {   // gentle upward arc (Taeo win)
      brush([[A.x0, E.y - .004], [A.cx, E.y + .024], [A.x1 + s * .006, E.y - .002]], t => .011 * (.45 + .55 * Math.sin(Math.PI * t)));
      line([[A.x0 + s * .02, E.y - .022], [A.cx + s * .02, E.y - .026]], .003, soft);
    } else {                         // calmly closed (ko): lid line + lash ticks
      brush([[A.x0, E.y + .002], [A.cx, E.y - .008], [A.x1 + s * .006, E.y]], t => .009 * (.5 + .5 * Math.sin(Math.PI * t)));
      for (let k = 0; k < 3; k++) { const px = lerp(A.x0, A.x1, .45 + k * .18); line([[px, E.y - .007], [px + s * .004, E.y - .015]], .0025); }
    }
  };
  // dark circles: Siwoo two lines, Taeo one faint line
  const darkCircles = s => {
    const x0 = s * (E.x0 + ew * .25), x1 = s * (E.x1 - ew * .02), b = E.y - E.h * .48;
    brush([[x0, b - .011], [s * (ecx + ew * .1), b - .02], [x1, b - .008]], t => .0034 * Math.sin(Math.PI * t), soft);
    if (siwoo) brush([[x0 + s * .02, b - .023], [s * (ecx + ew * .14), b - .031], [x1 - s * .01, b - .02]], t => .003 * Math.sin(Math.PI * t), soft);
  };
  const brow = (s, knit = 0, lift = 0, w = P.browW) => {
    const x0 = s * (E.x0 - .012), x1 = s * (E.x1 + .016), y = P.browY + lift;
    if (siwoo) {
      // thin, almost level stroke, the inner end a hair lower
      brush([[x0, y - .004 - knit], [s * lerp(E.x0, E.x1, .45), y + .006 + knit * .2], [x1, y + .001]], t => w * NB * (t < .15 ? lerp(.75, 1, t / .15) : lerp(1, .45, (t - .15) / .85)));
    } else {
      // Taeo: thick straight brow, heavy at the inner end, the outer two thirds rising a little then a short drop
      brush([[x0, y - knit], [s * lerp(E.x0, E.x1, .3), y + .006 + knit * .3], [s * lerp(E.x0, E.x1, .72), y + .016 + knit * .1], [x1, y + .009]],
        t => w * NB * (t < .1 ? lerp(.8, 1.05, t / .1) : t < .55 ? lerp(1.05, .92, (t - .1) / .45) : lerp(.92, .42, (t - .55) / .45)));
    }
  };
  const knitLines = () => { for (const s of [-1, 1]) line([[s * .015, P.browY - .002], [s * .021, P.browY - .036]], .005); };

  // ---- nose: no bridge line, short line on the shadow side (+X) over the lower 40% + one nostril tick ----
  const nose = () => {
    if (!siwoo && !mono) {
      // Taeo: the nose side plane on the shadow side (crisp, no blur) + a short cast shadow under the tip
      g.globalAlpha = .55; fillPath([[.019, -.085], [.03, -.106], [.043, -.135], [.05, -.152], [.03, -.152], [.016, -.122]], skinSh);
      g.globalAlpha = .5; fillPath([[-.014, -.16], [.018, -.16], [.014, -.171], [-.01, -.171]], skinSh); g.globalAlpha = 1;
    }
    brush([[.03, -.095], [.034, -.117], [.03, -.135]], taper(.0032, .0046), soft);
    brush([[.006, -.152], [.022, -.154], [.034, -.148]], taper(.0048, .0034));
  };
  // ---- mouth helpers (y = P.mouthY) ----
  const my = P.mouthY, mw = P.mouthW, lw = P.lipW || mw;
  // gritted teeth (hurt): an open mouth shape (upper edge left → right, lower edge right → left) with the upper row of
  // 4 ordinary teeth and a shorter lower row of 3 set half a tooth off; the corners stay dark gaps. The outline is
  // heavy along the upper lip and thin along the lower, with a raised upper-lip line and a lower-lip dash outside.
  const teeth = (up, lo, x0, x1, split, curl = 0) => {
    const pts = up.concat(lo), clip = () => { const q = curve(pts, 8); g.beginPath(); g.moveTo(q[0][0], q[0][1]); for (const p of q) g.lineTo(p[0], p[1]); g.closePath(); g.clip(); };
    fillPath(pts, '#3a2224');
    g.save(); clip();
    const ys = x => split + (x - x0) / (x1 - x0) * curl;
    fillPath([[x0, my + .06], [x1, my + .06], [x1, ys(x1)], [x0, ys(x0)]], paper);
    const li = (x1 - x0) * .09;
    fillPath([[x0 + li, ys(x0 + li) - .002], [x1 - li, ys(x1 - li) - .002], [x1 - li, my - .06], [x0 + li, my - .06]], paper);
    for (let k = 1; k < 4; k++) { const x = lerp(x0, x1, k / 4); line([[x, ys(x) + .018], [x, ys(x)]], .002); }
    for (let k = 1; k < 3; k++) { const x = lerp(x0 + li, x1 - li, k / 3); line([[x, ys(x) - .002], [x, ys(x) - .013]], .002); }
    line([[x0, ys(x0)], [x1, ys(x1)]], .0022);
    // dark wedges at the corners
    for (const e of [up[0], up[up.length - 1]]) { const sx = Math.sign(e[0]); fillPath([[e[0], e[1] + .004], [e[0] - sx * .03, e[1] + .012], [e[0] - sx * .03, e[1] - .02], [e[0], e[1] - .006]], '#3a2224'); }
    g.restore();
    brush(up, t => .0056 * NB * (.55 + .45 * Math.sin(Math.PI * t)));
    brush([up[up.length - 1]].concat(lo).concat([up[0]]), t => .0026 * (.5 + .5 * Math.sin(Math.PI * t)));
    brush(up.map(([x, y]) => [x * .86, y + .013 + Math.max(0, x) * .05]), t => .003 * Math.sin(Math.PI * t), soft);   // curled upper lip
    line([[-mw * .3, my - .045], [mw * .26, my - .046]], .0036, soft);                                                    // lower-lip dash
  };

  // ================= expressions =================
  if (siwoo) {
    if (expr === 'normal' || expr === 'win') {
      for (const s of [-1, 1]) { eyeOpen(s, { lid: expr === 'win' ? .42 : E.lid, open: expr === 'win' ? .9 : 1, shade: s < 0 }); darkCircles(s); brow(s, expr === 'win' ? -.002 : 0); }
    } else if (expr === 'hurt') {
      eyeClosed(-1, 'squeeze');                                   // the eye under the fringe is squeezed shut
      eyeOpen(1, { open: 1.5, lid: .0, ir: E.ir * .65, look: [.003, .004], noCrease: true }); darkCircles(1);
      brow(-1, .036, -.008); brow(1, .034, .006); knitLines();
    } else if (expr === 'ko') {
      for (const s of [-1, 1]) { eyeOpen(s, { open: .7, lid: .55, look: [s * .01, .01], noPupil: true, shade: s < 0 }); darkCircles(s); brow(s, -.006, -.004); }
    }
    nose();
    if (expr === 'normal') {
      brush([[-mw, my + .001], [0, my + .002], [mw * .9, my - .002]], t => .0078 * NB * (t < .1 ? .6 : t > .85 ? .7 : 1));
      line([[-mw * .3, my - .036], [mw * .26, my - .036]], .0042, soft);
      for (const s of [-1, 1]) line([[s * lw * .98, my + .004], [s * lw * 1.04, my - .004]], .0026, soft);   // faint mouth corners at the real lip width
    } else if (expr === 'win') {
      // breathless, one corner up in a short smirk, a few upper teeth on that side
      const pts = [[-mw * .9, my + .002], [0, my - .002], [mw * .95, my + .016], [mw * .5, my - .014], [-mw * .3, my - .01]];
      fillPath(pts, '#3a2224');
      g.save(); { const q = curve(pts, 8); g.beginPath(); g.moveTo(q[0][0], q[0][1]); for (const p of q) g.lineTo(p[0], p[1]); g.closePath(); g.clip(); }
      fillPath([[mw * .05, my + .02], [mw * 1.1, my + .03], [mw * 1.1, my + .002], [mw * .05, my - .004]], paper);
      for (const x of [mw * .32, mw * .62]) line([[x, my + .02], [x, my - .01]], .0024);
      g.restore();
      brush(pts.concat([pts[0]]), t => .0045);
      line([[-mw * .2, my - .03], [mw * .3, my - .032]], .003, soft);
    } else if (expr === 'hurt') {
      // gritted teeth, wide: the upper lip pulled up on the left (+X), a cut at the corner
      teeth([[-lw * 1.2, my + .004], [-lw * .5, my + .018], [lw * .4, my + .028], [lw * 1.24, my + .016]], [[lw * 1.14, my - .016], [0, my - .022], [-lw * 1.08, my - .014]], -lw * .98, lw * 1.06, my + .001, .004);
      if (!mono) line([[lw * .62, my - .02], [lw * .7, my - .09]], .0055, '#8a2a24');   // cut lip, a line of blood
    } else if (expr === 'ko') {
      const pts = [[-mw * .6, my + .004], [0, my + .01], [mw * .6, my + .002], [mw * .3, my - .03], [-mw * .3, my - .028]];
      fillPath(pts, '#3a2224'); fillPath([[-mw * .5, my + .006], [mw * .5, my + .004], [mw * .45, my - .006], [-mw * .45, my - .004]], paper);
      brush(pts.concat([pts[0]]), () => .0045);
    }
    // band-aid across the bridge (drawn last, over the nose)
    if (def.bandaid) {
      // on the bridge, under the eye line (clear of the inner eye corners), tilted
      g.save(); g.translate(X(-.008), Y(-.042)); g.rotate(.22);
      const bw = .12 * PPH, bh = .036 * PPH;
      g.fillStyle = mono ? paper : celCss(S, '#e8cfa6'); g.strokeStyle = inkC; g.lineWidth = Lw(.0032 * B);
      g.beginPath(); g.roundRect(-bw / 2, -bh / 2, bw, bh, bh * .45); g.fill(); g.stroke();
      g.fillStyle = mono ? paper : celCss(S, '#d9b98a'); g.fillRect(-bw * .17, -bh * .36, bw * .34, bh * .72);
      g.lineWidth = Lw(.0022 * B); g.strokeRect(-bw * .17, -bh * .36, bw * .34, bh * .72);
      g.fillStyle = inkC; for (const i of [-3.4, -2.6, 2.6, 3.4]) { g.beginPath(); g.arc(i * bw * .11, 0, Lw(.0026 * B), 0, TAU); g.fill(); }
      g.restore();
    }
  } else {
    if (expr === 'normal') {
      for (const s of [-1, 1]) { eyeOpen(s, { look: [s > 0 ? .002 : .006, 0] }); darkCircles(s); brow(s); }
    } else if (expr === 'hurt') {
      for (const s of [-1, 1]) { eyeOpen(s, { open: 1.32, lid: -.08, ir: E.ir * .74, look: [0, .003], noCrease: true }); brow(s, .034, .008); }
      knitLines();
      for (const k of [0, 1]) line([[-.012 + k * .024, -.075], [-.016 + k * .032, -.1]], .003, soft);   // nose wrinkles
    } else if (expr === 'ko') {
      for (const s of [-1, 1]) { eyeClosed(s, 'calm'); brow(s, -.004, -.004); }
    } else if (expr === 'win') {
      for (const s of [-1, 1]) { eyeClosed(s, 'smile'); brow(s, -.006, .008); }
    }
    nose();
    if (expr === 'normal') {
      // easy lopsided half-smile: flat line, only the +X corner lifts
      brush([[-mw * .95, my - .001], [-mw * .2, my - .004], [mw * .55, my - .001], [mw * 1.02, my + .014]], t => .0078 * NB * (t < .1 ? .6 : t > .9 ? .55 : 1));
      line([[mw * .95, my + .02], [mw * 1.08, my + .006]], .0032, soft);
      line([[-mw * .28, my - .037], [mw * .26, my - .038]], .0042, soft);
    } else if (expr === 'hurt') {
      teeth([[-lw * 1.2, my + .008], [-lw * .45, my + .024], [lw * .45, my + .025], [lw * 1.22, my + .01]], [[lw * 1.12, my - .02], [0, my - .026], [-lw * 1.1, my - .02]], -lw * 1.0, lw * 1.02, my + .0, 0);
    } else if (expr === 'ko') {
      const pts = [[-mw * .55, my + .004], [0, my + .008], [mw * .55, my + .004], [mw * .3, my - .03], [-mw * .3, my - .03]];
      fillPath(pts, '#3a2224'); brush(pts.concat([pts[0]]), () => .0045);
    } else if (expr === 'win') {
      // big open grin with the upper row of ordinary teeth
      const pts = [[-mw * 1.1, my + .012], [0, my + .006], [mw * 1.1, my + .012], [mw * .6, my - .04], [0, my - .055], [-mw * .6, my - .04]];
      fillPath(pts, '#3a2224');
      g.save(); { const q = curve(pts, 8); g.beginPath(); g.moveTo(q[0][0], q[0][1]); for (const p of q) g.lineTo(p[0], p[1]); g.closePath(); g.clip(); }
      fillPath([[-mw * 1.2, my + .03], [mw * 1.2, my + .03], [mw * 1.2, my - .006], [0, my - .014], [-mw * 1.2, my - .006]], paper);
      for (let k = 1; k < 6; k++) { const x = lerp(-mw * .8, mw * .8, k / 6); line([[x, my + .02], [x, my - .012]], .0024); }
      if (!mono) { g.globalAlpha = .7; ellipse(0, my - .045, mw * .5, .014, '#a8565a'); g.globalAlpha = 1; }
      g.restore();
      brush(pts.concat([pts[0]]), () => .0048);
    }
  }
  const tex = canvasTex(c); tex.wrapS = tex.wrapT = THREE.ClampToEdgeWrapping;
  return tex;
}
