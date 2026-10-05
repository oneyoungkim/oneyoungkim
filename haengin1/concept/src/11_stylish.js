// ---------- stylish fighters (A 컬러 셀 / A′ 잉크 망가) — 후지모토풍 리빌드 (FUJIMOTO_STYLE.md) ----------
// Realistic 7.4 / 7.5 head proportions, sculpted heads (10_faces.js), heavy clumped hair,
// school uniforms that fit like real clothes, jointed hands, show poses held by IK.
// Torso / coat tables are keyed by the fraction of torsoL (hips joint → neck joint).
const STYLISH_DEF = {
  siwoo: {
    heads: 7.4, legF: .52, footH: .066, sh: .16, hip: .088, upperL: .305, foreL: .268, hand: .18, neckR: .045,
    skin: '#be8c6a', skinSh: '#8f6252', hair: '#1a1f33', hairSh: '#121626',
    jacket: '#2b3049', jacketSh: '#1e2238', lapel: '#333955', lining: '#1c2033', inner: '#9e9fa4', innerSh: '#737480', pants: '#30323b', pantsSh: '#22232b',
    shoe: '#f2f0ea', shoeSh: '#c3c4cc', sole: '#e2582c', soleSh: '#b24324', tape: '#f4efe2',
    // hoodie body [f, rx, rz]: hangs over the trouser seat (wider than it) down to about the crotch
    torso: [[-.15, .144, .106], [-.06, .143, .105], [.1, .141, .103], [.26, .138, .1], [.45, .13, .09], [.65, .136, .09], [.82, .146, .092], [.95, .118, .072], [1.03, .05, .045]],
    // open blazer, one size up: [f, half width]
    coat: { hem: .1, depth: .64, flare: .05, w: [[-.17, .158], [0, .16], [.26, .152], [.54, .162], [.71, .172], [.86, .181], [.93, .184], [.98, .172], [1.01, .14], [1.04, .1], [1.065, .058]] },
    pantsR: [.082, .068, .062, .064], seat: [.13, .09], shoeKind: 'hightop', shoeLen: .27, shoeW: .098, shoeH: .086,
    sleeve: [.058, .05], cuff: .056, fore: [.03, .023],
  },
  taeo: {
    heads: 7.5, legF: .53, footH: .07, sh: .205, hip: .1, upperL: .32, foreL: .28, hand: .198, neckR: .065,
    skin: '#f3d2bc', skinSh: '#c29787', hair: '#1a1f33', hairSh: '#121626', hairSide: '#3d4252', hairSideSh: '#2c303d',
    jacket: '#2b3049', jacketSh: '#1e2238', lapel: '#333955', lining: '#1c2033', inner: '#f4f2ec', innerSh: '#bec2d0', pants: '#30323b', pantsSh: '#22232b',
    tie: '#252b41', tieSh: '#191d2e', stripe: '#8fbfb0', belt: '#16161a', shoe: '#1b1b20', shoeSh: '#121216', sole: '#121216', sock: '#24489a',
    // shirt body: thick grappler trunk (waist ≈ .85 × chest). Below the belt the shirt stays inside the trouser seat
    // (rounder, follows the hips); above it a little blousing; the yoke covers the shoulder joints; trapezius slope
    // from the shoulder up into the neck (≈ 5 cm rise)
    torso: [[.1, .145, .095], [.17, .155, .102], [.22, .172, .116], [.45, .186, .134], [.65, .2, .142], [.84, .21, .14], [.92, .21, .13], [.975, .175, .106], [1.015, .122, .084], [1.05, .078, .066]],
    pantsR: [.098, .084, .076, .078], seat: [.166, .122], seatZ: -.01, shoeKind: 'loafer', shoeLen: .29, shoeW: .1, shoeH: .062,
    sleeve: [.06, .055], cuff: .062, fore: [.054, .038],
  },
};

// poses (same format as STANCE / SHOW). ik = show-pose IK targets in root space, filled in by StylishFighter.
const STYLISH_POSES = {
  siwoo: {
    // 양키 스쿼트: deep squat, knees wide, forearms over the knees, head tilted, glaring
    show: {
      hips: [-.1, 0, 0], spine: [.38, 0, 0], chest: [.22, .05, 0], neck: [-.3, -.05, 0], head: [-.36, .14, .16],
      uaL: [-.9, 0, .4], faL: [-.9, 0, 0], haL: [.55, 0, .1], uaR: [-.9, 0, -.4], faR: [-.9, 0, 0], haR: [.55, 0, -.1],
      thL: [-1.9, -.2, .5], shL: [2.5, 0, 0], ftL: [-.65, .3, 0], thR: [-1.9, .2, -.5], shR: [2.5, 0, 0], ftR: [-.65, -.3, 0],
      drop: .65,
    },
    // low, quick high guard, chin tucked
    fight: {
      hips: [0, -.48, 0], spine: [.08, .08, 0], chest: [.12, .12, 0], neck: [.1, .04, 0], head: [.08, -.06, 0],
      uaL: [-.98, .2, .4], faL: [-2.25, 0, 0], haL: [.12, 0, 0],
      uaR: [-.72, -.14, -.38], faR: [-2.4, 0, 0], haR: [.18, 0, 0],
      thL: [-.42, .3, .08], shL: [.46, 0, 0], ftL: [.0, .2, 0],
      thR: [.32, .25, -.14], shR: [.48, 0, 0], ftR: [-.24, .35, 0],
      drop: .1,
    },
    curl: { show: .3, fight: 1 },
  },
  taeo: {
    // 콘트라포스토: weight on the right leg, left hand in the pocket (thumb out), banana milk at the chest, head tilted
    show: {
      hips: [0, .12, -.06], spine: [-.02, -.04, .03], chest: [-.05, -.1, .06], neck: [.02, -.04, -.02], head: [-.02, -.12, .12],
      uaL: [.1, 0, .3], faL: [-.5, 0, 0], haL: [0, 0, 0], uaR: [-.3, 0, -.25], faR: [-1.9, 0, 0], haR: [0, -1.3, 0],
      thL: [-.22, -.12, .14], shL: [.42, 0, 0], ftL: [-.2, .35, 0], thR: [.04, .05, .05], shR: [.03, 0, 0], ftR: [0, -.15, 0],
      drop: .015,
    },
    // judo base: low, open hands reaching for the collar
    fight: {
      hips: [0, -.28, 0], spine: [.12, .05, 0], chest: [.16, .08, 0], neck: [0, .05, 0], head: [.12, .14, 0],
      uaL: [-1.12, .12, .22], faL: [-1.05, 0, 0], haL: [-.25, 0, 0],
      uaR: [-.92, -.12, -.24], faR: [-1.3, 0, 0], haR: [-.2, 0, 0],
      thL: [-.46, .22, .16], shL: [.55, 0, 0], ftL: [-.05, .15, 0],
      thR: [.22, .15, -.22], shR: [.55, 0, 0], ftR: [-.25, .3, 0],
      drop: .15,
    },
    curl: { show: .62, fight: .28 },
  },
};

class StylishFighter extends Fighter {
  build(scene) {
    const def = this.def, S = this.S, D = STYLISH_DEF[def.id], isS = def.id === 'siwoo';
    const kit = inkKit(S); this.kit = kit; this.D = D;
    const V = (x, y, z) => new THREE.Vector3(x, y, z);
    // ---- proportions (bible 2-2): height ÷ head height (skull top → chin), head joint .66 hh below the skull top ----
    const H = def.H, hh = H / ((S.heads && S.heads[def.id]) || D.heads), headR = hh * .5;
    const legL = H * D.legF, footH = D.footH, thighL = (legL - footH) / 2, shinL = thighL;
    const neckL = hh * .56, headTop = hh * (HEAD_F.top - HEAD_F.joint[1]);
    const torsoL = (H - legL - neckL - headTop) / 1.01;
    const sh = D.sh, hip = D.hip, upperL = D.upperL, foreL = D.foreL, handR = D.hand / 2;
    Object.assign(this, { hh, headR, neckL, torsoL, legL, footH, sh, hip, handR, upperL, foreL, thighL, shinL });
    const P = STYLISH_POSES[def.id];
    this.showPoseDef = P.show; this.fightPoseDef = P.fight; this.curlDef = P.curl;

    // ---- materials (A: character cel path — multiply shadow, film saturation, no hatch / rim / fake folds) ----
    const C = (hex, o) => kit.solid(hex, Object.assign({ cel: true }, o));
    this.hairTex = this.hairStreakTex();
    const M = {
      skin: C(D.skin, { role: 'skin', shadow: D.skinSh }),
      hair: C(D.hair, { role: 'dark', shadow: D.hairSh, hi: 2, rimM: 0, keyline: false }),
      hairS: C('#ffffff', { map: this.hairTex, role: 'dark', shade: [.72, .72, .74], hi: 2, rimM: 0, keyline: false }),
      hairT: C('#ffffff', { map: this.hairStreakTex(true), role: 'dark', shade: [.72, .72, .74], hi: 2, rimM: 0, keyline: false }),
      // two-block shell: lit like the face skin (same threshold / multiplier) so its skin-coloured fade edge melts into the head
      side: C('#ffffff', { role: 'face', shade: [.93, .88, .88], hi: 2, rimM: 0, keyline: false }),
      jacket: C(D.jacket, { role: 'dark', shadow: D.jacketSh, hi: 2 }),
      lapel: C(D.lapel, { role: 'dark', shadow: D.jacketSh, hi: 2 }),
      lining: C(D.lining, { role: 'dark', hi: 2 }),
      inner: C(D.inner, { role: isS ? 'mid' : 'light', shadow: D.innerSh }),
      pants: C(D.pants, { role: 'dark', shadow: D.pantsSh, hi: 2 }),
      shoe: C(D.shoe, { role: isS ? 'light' : 'dark', shadow: D.shoeSh, gloss: isS ? 0 : 1, hi: 2 }),
      sole: C(D.sole, { role: isS ? 'spot' : 'dark', shadow: D.soleSh, hi: 2 }),
      tape: C(D.tape || '#f4efe2', { role: 'light' }),
    };
    if (D.tie) {
      M.tie = C(D.tie, { role: 'dark', shadow: D.tieSh, hi: 2 }); M.stripe = C(D.stripe, { role: 'light' });
      M.belt = C(D.belt, { role: 'dark', gloss: .4, hi: 2 }); M.buckle = C('#a9a7a0', { role: 'light' }); M.sock = C(D.sock, { role: 'spot', shade: [.7, .72, .8] });
      M.button = C('#e9e6dc', { role: 'light' });
    }
    M.side.polygonOffset = true; M.side.polygonOffsetFactor = -1; M.side.polygonOffsetUnits = -2;
    this.faces = {}; this.faceFar = false;
    M.face = C('#ffffff', { map: this.faceTex('normal', false), role: 'face' });
    this.M = M;

    // ---- joints ----
    const J = {}; this.J = J;
    const root = new THREE.Group(); root.rotation.order = 'YXZ'; this.root = root; scene.add(root);
    const tilt = new THREE.Group(), off = new THREE.Group(); root.add(tilt); tilt.add(off); this.tilt = tilt; this.off = off;
    const G = (name, parent, y = 0, x = 0, z = 0) => { const g = new THREE.Group(); g.name = name; g.position.set(x, y, z); parent.add(g); J[name] = g; return g; };
    // the head sits ~3 cm forward of the neck base: real necks lean 15-20 deg forward, not a vertical post
    const headZ = neckL * (isS ? .16 : .2);
    G('hips', off, legL); G('spine', J.hips, 0); G('chest', J.spine, torsoL * .45); G('neck', J.chest, torsoL * .56); G('head', J.neck, neckL, 0, headZ);
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R';
      G('ua' + L, J.chest, torsoL * .47, s * sh, 0); G('fa' + L, J['ua' + L], -upperL); G('ha' + L, J['fa' + L], -foreL);
      G('th' + L, J.hips, -hip * .1, s * hip, 0); G('sh' + L, J['th' + L], -thighL); G('ft' + L, J['sh' + L], -shinL);
    }
    root.updateMatrixWorld(true);
    const bones = ['hips', 'spine', 'chest', 'uaL', 'uaR'].map(n => J[n]);
    this.skel = new THREE.Skeleton(bones);
    const part = (p, geo, mat, o = {}) => kit.part(p, geo, mat, Object.assign({ receive: true }, o));
    this.part = part;
    const skinned = (geo, mat, ow = 1, wfn) => {
      skinWeights(geo, wfn);
      const m = new THREE.SkinnedMesh(geo, mat); m.castShadow = true; m.receiveShadow = true; m.frustumCulled = false;
      off.add(m); m.bind(this.skel, new THREE.Matrix4());
      const h = kit.hull(geo, Array.isArray(mat) ? mat[0] : mat, ow);
      if (h) { const hm = new THREE.SkinnedMesh(h.geo, h.mat); hm.frustumCulled = false; off.add(hm); hm.bind(this.skel, new THREE.Matrix4()); }
      return m;
    };
    // torso weights: hem → hips, belly → spine, ribcage → chest, outer shoulder → upper arm
    const torsoW = (x, y) => {
      const yr = y - legL;
      const wh = 1 - (isS ? smooth01(-.03, .09, yr) : smooth01(.05, .15, yr)), wc = smooth01(torsoL * .28, torsoL * .62, yr);
      let wua = smooth01(sh * .72, sh * 1.25, Math.abs(x)) * smooth01(torsoL * .62, torsoL * .86, yr) * .55;
      const ws = Math.max(0, 1 - wh - wc);
      const wcc = wc * (1 - wua); wua *= wc;
      return [[0, wh], [1, ws], [2, wcc], [x > 0 ? 3 : 4, wua]];
    };

    // ---- torso (hoodie / shirt) ----
    const tz = f => interpTable(D.torso, f);
    const fb = D.torso[0][0], ft = D.torso[D.torso.length - 1][0];
    this.torsoTex = this.clothTex(isS ? 'hoodie' : 'shirt');
    M.innerT = C('#ffffff', { map: this.torsoTex, role: isS ? 'mid' : 'light', shade: isS ? [.74, .74, .8] : [.78, .8, .88] });
    const torsoGeo = taperTube([V(0, legL + fb * torsoL, 0), V(0, legL + ft * torsoL, 0)], t => { const r = tz(lerp(fb, ft, t)); return [r[0], r[1]]; }, { radial: 28, tubular: 24, capLen: .3 });
    skinned(torsoGeo, M.innerT, 1, torsoW);

    // ---- Siwoo: open blazer one size up (shoulder seams drop 2–3 cm, hem at the hip) ----
    if (isS) {
      const JK = D.coat, wtab = JK.w.map(([f, w]) => [f * torsoL, w]);
      const jy0 = legL - JK.hem, jy1 = legL + wtab[wtab.length - 1][0];
      const yf = yr => (legL + yr - jy0) / (jy1 - jy0);
      const ctrl = wtab.map(([yr, w]) => [yf(yr), w]);
      const openAt = f => { const yr = lerp(jy0, jy1, f) - legL; return yr < .2 ? lerp(.9, .62, smooth01(-JK.hem, .2, yr)) : yr < torsoL * .9 ? lerp(.62, 1.5, smooth01(.2, torsoL * .9, yr)) : lerp(1.5, 2.5, smooth01(torsoL * .9, torsoL * 1.06, yr)); };
      const jg = jacketGeo({ ctrl, y0: jy0, y1: jy1, open: openAt, depth: JK.depth, flare: JK.flare, thick: .01, rows: 34, cols: 40, front: .92, back: 1.05 });
      this.jacket = skinned(jg, [M.jacket, M.lining], 1, torsoW);
      const jEdge = yr => { const f = yf(yr), ha = openAt(f) / 2, w = interpTable(wtab, yr)[0]; return new THREE.Vector3(w * Math.sin(ha), yr - torsoL * .45, w * JK.depth * .92 * Math.cos(ha)); };
      const jNorm = yr => { const f = yf(yr), ha = openAt(f) / 2 + .25, w = interpTable(wtab, yr)[0]; return new THREE.Vector3(Math.sin(ha) / w, 0, Math.cos(ha) / (w * JK.depth * .92)).normalize(); };
      this.plate(len => lapelGeo({ len, width: hh * .36, notch: hh * .1, peak: .3 }), jEdge(torsoL * .975), jEdge(torsoL * .4), M.lapel, .008, jNorm(torsoL * .7));
    }

    // ---- neck & head ----
    const nR = D.neckR, hj = HEAD_F.joint;
    // neck: from inside the chest up into the skull base, leaning into the head (throat under the jaw, nape under the occiput)
    const nTop = V(0, neckL + (-.16 - hj[1]) * hh, (-.035 - hj[2]) * hh + headZ);
    this.neckTex = this.neckShadowTex();
    M.neck = C('#ffffff', { map: this.neckTex, role: 'skin', shadow: D.skinSh });
    part(J.neck, taperTube([V(0, -.035, -.004), V(0, neckL * .5, nTop.z * .5), nTop, V(0, neckL + .02 * hh, nTop.z)], t => [nR * lerp(isS ? 1.06 : 1.25, isS ? .97 : 1.0, smooth01(0, .5, t)), nR * lerp(isS ? 1.0 : 1.08, isS ? .93 : .97, t)], { radial: 20, tubular: 10 }), M.neck, { ow: .8 });
    this.buildHead(J.head, hh, M, part);

    // ---- inner details: hood (Siwoo) / collar + loose tie (Taeo) ----
    const topY = torsoL * .56; // neck joint in chest space
    if (isS) {
      const hood = taperTube([V(.068, topY - .055, .052), V(.088, topY - .012, -.012), V(.05, topY + .012, -.078), V(0, topY + .016, -.092), V(-.05, topY + .012, -.078), V(-.088, topY - .012, -.012), V(-.068, topY - .055, .052)],
        t => [.022 + .016 * Math.sin(t * Math.PI), .028 + .012 * Math.sin(t * Math.PI)], { radial: 12, tubular: 30 });
      part(J.chest, hood, M.inner, { ow: .8 });
      const bag = taperTube([V(0, topY + .005, -.094), V(0, topY - .055, -.124), V(0, topY - .12, -.122)], t => [.08 * (1 - .35 * t), .028 * (1 - .3 * t)], { radial: 14, tubular: 8, up: V(0, 0, 1) });
      part(J.chest, bag, M.inner, { ow: .8 });
      for (const s of [-1, 1]) {
        const cord = taperTube([V(s * .028, topY - .05, .082), V(s * .031, topY - .1, .09), V(s * .028, topY - .158, .092)], .0034, { radial: 6, tubular: 6 });
        part(J.chest, cord, M.tape, { ow: .5 });
      }
    } else {
      // open collar (two buttons undone): skin V from the neck down to the tie knot (a skin patch riding the shirt, so
      // the neck runs on into the chest in one piece), collar band standing behind and beside the neck only, collar
      // points along the V edges lifted off the shirt, loosened tie with one mint stripe
      const onShirt = (f, x, lift) => { const [rx, rz] = interpTable(D.torso, f); return V(x, (f - .45) * torsoL, rz * Math.sqrt(Math.max(0, 1 - (x / rx) * (x / rx))) + lift); };
      M.vee = C('#ffffff', { map: this.veeTex(), role: 'skin' });
      part(J.chest, this.veeGeo(D, torsoL), M.vee, { outline: false, receive: true });
      const collar = jacketGeo({ ctrl: [[0, .1], [.5, .094], [1, .084]], y0: topY - .012, y1: topY + .034, open: 2.45, depth: .93, thick: .004, rows: 4, cols: 26 });
      part(J.chest, collar, [M.inner, M.inner], { pos: [0, 0, -.004], ow: .6 });
      // collar points: inner edge along the skin V (roll line a → b), the point spreading down and out over the chest
      const pointGeo = len => {
        const w = .056, sh = new THREE.Shape();
        sh.moveTo(0, 0); sh.lineTo(0, -len); sh.lineTo(w, -len * 1.16); sh.lineTo(w * .62, -len * .12); sh.lineTo(w * .4, .004); sh.lineTo(0, 0);
        const g = new THREE.ExtrudeGeometry(sh, { depth: .004, bevelEnabled: false, steps: 1 }); g.translate(0, 0, -.002); return g;
      };
      this.plate(pointGeo, onShirt(1.03, .072, .006), onShirt(.86, .014, .006), M.inner, .002, V(.25, .25, .93), .26);
      // tie on the shirt surface (z from the trunk table)
      const tz = y => interpTable(D.torso, .45 + y / torsoL)[1];
      part(J.chest, taperTube([V(.004, topY - .098, tz(topY - .098) + .008), V(.002, topY - .13, tz(topY - .13) + .012)], t => [.018 - .004 * t, .01], { radial: 10, tubular: 3, capLen: .5 }), M.tie, { ow: .7 });
      const blade = taperTube([V(.002, topY - .125, tz(topY - .125) + .011), V(.008, topY - .21, tz(topY - .21) + .016), V(-.004, topY - .31, tz(topY - .31) + .016), V(-.002, topY - .39, tz(topY - .39) + .008)], t => [.019 + .012 * t, .0045], { radial: 8, tubular: 12, capLen: .3 });
      part(J.chest, blade, M.tie, { ow: .6 });
      // faded mint diagonal stripe across the blade
      part(J.chest, taperTube([V(-.024, topY - .262, tz(topY - .262) + .022), V(.024, topY - .232, tz(topY - .232) + .021)], .0035, { radial: 6, tubular: 2, capLen: .5 }), M.stripe, { outline: false });
    }

    // ---- arms ----
    this.hands = {};
    this.sleeveTex = this.clothTex(isS ? 'sleeveJ' : 'sleeveS');
    M.sleeve = C('#ffffff', { map: this.sleeveTex, role: isS ? 'dark' : 'light', shade: isS ? [.7, .7, .78] : [.78, .8, .88], hi: 2 });
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R', ua = J['ua' + L], fa = J['fa' + L], ha = J['ha' + L];
      if (isS) part(ua, taperTube([V(s * .006, -.004, 0), V(0, -upperL * .5, 0), V(0, -upperL - .014, 0)], t => [lerp(D.sleeve[0] * .97, D.sleeve[1], t), lerp(D.sleeve[0], D.sleeve[1], t) * .94], { radial: 18, tubular: 10, capLen: .55 }), M.sleeve, { ow: 1 });
      else part(ua, taperTube([V(s * .006, -.04, 0), V(0, -upperL * .5, 0), V(0, -upperL - .014, 0)], t => [lerp(D.sleeve[0] * .86, D.sleeve[1], smooth01(0, .35, t)), lerp(D.sleeve[0] * .9, D.sleeve[1], smooth01(0, .35, t)) * .94], { radial: 18, tubular: 10, capLen: .6 }), M.sleeve, { ow: .85 });
      if (isS) {
        // blazer sleeve pushed up to mid-forearm: bunched zigzag folds, the grey hoodie cuff peeking out
        part(fa, taperTube([V(0, .03, 0), V(0, -foreL * .4, 0)], t => D.cuff * (1 - .12 * t) * (1 + .09 * Math.sin(t * Math.PI * 4.5)), { radial: 16, tubular: 18, capLen: .5 }), M.sleeve);
        part(fa, taperTube([V(0, -foreL * .36, 0), V(0, -foreL * .47, 0)], t => D.cuff * .82 * (1 + .05 * Math.sin(t * 9)), { radial: 14, tubular: 4, capLen: .5 }), M.inner, { ow: .7 });
        part(fa, taperTube([V(0, -foreL * .3, 0), V(0, -foreL + .004, 0)], t => lerp(D.fore[0], D.fore[1], t), { radial: 12, tubular: 6 }), M.skin);
      } else {
        // shirt sleeve rolled to the elbow, thick forearm
        part(fa, taperTube([V(0, .05, 0), V(0, -foreL * .1, 0)], t => D.cuff * (1 + .07 * Math.sin(t * Math.PI * 3.5)), { radial: 18, tubular: 10, capLen: .5 }), M.sleeve);
        part(fa, taperTube([V(0, -foreL * .05, 0), V(0, -foreL * .62, .005), V(0, -foreL + .004, 0)], t => lerp(D.fore[0], D.fore[1], t) * (1 + .1 * Math.sin(t * Math.PI)), { radial: 14, tubular: 8 }), M.skin);
      }
      this.hands[L] = buildHand(kit, ha, { size: D.hand, side: s, curl: P.curl.show, skinMat: M.skin, tapeMat: isS ? M.tape : null, thick: isS ? .92 : 1.15 });
    }

    // ---- legs: straight-fit school trousers, one break over the shoes ----
    const PR = D.pantsR;
    this.pantsTex = this.clothTex('pants');
    M.pantsT = C('#ffffff', { map: this.pantsTex, role: 'dark', shade: [.7, .7, .78], hi: 2 });
    const sz = D.seatZ || 0;
    // Siwoo's seat starts under the hoodie (its top would poke through the hoodie back where the spine bends in the
    // squat / guard: linear-blend skinning flattens the hoodie there)
    part(J.hips, taperTube([V(0, isS ? .03 : .115, .004), V(0, isS ? -.01 : .02, -.004 + sz), V(0, -.07, -.002 + sz * .6)], t => [lerp(D.seat[0], D.seat[0] * .92, t), lerp(D.seat[1], D.seat[1] * 1.04, t) * (isS ? lerp(.9, 1, t) : 1)], { radial: 22, tubular: 8, capLen: .45 }), M.pants, { ow: isS ? .6 : 1 });
    // slanted front pocket openings on the seat (a thin dark seam line; Siwoo's are under the hoodie)
    if (!isS) for (const s of [-1, 1]) {
      const pk = [[.72, .1], [.8, .04], [.92, -.03]].map(([fx, y]) => { const x = fx * D.seat[0]; return V(s * x, y, D.seat[1] * Math.sqrt(Math.max(0, 1 - fx * fx)) + .003); });
      part(J.hips, taperTube(pk, .0022, { radial: 6, tubular: 6, capLen: .5 }), kitSolidOnce(this, D.pantsSh, { role: 'dark', shade: [.8, .8, .8] }), { outline: false });
    }
    if (M.belt) {
      const belt = new THREE.TorusGeometry(1, .14, 8, 40); belt.rotateX(Math.PI / 2);
      part(J.hips, belt, M.belt, { pos: [0, .108, 0], scl: [D.seat[0] * .99, .1, D.seat[1] * 1.1], ow: .6 });
      part(J.hips, new THREE.BoxGeometry(.04, .03, .01), M.buckle, { pos: [0, .108, D.seat[1] * 1.1 + .005], ow: .6 });
    }
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R', th = J['th' + L], sn = J['sh' + L], ftJ = J['ft' + L];
      const th0 = isS ? .85 : 1;
      part(th, taperTube([V(0, isS ? -.02 : .05, 0), V(0, -thighL * .5, 0), V(0, -thighL - .012, .004)], t => { const r = lerp(PR[0] * lerp(th0, 1, smooth01(0, .3, t)), PR[1], t); return [r, r * .96]; }, { radial: 18, tubular: 8 }), M.pants);
      part(sn, taperTube([V(0, .022, .004), V(0, -shinL * .5, 0), V(0, -shinL + .03, 0)], t => { const r = lerp(PR[2], PR[3], t) * (1 + .06 * _g(t, .33, .2)); return [r, r * 1.02]; }, { radial: 18, tubular: 10 }), M.pantsT);
      // hem break: one soft fold sitting on the shoe
      part(sn, taperTube([V(0, -shinL + .055, 0), V(0, -shinL + (isS ? -.014 : .004), isS ? .012 : .008)], t => [PR[3] * (1.02 + (isS ? .12 : .08) * t), PR[3] * (1.05 + (isS ? .16 : .1) * t)], { radial: 18, tubular: 4, capLen: .3 }), M.pants, { ow: .8 });
      if (M.sock) part(sn, taperTube([V(0, -shinL + .02, 0), V(0, -shinL - .03, .004)], .036, { radial: 12, tubular: 2, capLen: .4 }), M.sock, { outline: false });
      const shoe = shoeGeo({ kind: D.shoeKind, len: D.shoeLen, width: D.shoeW, height: D.shoeH });
      part(ftJ, shoe.upper, M.shoe, { pos: [0, -footH, .01] });
      part(ftJ, shoe.sole, M.sole, { pos: [0, -footH, .01], ow: .8 });
      if (isS) part(ftJ, taperTube([V(-D.shoeW * .5, -footH + D.shoeH * .5, D.shoeLen * .6), V(D.shoeW * .5, -footH + D.shoeH * .5, D.shoeLen * .6)], [.006, .012], { radial: 8, tubular: 2, capLen: .4 }), M.tape, { ow: .4 });
    }

    // ---- Taeo show pose only: navy blazer draped over the shoulders, arms not in the sleeves.
    // It sits on the shoulder yoke (half width ≈ shoulder + sleeve), the fronts wrap forward so both lapels show on the
    // chest (key visual duo_show), and the body hangs close behind the arms instead of standing off like a backpack.
    if (!isS) {
      const cape = new THREE.Group(); J.chest.add(cape); this.cape = cape;
      const y0 = -torsoL * .45 - .12, y1 = topY + .035;
      const capeOpen = f => f > .9 ? lerp(2.2, 1.35, smooth01(.9, 1, f)) : f > .62 ? lerp(3.1, 2.2, smooth01(.62, .9, f)) : 3.1;
      const cg = jacketGeo({ ctrl: [[0, .262], [.4, .268], [.7, .272], [.84, .272], [.92, .25], [.965, .19], [1, .118]], y0, y1, open: capeOpen, depth: .56, flare: .02, thick: .01, rows: 30, cols: 40, front: .95, back: 1.0, zOff: -.012 });
      this.part(cape, cg, [M.jacket, M.lining], { ow: 1 });
      // lapels on the fronts (roll line from the collar to the break at mid chest)
      const ha = f => capeOpen(f) / 2, yAt = f => lerp(y0, y1, f), wAt = f => interpTable([[0, .262], [.4, .268], [.7, .272], [.84, .272], [.92, .25], [.965, .19], [1, .118]], f)[0];
      const edge = f => new THREE.Vector3(wAt(f) * Math.sin(ha(f)), yAt(f), wAt(f) * .56 * .95 * Math.cos(ha(f)) - .012);
      for (const sd of [-1, 1]) {
        const mir = new THREE.Group(); if (sd < 0) mir.scale.x = -1; cape.add(mir);
        const a = edge(.975), b = edge(.72), Yv = a.clone().sub(b).normalize();
        const Z = new THREE.Vector3(Math.sin(ha(.85)) * .6, .2, Math.cos(ha(.85))).normalize(); Z.addScaledVector(Yv, -Z.dot(Yv)).normalize();
        const X = new THREE.Vector3().crossVectors(Yv, Z);
        const g = new THREE.Group(); g.position.copy(a).addScaledVector(Z, .006); g.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(X, Yv, Z)); mir.add(g);
        this.part(g, lapelGeo({ len: a.distanceTo(b), width: .075, notch: .022, peak: .3 }), M.lapel, { ow: .7 });
      }
      // empty sleeves hanging down the back on both sides, outside the back panel (breaks the box silhouette, duo_show)
      for (const sd of [-1, 1]) {
        const sl = taperTube([V(sd * .262, topY - .05, -.13), V(sd * .27, topY - .2, -.17), V(sd * .24, y0 + .2, -.185), V(sd * .2, y0 + .03, -.17)], t => [lerp(.054, .046, t), lerp(.04, .034, t)], { radial: 14, tubular: 12, capLen: .4 });
        this.part(cape, sl, M.jacket, { ow: .8 });
      }
    }

    // ---- props ----
    if (!isS) {
      const milk = buildBananaMilk(kit); milk.scale.setScalar(1.0);
      const hold = new THREE.Group(); J.haR.add(hold);
      hold.position.set(D.hand * .085 + .037, -D.hand * .3, .0);
      hold.add(milk); milk.position.y = -.03;
      this.milk = hold;
      // show pose: right hand thumb-up, fingers forward, palm toward the body's midline (root space basis)
      const hz = new THREE.Vector3(.1, 1, .3).normalize(), hy = new THREE.Vector3(-.15, .1, -1); hy.addScaledVector(hz, -hy.dot(hz)).normalize();
      this.handShowQ = new THREE.Quaternion().setFromRotationMatrix(new THREE.Matrix4().makeBasis(new THREE.Vector3().crossVectors(hy, hz), hy, hz));
    }

    // ---- show-pose IK targets (root space) ----
    if (isS) {
      this.ikTargets = {
        FL: { foot: [.15, footH, .12], pole: [.75, 1.5, 1.3], flat: true, yaw: .42 },
        FR: { foot: [-.15, footH, .12], pole: [-.75, 1.5, 1.3], flat: true, yaw: -.42 },
        L: { hand: [.2, .39, .5], pole: [.75, .7, .32] },
        R: { hand: [-.2, .39, .5], pole: [-.75, .7, .32] },
      };
    } else {
      this.ikTargets = {
        // hand in the front trouser pocket: the wrist at the pocket mouth, fingers down inside, thumb hooked out
        L: { hand: [.14, legL - .02, .028], pole: [.75, legL + .25, -.45] },
        R: { hand: [-.03, legL + torsoL * .62, .25], pole: [-.7, legL + .1, -.25] },
        FR: { foot: [-.07, footH, -.01], pole: [-.2, .5, 1], flat: true, yaw: -.18 },
      };
      this.pocketHand = 'L';
      // pocket hand basis (root space): fingers down into the pocket (slightly in & back), palm to the thigh, thumb forward out
      const pf = new THREE.Vector3(-.16, -1, -.32).normalize(), pz = new THREE.Vector3(.1, 0, 1); pz.addScaledVector(pf, -pz.dot(pf)).normalize();
      const py = pf.clone().negate();
      this.pocketQ = new THREE.Quaternion().setFromRotationMatrix(new THREE.Matrix4().makeBasis(new THREE.Vector3().crossVectors(py, pz), py, pz));
    }
    this.registerFlash();
    // draw the far-LOD expression textures ahead of the first hit (no hitch on the first hurt face)
    const warm = () => {
      if (this._gone) return;
      for (const e of ['normal', 'hurt', 'ko', 'win']) { this.faceTex(e, true); sculptHeadGeo(def.id, hh, e); this.kit.hull(sculptHeadGeo(def.id, hh, e, 1), M.face, 1); }
    };
    // (with a timeout: on a busy main thread an idle callback may never come, and a pending one would keep this fighter alive)
    if (window.requestIdleCallback) requestIdleCallback(warm, { timeout: 1200 }); else setTimeout(warm, 60);
  }

  // hit flash: every lit material on the fighter (bible 7-3), registered once the whole body exists
  registerFlash() {
    const fl = this.kit.flashables;
    this.root.traverse(o => { if (!o.material) return; for (const m of [].concat(o.material)) if (m.emissive && !fl.includes(m)) { m.emissive.set(0xffffff); m.emissiveIntensity = 0; fl.push(m); } });
  }

  // two mirrored lapel plates riding the chest (roll line a → b, facing nrm)
  plate(geoFn, a, b, mat, lift, nrm, out = 0) {
    const J = this.J;
    for (const s of [-1, 1]) {
      const mir = new THREE.Group(); if (s < 0) mir.scale.x = -1; J.chest.add(mir);
      const Y = a.clone().sub(b).normalize();
      const Z = nrm.clone().normalize(); Z.addScaledVector(Y, -Z.dot(Y)).normalize();
      const X = new THREE.Vector3().crossVectors(Y, Z);
      if (out) { X.applyAxisAngle(Y, -out); Z.applyAxisAngle(Y, -out); }
      const g = new THREE.Group(); g.position.copy(a).addScaledVector(Z, lift); g.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(X, Y, Z)); mir.add(g);
      this.part(g, geoFn(a.distanceTo(b)), mat, { ow: .7 });
    }
  }

  // F (head frame, hh units) → head-joint local metres
  hl(p) { const j = HEAD_F.joint; return new THREE.Vector3((p.x - j[0]) * this.hh, (p.y - j[1]) * this.hh, (p.z - j[2]) * this.hh); }
  // surface point + outward normal (F units) under the point q, seen from the head centre c0
  surf(q) {
    const P = HEAD_SCULPT[this.def.id], c = new THREE.Vector3(...HEAD_F.c0);
    const d = q.clone().sub(c).normalize();
    const at = v => { const u = v.clone().normalize(); return c.clone().addScaledVector(u, headRay(P, u.x, u.y, u.z)); };
    const p = at(d);
    const t1 = new THREE.Vector3(0, 1, 0).cross(d); if (t1.lengthSq() < 1e-6) t1.set(1, 0, 0); t1.normalize();
    const t2 = d.clone().cross(t1).normalize(), e = .03;
    const a1 = at(d.clone().addScaledVector(t1, e)), b1 = at(d.clone().addScaledVector(t1, -e)), a2 = at(d.clone().addScaledVector(t2, e)), b2 = at(d.clone().addScaledVector(t2, -e));
    const n = a1.sub(b1).cross(a2.sub(b2)).normalize(); if (n.dot(d) < 0) n.negate();
    return { p, n };
  }

  buildHead(head, hh, M, part) {
    const def = this.def, isS = def.id === 'siwoo', P = HEAD_SCULPT[def.id], j = HEAD_F.joint;
    const geo = sculptHeadGeo(def.id, hh);
    this._hseq = 0;
    this.headMesh = part(head, geo, M.face, { pos: [-j[0] * hh, -j[1] * hh, -j[2] * hh], receive: false, outline: false });
    // outline hull from the coarse copy of the same sculpt (see HEAD_RES)
    const hl0 = this.kit.hull(sculptHeadGeo(def.id, hh, 'normal', 1), M.face, 1);
    this.headHull = null;
    if (hl0) { const hm = new THREE.Mesh(hl0.geo, hl0.mat); hm.castShadow = false; hm.receiveShadow = false; this.headMesh.add(hm); this.headHull = hm; }
    this.headC = this.hl(new THREE.Vector3(0, .05, 0));
    // swap the face texture LOD by on-screen head size (fine lines up close, bold lines at game distance)
    const hp = new THREE.Vector3();
    this.headMesh.onBeforeRender = (r, sc, cam) => {
      if (!cam.isPerspectiveCamera) return;
      this.headMesh.getWorldPosition(hp);
      const px = hh / (2 * hp.distanceTo(cam.position) * Math.tan(cam.fov * Math.PI / 360)) * r.domElement.height;
      const far = this.faceFar ? px < 150 : px < 125;
      if (far !== this.faceFar) { this.faceFar = far; this.applyFace(); }
    };
    // ears: realistic C between brow and nose-base height, behind the face side (Taeo's left = cauliflower)
    const E = P.ear;
    for (const s of [-1, 1]) {
      const cauli = !isS && s === 1;
      // mirror group (head space) → left-ear basis: ear x (back) = -Z, y = +Y, z (outward) = +X; then tilt back + flare out
      let ea = 0, eb = .6; for (let k = 0; k < 18; k++) { const m = (ea + eb) / 2; if (headInside(P, m, E.y, E.z)) ea = m; else eb = m; }
      const mir = new THREE.Group(); mir.position.copy(this.hl(new THREE.Vector3(s * (ea - .006), E.y, E.z))); mir.scale.x = s; head.add(mir);
      const g = new THREE.Group(); mir.add(g);
      g.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(new THREE.Vector3(0, 0, -1), new THREE.Vector3(0, 1, 0), new THREE.Vector3(1, 0, 0)));
      const inner = new THREE.Group(); g.add(inner); inner.rotation.set(-.18, (cauli ? -.46 : -.36), 0);
      const eg = earGeos(E.h * hh, { cauli });
      part(inner, eg.shell, M.skin, { ow: .75 });
      part(inner, eg.rim, M.skin, { ow: .45 });
      part(inner, eg.inner, M.skin, { ow: .4 });
      // cauliflower ear: swollen lumps along the helix (+3-5 mm), the bowl filled in (bible 4-3)
      if (cauli) {
        const eh = E.h * hh;
        for (const [x, y, r, z] of [[.18, .96, .085, .17], [.36, .87, .095, .17], [.45, .65, .09, .17], [.42, .42, .08, .16], [.24, .6, .11, .1]]) part(inner, new THREE.SphereGeometry(eh * r, 10, 8), M.skin, { pos: [x * eh * .83, y * eh - eh * .5, eh * z], scl: [1, 1.1, .8], ow: .4 });
      }
    }
    if (isS) this.hairSiwoo(head, M, part); else this.hairTaeo(head, M, part);
  }

  // hair cap: the skull offset outward by thick(lat, lon, pF) (F units, < 0 dives under the skin).
  // Quads whose four corners are all under the skin are not built, so the coarse shell can never cut through the
  // face between sculpt vertices. o.color(lat, lon, pF, t) → vertex colour, o.inkW(t) → outline weight.
  // head-hair geometry cache: the hair is a pure function of (fighter, head size, style, call order), so switching
  // A ↔ A′ or re-entering a tab reuses it (see sculptHeadGeo)
  hairGeo(build) {
    const key = this.def.id + ':' + this.hh.toFixed(5) + ':' + this.S.key + ':' + (this._hseq = (this._hseq || 0) + 1);
    let g = _hairGeoCache.get(key);
    if (!g) { g = build(); _hairGeoCache.set(key, g); }
    return g;
  }

  hairCap(head, mat, part, thickFn, o = {}) {
    const geo = this.hairGeo(() => {
    const P = HEAD_SCULPT[this.def.id], c = HEAD_F.c0, NL = o.nl || 48, NC = o.nc || 64;
    const pos = [], uv = [], idx = [], th = [], col = o.color ? [] : null, iw = o.inkW ? [] : null, ys = new Float32Array((NL + 1) * (NC + 1));
    for (let i = 0; i <= NL; i++) {
      const lat = 90 - i / NL * 180, la = lat * Math.PI / 180;
      let hint = 0;
      for (let jj = 0; jj <= NC; jj++) {
        const lon = -180 + jj / NC * 360, lo = lon * Math.PI / 180;
        const dx = Math.cos(la) * Math.sin(lo), dy = Math.sin(la), dz = Math.cos(la) * Math.cos(lo);
        const r0 = hint = headRay(P, dx, dy, dz, hint), pF = new THREE.Vector3(c[0] + dx * r0, c[1] + dy * r0, c[2] + dz * r0);
        const t = thickFn(lat, lon, pF), r = r0 + t;
        ys[th.length] = pF.y; th.push(t);
        const q = this.hl(new THREE.Vector3(c[0] + dx * r, c[1] + dy * r, c[2] + dz * r));
        pos.push(q.x, q.y, q.z); uv.push(jj / NC, i / NL);
        if (col) { const k = o.color(lat, lon, pF, t); col.push(k.r, k.g, k.b); }
        if (iw) iw.push(o.inkW(t, lat, lon, pF));
        if (o.cull && o.cull(lat, lon, pF, t)) th[th.length - 1] = -1;
      }
    }
    const under = -.004;
    for (let i = 0; i < NL; i++) for (let jj = 0; jj < NC; jj++) {
      const a = i * (NC + 1) + jj, b = a + 1, d = a + NC + 1, e = d + 1;
      if (th[a] < under && th[b] < under && th[d] < under && th[e] < under) continue;
      idx.push(a, d, b, b, d, e);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    if (col) g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
    if (iw) g.setAttribute('inkW', new THREE.Float32BufferAttribute(iw, 1));
    g.setIndex(idx); g.computeVertexNormals();
    g.userData.yF = ys; g.userData.NL = NL; g.userData.NC = NC;
    // weld the seam normals (lon -180 / 180)
    const n = g.attributes.normal;
    for (let i = 0; i <= NL; i++) { const a = i * (NC + 1), b = a + NC; const x = n.getX(a) + n.getX(b), y = n.getY(a) + n.getY(b), z = n.getZ(a) + n.getZ(b), l = Math.hypot(x, y, z) || 1; n.setXYZ(a, x / l, y / l, z / l); n.setXYZ(b, x / l, y / l, z / l); }
    return g;
    });
    return part(head, geo, mat, { receive: false, ow: o.ow ?? 1, outline: o.outline });
  }

  // hair clump: root on the scalp (lat/lon from c0), follows the scalp at `lift` along `flow`, then falls (o.fall).
  // Lens cross-section (flat on the scalp), wedge-shaped end, optional end curl (+ = flick outward).
  clump(head, mat, part, o) {
    const geo = this.hairGeo(() => {
    const steps = o.steps || 16, len = o.len, ds = len / steps;
    const P = HEAD_SCULPT[this.def.id];
    let s0 = this.surf(headPoint(P, o.lat, o.lon));
    const flow = new THREE.Vector3(...o.flow).normalize();
    const lift = s => (typeof o.lift === 'function' ? o.lift(s) : (o.lift ?? .03));
    const proj = (v, n) => v.clone().addScaledVector(n, -v.dot(n)).normalize();
    let dir = proj(flow, s0.n), sp = s0.p.clone(), n = s0.n.clone(), falling = false;
    const pts = [s0.p.clone().addScaledVector(s0.n, lift(0))], nrm = [s0.n.clone()];
    for (let k = 1; k <= steps; k++) {
      const s = k / steps;
      let pt;
      if (!falling) {
        const q = sp.clone().addScaledVector(dir, ds), r = this.surf(q);
        const d2 = r.p.clone().sub(sp).normalize();
        dir = proj(d2.lerp(proj(flow, r.n), .25), r.n);
        sp = r.p; n = r.n;
        pt = sp.clone().addScaledVector(n, lift(s));
        if (o.fall != null && n.y < o.fall) falling = true;
      } else {
        const out = new THREE.Vector3(pts[pts.length - 1].x, 0, pts[pts.length - 1].z - HEAD_F.c0[2]).normalize();
        dir.lerp(new THREE.Vector3(0, -1, 0).addScaledVector(out, o.splay ?? .15).normalize(), .3).normalize();
        n = out.clone().addScaledVector(dir, -out.dot(dir)).normalize();
        pt = pts[pts.length - 1].clone().addScaledVector(dir, ds);
      }
      // end curl: bend the last third toward the outward normal (flick) or inward (tuck)
      if (o.curl && s > .62) { const k2 = (s - .62) / .38; pt.addScaledVector(n, o.curl * k2 * k2 * ds * 2.2); }
      pts.push(pt); nrm.push(n.clone());
    }
    // rings: width along the scalp, thickness along the normal; wedge end (width stays, thickness thins)
    const tipW = o.tipW ?? .42, w = o.w, t = o.t ?? o.w * .3;
    const rings = pts.map((c, i) => {
      const s = i / (pts.length - 1);
      const T = pts[Math.min(pts.length - 1, i + 1)].clone().sub(pts[Math.max(0, i - 1)]).normalize();
      const N = nrm[i].clone().addScaledVector(T, -nrm[i].dot(T)).normalize();
      if (o.roll) N.applyAxisAngle(T, o.roll);
      const X = new THREE.Vector3().crossVectors(N, T).normalize();
      const wf = s < .12 ? lerp(.7, 1, s / .12) : lerp(1, tipW, smooth01(.45, 1, s));
      const tf = lerp(1, .06, Math.pow(s, 1.3)) * (s < .08 ? lerp(.7, 1, s / .08) : 1);
      return { c: this.hl(c), X, N, rx: w / 2 * wf * this.hh, rz: t / 2 * tf * this.hh };
    });
    const geo = frameTube(rings, { radial: o.radial || 12, capLen: o.cap ?? .22 });
    if (o.inkIn != null) {
      // the side facing the scalp / face gets a thinner (or no) hull: no dark rim under the fringe on the forehead
      const u = geo.attributes.uv, w = new Float32Array(u.count);
      for (let i = 0; i < u.count; i++) w[i] = lerp(o.inkIn, 1, smooth01(-.45, .35, Math.sin(u.getX(i) * TAU)));
      geo.setAttribute('inkW', new THREE.BufferAttribute(w, 1));
    }
    return geo;
    });
    return part(head, geo, mat, { receive: false, ow: o.ow ?? .55, outline: o.outline });
  }

  // thin stray strand (삐침): a tapered tube from a scalp point outward along `dir`
  stray(head, mat, part, lat, lon, dir, len, w, bend = .3) {
    const geo = this.hairGeo(() => {
      const P = HEAD_SCULPT[this.def.id], r = this.surf(headPoint(P, lat, lon));
      const d = new THREE.Vector3(...dir).normalize(), pts = [];
      for (let k = 0; k <= 6; k++) { const s = k / 6; pts.push(this.hl(r.p.clone().addScaledVector(r.n, .045).addScaledVector(d, len * s).addScaledVector(r.n, bend * len * s * s))); }
      return taperTube(pts, t => [w * this.hh * (1 - t * .92), w * this.hh * .45 * (1 - t * .9)], { radial: 6, tubular: 10, capLen: .4 });
    });
    return part(head, geo, mat, { receive: false, ow: .4 });
  }

  hairSiwoo(head, M, part) {
    const H = M.hair, HS = M.hairS, rnd = mulberry(7), R = (a, b) => a + (b - a) * rnd();
    // base mass: a shaggy cloud that sits low on the crown (≈ 1.2-1.5 cm over the scalp with the clumps) and puffs out
    // sideways above the ears (siwoo_face_v1: wider than tall). It dives under the skin above the hairline so the
    // clumps make the edge.
    const hemY = lon => { const a = Math.abs(lon); return a < 38 ? lerp(.3, .24, a / 38) : a < 80 ? lerp(.24, .06, (a - 38) / 42) : a < 120 ? lerp(.06, -.06, (a - 80) / 40) : lerp(-.06, -.2, (a - 120) / 60); };
    const capT = (lat, lon) => {
      const a = Math.abs(lon);
      return (lerp(.05, .062, smooth01(10, 45, lat)) - .036 * smooth01(55, 85, lat)) * lerp(.8, 1, smooth01(25, 75, a))
        + .026 * smooth01(50, 95, a) * smooth01(-2, 12, lat) * (1 - smooth01(28, 45, lat));
    };
    this.hairCap(head, H, part, (lat, lon, p) => lerp(-.03, capT(lat, lon), smooth01(hemY(lon) - .02, hemY(lon) + .07, p.y)), { ow: 1, nl: 36, nc: 52 });
    // clumps: broad, flat lens sections with flat wedge ends (bible 4-1); the scalp side carries a thin line only
    const C = o => this.clump(head, o.mat || H, part, Object.assign({ radial: 8, cap: .08, ow: .42, inkIn: .45, steps: 11 }, o));
    // crown: 7 broad lumps radiating from the whorl (back of the crown), lying on the mass; V grooves show between them
    for (let i = 0; i < 9; i++) {
      const lon = -180 + i * 40 + R(-8, 8), out = [Math.sin(lon * Math.PI / 180), -.25, Math.cos(lon * Math.PI / 180)];
      C({ lat: 70 + R(-3, 5), lon, flow: out, len: R(.4, .54), w: R(.3, .38), t: .042, lift: s => capT(lerp(80, 50, s), lon) + .006 + .012 * Math.sin(Math.PI * s), fall: -.05, splay: .5, curl: R(.12, .3), tipW: R(.1, .2), roll: R(-.15, .15), mat: i % 2 ? HS : H });
    }
    // mid layer over the upper sides and back (half under the crown lumps: no outline of its own)
    for (let i = 0; i < 9; i++) {
      const lon = -150 + i * 37.5 + R(-6, 6);
      if (Math.abs(lon) < 42) continue;
      C({ lat: R(38, 50), lon, flow: [Math.sin(lon * Math.PI / 180) * .7, -1, Math.cos(lon * Math.PI / 180) * .4], len: R(.3, .4), w: R(.26, .34), t: .045, lift: s => capT(45, lon) + .008 + .02 * s, fall: -.05, splay: .6, curl: R(.1, .3), tipW: R(.1, .2), roll: R(-.25, .25), mat: i % 2 ? H : HS, inkIn: .2, ow: .36 });
    }
    // hem: 24 narrow flat wedges all round (half over the ears, touching the nape), two staggered rows, length ±20 %,
    // pointed-wedge ends; every third one flicks outward, so the outline breaks up like the drawn cloud
    for (let i = 0; i < 24; i++) {
      const lon = 60 + i * (240 / 23) + R(-4, 4), L = lon > 180 ? lon - 360 : lon, a = Math.abs(L), low = i % 2;
      const lat = (a < 85 ? R(22, 32) : a < 120 ? R(14, 28) : R(4, 18)) - (low ? 5 : 0);
      C({ lat, lon: L, flow: [Math.sin(L * Math.PI / 180) * .4 + R(-.15, .15), -1, Math.cos(L * Math.PI / 180) * .25], len: (low ? .3 : .26) * R(.8, 1.2), w: R(.14, .22), t: .034,
        lift: s => capT(lat, L) * .85 + .01 + (low ? 0 : .006), fall: -.02, splay: R(.45, .78), curl: i % 3 === 1 ? R(.25, .45) : R(-.08, .14), tipW: R(.05, .14), roll: R(-.3, .3), mat: i % 3 ? H : HS, ow: .36, cap: .05, inkIn: .25, radial: 6, steps: 10 });
    }
    // fringe: 9 strands of uneven length hanging off the brow ridge; the long ones (-X) cover the right brow and the
    // top half of the right eye as separate strands (not one visor), the left ones stop round the left brow
    const fr = [[-44, .32, .17, -.32, .08, .1], [-35, .44, .13, -.24, -.02, .08], [-26, .48, .13, -.18, .04, .07], [-18, .5, .13, -.1, -.05, .08], [-10, .45, .13, -.04, .02, .08],
      [-2, .38, .15, .02, .06, .1], [8, .3, .16, .1, -.06, .1], [19, .26, .15, .2, .08, .1], [31, .29, .16, .3, -.05, .1]];
    for (const [lon, len, w, sway, curl, tw] of fr) C({ lat: 57, lon, flow: [sway, -1, .5], len, w, t: .05, lift: s => lerp(.072, .032, smooth01(0, .85, s)), fall: .12, splay: .1, curl, tipW: tw, ow: .38, inkIn: 0, steps: 13, radial: 8, roll: R(-.15, .15) });
    // stray strands (bible 4-2: crown 1, left side 1, back 1), lying with the flow, ≤ 4 cm, none standing up
    this.stray(head, H, part, 78, 140, [.35, .15, -.9], .075, .018, -.25);
    this.stray(head, H, part, 22, 96, [.9, -.2, -.2], .1, .022, -.3);
    this.stray(head, H, part, 12, -160, [-.4, -.5, -1], .11, .022, .3);
  }

  hairTaeo(head, M, part) {
    const H = M.hair, rnd = mulberry(3), R = (a, b) => a + (b - a) * rnd(), D = this.D, S = this.S, hh = this.hh, P = HEAD_SCULPT.taeo;
    // ---- two-block sides & back: clipped 3-6 mm, scalp showing through. A shell 1-2 mm over the skin everywhere in
    // the side area (never diving under it, so no stair edges); the edge is made by colour only: grey-navy under the
    // part line fading to 100 % skin at the edge. Edge: sideburn down to the tragus in front of the ear, ~1 cm over
    // the ear, down behind the ear to the nape; the front edge slants back from the temple to the sideburn.
    const partY = lon => { const a = Math.abs(lon); return a < 90 ? .22 : lerp(.22, .19, (a - 90) / 90); };
    const lowY = lon => { const a = Math.abs(lon); return a < 90 ? -.07 : a < 97 ? lerp(-.07, .14, smooth01(90, 97, a)) : a < 116 ? .14 : a < 138 ? lerp(.14, -.15, smooth01(116, 138, a)) : lerp(-.15, -.24, (a - 138) / 42); };
    const frontA = y => lerp(55, 82, clamp((.22 - y) / .29, 0, 1));
    const sideK = (lon, y) => { const a = Math.abs(lon), fa = frontA(y); return smooth01(fa - 6, fa + 1, a) * smooth01(lowY(lon) - .004, lowY(lon) + .022, y); };
    const keepK = (lon, y) => { const a = Math.abs(lon), fa = frontA(y); return smooth01(fa - 16, fa - 6, a) * smooth01(lowY(lon) - .045, lowY(lon) - .004, y); };
    const sideCap = this.hairCap(head, M.side, part, (lat, lon, p) => .004 + .004 * sideK(lon, p.y) * (1 - smooth01(partY(lon) + .02, partY(lon) + .07, p.y)), {
      outline: false, nl: 46, nc: 80, cull: (lat, lon, p) => keepK(lon, p.y) < .001 || p.y > partY(lon) + .09,
    });
    M.side.map = this.sideTex(sideCap.geometry.userData, (lon, y) => sideK(lon, y) * lerp(.84, 1, smooth01(lowY(lon) + .01, partY(lon) - .03, y)));
    M.side.needsUpdate = true;
    // ---- combed-back top: rises off the hairline and rolls over (front lift), thin wedge edge over the sides (no cap
    // brim, a light line only), hairline dips at the temples
    const hairY = lon => { const a = Math.abs(lon); return a < 12 ? .315 : a < 30 ? lerp(.315, .29, (a - 12) / 18) : a < 50 ? lerp(.29, .255, (a - 30) / 20) : .24; };
    const topT = (lat, lon, y) => { const a = Math.abs(lon); return lerp(.026, .017, smooth01(40, 170, a)) + .055 * (1 - smooth01(25, 85, a)) * smooth01(hairY(lon) - .01, hairY(lon) + .12, y) * (1 - smooth01(.48, .58, y) * .45); };
    this.hairCap(head, H, part, (lat, lon, p) => {
      const a = Math.abs(lon);
      const front = a < 60 ? smooth01(hairY(lon) - .01, hairY(lon) + .07, p.y) : 1;
      const side = smooth01(partY(lon) - .015, partY(lon) + .055, p.y);
      return lerp(-.03, topT(lat, lon, p.y), Math.min(front, side));
    }, { ow: .75, nl: 44, nc: 64, inkW: (t, lat, lon) => { const a = Math.abs(lon); return lerp(a > 55 ? .12 : .35, 1, smooth01(.012, .036, t)); } });
    // 6 narrower combed-back lumps from just behind the hairline into the knot, V grooves between them (lined)
    const tops = [[-46, .2], [-28, .2], [-9, .2], [9, .2], [28, .2], [46, .19]];
    tops.forEach(([lon, w], i) => {
      this.clump(head, i % 2 ? M.hairT : H, part, { lat: 44 - Math.abs(lon) * .06, lon, flow: [-Math.sin(lon * Math.PI / 180) * .3, .3, -1.3], len: R(.6, .7), w, t: .05,
        lift: s => topT(50, lon, lerp(.34, .2, s)) * smooth01(-.02, .12, s) + .016 + .012 * Math.sin(Math.PI * s), tipW: .22, ow: .42, steps: 16, radial: 9, cap: .1, inkIn: .5 });
    });
    // two short flyaways lying back along the crown (no spikes over the silhouette)
    this.stray(head, H, part, 60, -24, [-.3, .2, -1], .07, .018, -.25);
    this.stray(head, H, part, 56, 30, [.35, .15, -1], .06, .016, -.25);
    for (const s of [-1, 1]) this.clump(head, H, part, { lat: 32, lon: s * 98, flow: [-s * .3, .75, -1], len: .36, w: .2, t: .036, lift: .03, tipW: .4, ow: .38, cap: .1, inkIn: .3, radial: 9, steps: 12 });
    // ---- small messy half-up knot low on the back of the crown + deep-blue hair tie; short ends lying back-down
    const bp = this.surf(new THREE.Vector3(0, .33, -.6));
    const bun = new THREE.Group(); bun.position.copy(this.hl(bp.p.clone().addScaledVector(bp.n, .08))); head.add(bun);
    bun.quaternion.setFromUnitVectors(new THREE.Vector3(0, 0, 1), bp.n.clone().setY(bp.n.y * .5).normalize());
    part(bun, new THREE.SphereGeometry(hh * .062, 14, 10), H, { scl: [1.15, .9, .95], receive: false, ow: .6 });
    for (let k = 0; k < 4; k++) {
      const a0 = k / 4 * TAU + .4, pts = [];
      for (let jj = 0; jj <= 6; jj++) { const a = a0 + jj * .5; pts.push(new THREE.Vector3(Math.cos(a) * hh * .06, Math.sin(a) * hh * .051, (jj - 3) * hh * .011)); }
      part(bun, taperTube(pts, t => [hh * .027 * Math.sin(Math.PI * (.15 + .85 * t)), hh * .02], { radial: 8, tubular: 10 }), H, { receive: false, ow: .4 });
    }
    // two little loops sticking out of the knot (messy tie)
    for (const [a0, sz] of [[.9, .03], [3.6, .026]]) {
      const pts = []; for (let jj = 0; jj <= 6; jj++) { const a = jj / 6 * Math.PI; pts.push(new THREE.Vector3(Math.cos(a0) * hh * (.05 + sz * Math.sin(a)), Math.sin(a0) * hh * (.05 + sz * Math.sin(a)), hh * (.01 + sz * (Math.cos(a) * .5)))); }
      part(bun, taperTube(pts, hh * .011, { radial: 6, tubular: 8 }), H, { receive: false, ow: .35 });
    }
    part(bun, new THREE.TorusGeometry(hh * .045, hh * .013, 8, 20), kitSolidOnce(this, '#24489a'), { pos: [0, 0, -hh * .04], receive: false, ow: .5 });
    for (const [d, l] of [[[.55, -.35, .6], .035], [[-.5, -.45, .55], .03], [[.1, -.8, .45], .025]]) {
      const v = new THREE.Vector3(...d).normalize(), pts = [];
      for (let k = 0; k <= 5; k++) pts.push(v.clone().multiplyScalar(hh * (.05 + l * k / 5)).add(new THREE.Vector3(0, -hh * .012 * (k / 5) * (k / 5), 0)));
      part(bun, taperTube(pts, t => [hh * .018 * (1 - t * .9), hh * .01 * (1 - t * .8)], { radial: 6, tubular: 6, capLen: .4 }), H, { receive: false, ow: .35 });
    }
    // ---- forehead: one heavier strand off the parting and one short thin one beside it, both curving the same way in a
    // soft S, lying on the forehead (snapped to the skull) and ending at brow height
    for (const [lon, len, w0, sw] of [[-21, .22, .026, -.45], [-12, .11, .014, -.4]]) {
      const r0 = headPoint(P, 44, lon), pts = [];
      for (let k = 0; k <= 10; k++) {
        const s = k / 10, q = r0.clone().add(new THREE.Vector3(sw * .03 * Math.sin(Math.PI * s * 1.2) + sw * .12 * s * s, -len * (s * .35 + .65 * s * s), 0));
        const sp = this.surf(q); pts.push(this.hl(sp.p.addScaledVector(sp.n, lerp(.03, .014, s))));
      }
      part(head, taperTube(pts, t => [hh * w0 * (1 - .8 * t), hh * w0 * .45 * (1 - .6 * t)], { radial: 6, tubular: 14, capLen: .5 }), H, { receive: false, ow: .4 });
    }
  }

  // two-block colour painted per pixel in the cap's (lon, lat) UV space: k(lon, F y) = 0 skin .. 1 clipped hair.
  // The F y of each texel comes from the cap grid (bilinear), so the fade edge is smooth whatever the mesh density.
  sideTex(ud, kFn) {
    const S = this.S, D = this.D, mono = this.kit.mono, W = 1024, H = 512, c = mkCanvas(W, H), g = c.getContext('2d');
    const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));   // sRGB bytes straight from the hex
    const skin = mono ? hex('#' + new THREE.Color(S.ink.paper).lerp(new THREE.Color(S.ink.ink), .1).getHexString()) : hex(celCss(S, D.skin));
    const side = mono ? hex('#6d6a64') : hex(celCss(S, D.hairSide));
    const img = g.createImageData(W, H), d = img.data, { yF, NL, NC } = ud;
    for (let py = 0; py < H; py++) {
      const lat = -90 + 180 * (py + .5) / H, fi = (90 - lat) / 180 * NL, i0 = Math.min(NL - 1, Math.floor(fi)), ti = fi - i0;
      for (let px = 0; px < W; px++) {
        const lon = -180 + 360 * (px + .5) / W, fj = (lon + 180) / 360 * NC, j0 = Math.min(NC - 1, Math.floor(fj)), tj = fj - j0;
        const a = i0 * (NC + 1) + j0, b = a + NC + 1;
        const y = (yF[a] * (1 - tj) + yF[a + 1] * tj) * (1 - ti) + (yF[b] * (1 - tj) + yF[b + 1] * tj) * ti;
        const k = kFn(lon, y), o = (py * W + px) * 4;
        d[o] = skin[0] + (side[0] - skin[0]) * k; d[o + 1] = skin[1] + (side[1] - skin[1]) * k; d[o + 2] = skin[2] + (side[2] - skin[2]) * k; d[o + 3] = 255;
      }
    }
    g.putImageData(img, 0, 0);
    const t = canvasTex(c); t.wrapS = THREE.RepeatWrapping;
    return t;
  }


  // ---- textures ----
  // hair clump streaks: thin lighter gap lines along the flow on the outer face (bible 4-1)
  hairStreakTex(combed = false) {
    const c = mkCanvas(128, 256), g = c.getContext('2d'), S = this.S, mono = S.ink && S.ink.mode === 'mono';
    g.fillStyle = mono ? '#111111' : celCss(S, '#1a1f33'); g.fillRect(0, 0, 128, 256);
    g.strokeStyle = mono ? '#f7f5f0' : celCss(S, '#59617a'); g.lineCap = 'round';
    // combed (Taeo): several long parallel gap lines along the comb direction; loose (Siwoo): a few short ones
    const lines = combed ? [[.17, .06, .8, 1], [.26, .12, .9, 1.1], [.34, .04, .65, .8]] : [[.2, .06, .5, 1.1], [.27, .12, .62, .9], [.31, .04, .32, .8]];
    g.globalAlpha = combed ? .5 : .85; for (const [u, v0, v1, w] of lines) {
      g.lineWidth = w; g.beginPath(); g.moveTo(u * 128, v0 * 256); g.quadraticCurveTo(u * 128 + 3, (v0 + v1) * 128, u * 128 - 1, v1 * 256); g.stroke();
    }
    return canvasTex(c);
  }
  // under-chin 2nd shadow fixed on the neck (front, top) — bible 3-3 / 7-1
  neckShadowTex() {
    const N = 256, c = mkCanvas(N, N), g = c.getContext('2d'), S = this.S, D = this.D, mono = S.ink && S.ink.mode === 'mono';
    const skin = mono ? S.ink.paper : celCss(S, D.skin), sh = mono ? '#9a958c' : celCss(S, D.skinSh);
    g.fillStyle = skin; g.fillRect(0, 0, N, N);
    g.fillStyle = sh;
    // tube uv: u .25 = front, v 0 = base → 1 = top (canvas y flipped)
    g.beginPath(); g.moveTo(0, 0);
    for (let i = 0; i <= 32; i++) { const u = i / 32, f = Math.cos((u - .25) * TAU); g.lineTo(u * N, (1 - lerp(.76, .5, Math.max(0, f))) * N); }
    g.lineTo(N, 0); g.closePath(); g.fill();
    // sternocleidomastoid: two lines from behind the ears down to the pit of the throat (bible 3-3, skin shadow .4)
    g.strokeStyle = sh; g.lineCap = 'round'; g.lineWidth = N * .012;
    for (const sd of [-1, 1]) { g.beginPath(); g.moveTo((.25 + sd * .2) * N, (1 - .62) * N); g.quadraticCurveTo((.25 + sd * .12) * N, (1 - .3) * N, (.25 + sd * .025) * N, (1 - .03) * N); g.stroke(); }
    return canvasTex(c);
  }
  // Taeo's skin V: a patch riding the shirt front from the top of the trunk down to the tie knot (chest space)
  veeGeo(D, torsoL) {
    const T = D.torso, fTop = T[T.length - 1][0], fBot = .835, R = 12, Cn = 14, lift = .0016, pos = [], uv = [], idx = [];
    for (let i = 0; i <= R; i++) {
      const k = i / R, f = lerp(fTop, fBot, k), [rx, rz] = interpTable(T, f), hw = lerp(.084, .002, Math.pow(k, .8)), ph = Math.min(1.2, hw / rx);
      for (let j = 0; j <= Cn; j++) { const a = lerp(-ph, ph, j / Cn); pos.push((rx + lift) * Math.sin(a), (f - .45) * torsoL, (rz + lift) * Math.cos(a)); uv.push(j / Cn, 1 - k); }
    }
    for (let i = 0; i < R; i++) for (let j = 0; j < Cn; j++) { const a = i * (Cn + 1) + j, b = a + 1, c2 = a + Cn + 1, d = c2 + 1; idx.push(a, c2, b, b, c2, d); }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    g.setIndex(idx); g.computeVertexNormals();
    return g;
  }
  veeTex() {
    const N = 256, c = mkCanvas(N, N), g = c.getContext('2d'), S = this.S, D = this.D, mono = S.ink && S.ink.mode === 'mono';
    g.fillStyle = mono ? '#' + new THREE.Color(S.ink.paper).lerp(new THREE.Color(S.ink.ink), .1).getHexString() : celCss(S, D.skin); g.fillRect(0, 0, N, N);
    const sh = mono ? '#9a958c' : celCss(S, D.skinSh), ink = mono ? '#111111' : '#' + celColor(S, D.skinSh).lerp(new THREE.Color('#1a1417'), .55).getHexString();
    g.lineCap = 'round';
    // u 0..1 across the V (edge to edge), v 1 = top. Edges: thin ink line; inside: pit of the throat + clavicle ticks
    g.strokeStyle = ink; g.lineWidth = N * .03;
    for (const x of [0, 1]) { g.beginPath(); g.moveTo(x * N, 0); g.lineTo(x * N, N); g.stroke(); }
    g.strokeStyle = sh; g.lineWidth = N * .022;
    g.beginPath(); g.moveTo(.42 * N, .1 * N); g.quadraticCurveTo(.5 * N, .2 * N, .58 * N, .1 * N); g.stroke();
    for (const sd of [-1, 1]) { g.beginPath(); g.moveTo((.5 + sd * .16) * N, .17 * N); g.quadraticCurveTo((.5 + sd * .3) * N, .2 * N, (.5 + sd * .46) * N, .14 * N); g.stroke(); }
    g.lineWidth = N * .016; g.beginPath(); g.moveTo(.5 * N, .32 * N); g.lineTo(.5 * N, .5 * N); g.stroke();
    return canvasTex(c);
  }
  // clothes: base colour + a few fold-line decals only at the joints (bible 5-1)
  clothTex(kind) {
    const S = this.S, D = this.D, mono = S.ink && S.ink.mode === 'mono';
    const base = kind === 'pants' ? D.pants : kind === 'sleeveJ' ? D.jacket : D.inner;
    const lineC = mono ? '#111111' : '#' + celColor(S, base).lerp(new THREE.Color('#1a1417'), kind === 'shirt' || kind === 'sleeveS' || kind === 'hoodie' ? .45 : .55).getHexString();
    const c = mkCanvas(512, 512), g = c.getContext('2d');
    g.fillStyle = mono ? (kind === 'pants' || kind === 'sleeveJ' ? '#1a1a1a' : S.ink.paper) : celCss(S, base); g.fillRect(0, 0, 512, 512);
    g.strokeStyle = lineC; g.lineCap = 'round';
    const L = (pts, w) => { g.lineWidth = w; g.beginPath(); g.moveTo(pts[0][0] * 512, (1 - pts[0][1]) * 512); for (let i = 1; i < pts.length - 1; i++) { const m = [(pts[i][0] + pts[i + 1][0]) / 2, (pts[i][1] + pts[i + 1][1]) / 2]; g.quadraticCurveTo(pts[i][0] * 512, (1 - pts[i][1]) * 512, m[0] * 512, (1 - m[1]) * 512); } const e = pts[pts.length - 1]; g.lineTo(e[0] * 512, (1 - e[1]) * 512); g.stroke(); };
    if (kind === 'shirt') {
      // the skin V of the open collar is its own patch (veeGeo); the shirt only carries the placket and folds
      const T = D.torso, fb = T[0][0], ft = T[T.length - 1][0], vOf = f => (f - fb) / (ft - fb);
      const fBot = .835;
      // placket + 2 visible buttons below the open collar, tuck wrinkles at the waist, armpit pulls
      L([[.25, .08], [.25, vOf(fBot)]], 3);
      g.fillStyle = lineC; for (const v of [vOf(fBot) - .1, vOf(fBot) - .25]) { g.beginPath(); g.arc(.262 * 512, (1 - v) * 512, 4, 0, TAU); g.fill(); }
      for (const [u, v, d] of [[.12, .13, .06], [.2, .16, .05], [.33, .14, .06], [.4, .12, .05], [.62, .13, .05], [.7, .15, .06], [.85, .13, .05]]) L([[u - d, v], [u, v + .025], [u + d * .8, v - .01]], 2.4);
      for (const s of [.5, 1]) for (const k of [0, 1]) L([[s - .02 - k * .04, .74], [s - .06 - k * .05, .62 - k * .04]], 2.2);
    } else if (kind === 'hoodie') {
      for (const [u, v, d] of [[.15, .06, .05], [.3, .09, .06], [.38, .05, .04], [.65, .07, .05]]) L([[u - d, v], [u, v + .02], [u + d, v]], 2.6);
      for (const s of [0, .5]) for (const k of [0, 1]) L([[s + .02 + k * .035, .72], [s + .06 + k * .045, .6]], 2.4);
    } else if (kind === 'sleeveJ' || kind === 'sleeveS') {
      // elbow (v≈1 = bottom of the upper-arm tube): 2–3 zigzag folds; armpit at the top inner side
      for (const k of [0, 1, 2]) L([[.1 + k * .08, .9 - k * .03], [.2 + k * .08, .97], [.3 + k * .08, .9 - k * .02]], 3);
      for (const k of [0, 1]) L([[.55 + k * .06, .12], [.62 + k * .07, .2]], 2.6);
    } else if (kind === 'pants') {
      // shin tube: knee back (v≈0, u .75) and front crease, hem break folds near the bottom
      L([[.25, .05], [.25, .95]], 1.6);
      for (const k of [0, 1]) L([[.68 + k * .05, .05 + k * .03], [.75, .12 + k * .04], [.82 - k * .03, .06]], 2.6);
      for (const k of [0, 1]) L([[.12 + k * .4, .94], [.22 + k * .4, .9], [.3 + k * .4, .95]], 2.4);
    }
    const t = canvasTex(c); t.wrapS = THREE.RepeatWrapping; return t;
  }

  // ---- faces: lazy textures, LOD swap ----
  faceTex(e, far) { const k = e + (far ? ':far' : ''); return this.faces[k] || (this.faces[k] = stylishFaceTexture(this.def, this.S, e, far)); }
  applyFace() { if (!this.M || !this.M.face) return; const t = this.faceTex(this.expr || 'normal', this.faceFar); if (this.M.face.map !== t) { this.M.face.map = t; this.M.face.needsUpdate = true; } }
  setExpr(e, dur = 0) {
    this.expr = e; this.exprT = dur; this.applyFace();
    if (this.headMesh) {
      const g = sculptHeadGeo(this.def.id, this.hh, e || 'normal');
      if (this.headMesh.geometry !== g) {
        this.headMesh.geometry = g;
        if (this.headHull) { const h = this.kit.hull(sculptHeadGeo(this.def.id, this.hh, e || 'normal', 1), this.M.face, 1); if (h) this.headHull.geometry = h.geo; }
      }
    }
  }

  update(dt, rdt) {
    super.update(dt, rdt);
    const b = this.blend;
    const cs = this.curlDef.show, cf = this.clip ? 1 : this.curlDef.fight;
    const c = lerp(cs, cf, smooth01(.15, .85, b));
    this.hands.L.setCurl(this.pocketHand === 'L' && b < .5 && !this.override ? .6 : c); this.hands.R.setCurl(c);
    if (this.pocketQ && !this.clip && !this.override && b < .999) {
      const q = new THREE.Quaternion(), r = new THREE.Quaternion(), ha = this.J['ha' + this.pocketHand];
      ha.parent.updateWorldMatrix(true, false); ha.parent.getWorldQuaternion(q);
      this.root.getWorldQuaternion(r); r.multiply(this.pocketQ);
      ha.quaternion.slerp(q.invert().multiply(r), 1 - smooth01(0, .6, b));
    }
    if (this.handShowQ && !this.clip && !this.override && b < .999) {
      const q = new THREE.Quaternion(), r = new THREE.Quaternion(), ha = this.J.haR;
      ha.parent.updateWorldMatrix(true, false); ha.parent.getWorldQuaternion(q);
      this.root.getWorldQuaternion(r); r.multiply(this.handShowQ);
      ha.quaternion.slerp(q.invert().multiply(r), 1 - smooth01(0, .6, b));
    }
    if (this.cape) {
      // the draped blazer slides off backward over ~0.35 s when the fight starts (own timer), and is back on at once
      // in the show pose
      const t = this.capeT = (this.mode !== 'show' || this.override) ? Math.min(1, (this.capeT || 0) + dt / .35) : 0;
      this.cape.visible = t < 1;
      this.cape.position.set(0, -.6 * t * t, -.08 * t); this.cape.rotation.x = -.5 * t * t;
    }
    if (this.milk) {
      this.milk.visible = b < .5 && !this.override;
      if (this.milk.visible) {
        // keep the bottle upright in the world whatever the wrist does
        const q = new THREE.Quaternion(), r = new THREE.Quaternion();
        this.milk.parent.updateWorldMatrix(true, false); this.milk.parent.getWorldQuaternion(q);
        this.root.getWorldQuaternion(r); r.multiply(new THREE.Quaternion().setFromEuler(new THREE.Euler(.12, 0, -.08)));
        this.milk.quaternion.copy(q.invert().multiply(r));
      }
    }
  }

  dispose() {
    this._gone = true;
    const mats = new Set(), texs = new Set();
    this.root.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) [].concat(o.material).forEach(m => mats.add(m)); });
    mats.forEach(m => { if (m.map) texs.add(m.map); m.dispose(); });
    Object.values(this.faces).forEach(t => texs.add(t));
    texs.forEach(t => t.dispose());
    this.skel && this.skel.dispose();
    this.root.removeFromParent();
  }
}

const _hairGeoCache = new Map();
// one cel material per fighter for small accent parts (cached on the fighter)
function kitSolidOnce(f, hex, o = {}) {
  f._once = f._once || {};
  return f._once[hex] || (f._once[hex] = f.kit.solid(hex, Object.assign({ cel: true, role: 'spot', shade: [.72, .74, .8] }, o)));
}

function smooth01(a, b, x) { const t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
// table rows [key, v1, v2, ...] sorted by key → smooth-interpolated values at k
function interpTable(tbl, k) {
  if (k <= tbl[0][0]) return tbl[0].slice(1);
  for (let i = 0; i < tbl.length - 1; i++) {
    const a = tbl[i], b = tbl[i + 1];
    if (k <= b[0]) { const t = smooth01(a[0], b[0], k); return a.slice(1).map((v, j) => lerp(v, b[j + 1], t)); }
  }
  return tbl[tbl.length - 1].slice(1);
}
// skin weights from fn(x,y,z) → [[boneIndex, weight] x ≤4]
function skinWeights(geo, fn) {
  const p = geo.attributes.position, n = p.count, si = new Uint16Array(n * 4), sw = new Float32Array(n * 4);
  for (let i = 0; i < n; i++) {
    const w = fn(p.getX(i), p.getY(i), p.getZ(i)); let tot = 0;
    for (let k = 0; k < 4; k++) { const e = w[k]; if (e) { si[i * 4 + k] = e[0]; sw[i * 4 + k] = e[1]; tot += e[1]; } }
    if (tot < 1e-6) { si[i * 4] = 0; sw[i * 4] = 1; tot = 1; }
    for (let k = 0; k < 4; k++) sw[i * 4 + k] /= tot;
  }
  geo.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(si, 4));
  geo.setAttribute('skinWeight', new THREE.Float32BufferAttribute(sw, 4));
}
