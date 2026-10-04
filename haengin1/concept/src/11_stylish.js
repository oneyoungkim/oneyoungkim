// ---------- stylish fighters (A 스타일리시 셀 / A′ 잉크 망가) ----------
// Long proportions, oversized open jacket (skinned to hips/spine/chest/shoulders), pointed hair locks,
// sharp texture faces, big jointed hands, show poses held by IK.
const STYLISH_DEF = {
  siwoo: {
    heads: 7.6, legF: .54, footH: .066, sh: .165, hip: .086, upperL: .29, foreL: .255, hand: .178, neckR: .035,
    skin: '#c58b63', hair: '#17141c', jacket: '#252c4f', lining: '#1c2140', inner: '#9c9ea6', pants: '#2c2e36',
    shoe: '#f3f1ea', sole: '#e8572a', tape: '#f4efe2',
    torso: [[-.07, .118, .084], [0, .124, .088], [.15, .128, .086], [.3, .142, .098], [.4, .158, .098], [.455, .12, .07], [.48, .05, .045]],
    coat: { hem: .14, depth: .64, flare: .1, w: [[-.14, .178], [0, .182], [.12, .172], [.25, .186], [.33, .2], [.4, .212], [.43, .214], [.452, .198], [.468, .158], [.48, .112], [.49, .064]] },
    pantsR: [.088, .077, .075, .08], seat: [.142, .096], shoeKind: 'hightop', shoeLen: .27, shoeW: .1, shoeH: .075,
    sleeve: [.066, .057], cuff: .062, fore: [.031, .025],
  },
  taeo: {
    heads: 8.4, legF: .555, footH: .07, sh: .215, hip: .1, upperL: .32, foreL: .28, hand: .195, neckR: .045,
    skin: '#e7bd97', hair: '#121318', hairSide: '#3a3d47', jacket: '#252c4f', lining: '#1d5bd0', inner: '#f6f6f2', pants: '#2c2e36',
    tie: '#1f2747', belt: '#141418', shoe: '#15161a', sole: '#0e0e11',
    torso: [[.04, .138, .092], [.09, .146, .098], [.16, .162, .106], [.33, .19, .124], [.44, .205, .124], [.5, .15, .085], [.53, .062, .058]],
    coat: { hem: .13, depth: .66, flare: .1, w: [[-.13, .212], [0, .214], [.14, .2], [.3, .228], [.4, .246], [.47, .254], [.5, .244], [.52, .208], [.538, .148], [.555, .078]] },
    pantsR: [.1, .088, .084, .09], seat: [.158, .104], shoeKind: 'loafer', shoeLen: .29, shoeW: .1, shoeH: .062,
    sleeve: [.072, .064], cuff: .068, fore: [.046, .032],
  },
};

// poses (same format as STANCE / SHOW). ik = show-pose IK targets in root space, filled in by StylishFighter.
const STYLISH_POSES = {
  siwoo: {
    // 양키 스쿼트: deep squat, knees wide, forearms over the knees, chin up, glaring
    show: {
      hips: [-.1, 0, 0], spine: [.4, 0, 0], chest: [.24, .05, 0], neck: [-.34, -.05, 0], head: [-.42, .14, .2],
      uaL: [-.9, 0, .4], faL: [-.9, 0, 0], haL: [.55, 0, .1], uaR: [-.9, 0, -.4], faR: [-.9, 0, 0], haR: [.55, 0, -.1],
      thL: [-1.9, -.2, .5], shL: [2.5, 0, 0], ftL: [-.65, .3, 0], thR: [-1.9, .2, -.5], shR: [2.5, 0, 0], ftR: [-.65, -.3, 0],
      drop: .65,
    },
    // low, quick high guard, chin tucked
    fight: {
      hips: [0, -.48, 0], spine: [.08, .08, 0], chest: [.12, .12, 0], neck: [.06, .08, 0], head: [.24, .2, 0],
      uaL: [-.98, .2, .4], faL: [-2.25, 0, 0], haL: [.12, 0, 0],
      uaR: [-.72, -.14, -.38], faR: [-2.4, 0, 0], haR: [.18, 0, 0],
      thL: [-.42, .3, .08], shL: [.46, 0, 0], ftL: [.0, .2, 0],
      thR: [.32, .25, -.14], shR: [.48, 0, 0], ftR: [-.24, .35, 0],
      drop: .1,
    },
    curl: { show: .3, fight: 1 },
  },
  taeo: {
    // 콘트라포스토: weight on the right leg, left hand in the pocket, banana milk at the chest, head tilted
    show: {
      hips: [0, .12, -.07], spine: [-.02, -.04, .03], chest: [-.05, -.1, .07], neck: [.02, -.04, -.02], head: [-.02, -.12, .13],
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
    const def = this.def, S = this.S, D = STYLISH_DEF[def.id];
    const kit = inkKit(S); this.kit = kit; this.D = D;
    const V = (x, y, z) => new THREE.Vector3(x, y, z);
    const H = def.H, hh = H / ((S.heads && S.heads[def.id]) || D.heads), headR = hh * .5;
    const legL = H * D.legF, footH = D.footH, thighL = (legL - footH) / 2, shinL = thighL;
    const neckL = hh * .55, headTop = hh * .78, torsoL = H - legL - neckL - headTop;
    const sh = D.sh, hip = D.hip, upperL = D.upperL, foreL = D.foreL, handR = D.hand / 2;
    Object.assign(this, { hh, headR, neckL, torsoL, legL, footH, sh, hip, handR, upperL, foreL, thighL, shinL });
    const P = STYLISH_POSES[def.id];
    this.showPoseDef = P.show; this.fightPoseDef = P.fight; this.curlDef = P.curl;

    // ---- materials ----
    const mono = kit.mono;
    const M = {
      skin: kit.solid(D.skin, { role: 'skin' }),
      hair: kit.solid(D.hair, { role: 'dark', hi: .8, rimM: .45, keyline: false, fold: .0022, foldF: 170 }),
      side: kit.solid(D.hairSide || D.hair, { role: D.hairSide ? 'mid' : 'dark', mix: .55 }),
      jacket: kit.solid(D.jacket, { role: 'dark', fold: .011, foldF: 34, hi: .56 }),
      lapel: kit.solid('#' + new THREE.Color(D.jacket).lerp(new THREE.Color('#8790b8'), .16).getHexString(), { role: 'dark', hi: .66 }),
      lining: kit.solid(D.lining, { role: def.id === 'taeo' ? 'spot' : 'dark' }),
      inner: kit.solid(D.inner, { role: def.id === 'siwoo' ? 'mid' : 'light', fold: .007, foldF: 42 }),
      pants: kit.solid(D.pants, { role: 'dark', fold: .011, foldF: 28, hi: .6 }),
      shoe: kit.solid(D.shoe, { role: def.id === 'siwoo' ? 'light' : 'dark', gloss: def.id === 'taeo' ? 1 : 0 }),
      sole: kit.solid(D.sole, { role: def.id === 'siwoo' ? 'spot' : 'dark' }),
      tape: kit.solid(D.tape || '#ffffff', { role: 'light' }),
    };
    if (D.tie) { M.tie = kit.solid(D.tie, { role: 'dark' }); M.belt = kit.solid(D.belt, { role: 'dark', gloss: .5 }); M.buckle = kit.solid('#cfcabd', { role: 'light' }); }
    this.faces = {}; for (const e of ['normal', 'hurt', 'ko', 'win']) this.faces[e] = stylishFaceTexture(def, S, e);
    M.face = kit.solid('#ffffff', { map: this.faces.normal, role: 'face' });
    this.M = M;
    for (const k in M) if (M[k].emissive) { M[k].emissive.set(0xffffff); M[k].emissiveIntensity = 0; kit.flashables.push(M[k]); }

    // ---- joints ----
    const J = {}; this.J = J;
    const root = new THREE.Group(); root.rotation.order = 'YXZ'; this.root = root; scene.add(root);
    const tilt = new THREE.Group(), off = new THREE.Group(); root.add(tilt); tilt.add(off); this.tilt = tilt; this.off = off;
    const G = (name, parent, y = 0, x = 0, z = 0) => { const g = new THREE.Group(); g.name = name; g.position.set(x, y, z); parent.add(g); J[name] = g; return g; };
    G('hips', off, legL); G('spine', J.hips, 0); G('chest', J.spine, torsoL * .45); G('neck', J.chest, torsoL * .56); G('head', J.neck, neckL);
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R';
      G('ua' + L, J.chest, torsoL * .47, s * sh, 0); G('fa' + L, J['ua' + L], -upperL); G('ha' + L, J['fa' + L], -foreL);
      G('th' + L, J.hips, -hip * .1, s * hip, 0); G('sh' + L, J['th' + L], -thighL); G('ft' + L, J['sh' + L], -shinL);
    }
    root.updateMatrixWorld(true);
    const bones = ['hips', 'spine', 'chest', 'uaL', 'uaR'].map(n => J[n]);
    this.skel = new THREE.Skeleton(bones);
    const part = (p, geo, mat, o = {}) => kit.part(p, geo, mat, Object.assign({ receive: true }, o));
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
      const wh = 1 - smooth01(-.03, .09, yr), wc = smooth01(torsoL * .28, torsoL * .62, yr);
      let wua = smooth01(sh * .72, sh * 1.25, Math.abs(x)) * smooth01(torsoL * .62, torsoL * .86, yr) * .55;
      const ws = Math.max(0, 1 - wh - wc);
      const wcc = wc * (1 - wua); wua *= wc;
      return [[0, wh], [1, ws], [2, wcc], [x > 0 ? 3 : 4, wua]];
    };

    // ---- torso (hoodie / shirt) ----
    const tz = (tbl, yr) => interpTable(tbl, yr);
    const yb = D.torso[0][0], yt = D.torso[D.torso.length - 1][0];
    const torsoGeo = taperTube([V(0, legL + yb, 0), V(0, legL + yt, 0)], t => { const r = tz(D.torso, lerp(yb, yt, t)); return [r[0], r[1]]; }, { radial: 24, tubular: 20, capLen: .3 });
    skinned(torsoGeo, M.inner, 1, torsoW);

    // ---- jacket: open front, dropped shoulders, hem below the seat ----
    const JK = D.coat, jy0 = legL - JK.hem, jy1 = legL + JK.w[JK.w.length - 1][0];
    const yf = yr => (legL + yr - jy0) / (jy1 - jy0);
    const ctrl = JK.w.map(([yr, w]) => [yf(yr), w]);
    const openAt = f => { const yr = lerp(jy0, jy1, f) - legL; return yr < .2 ? lerp(.95, .62, smooth01(-JK.hem, .2, yr)) : yr < torsoL * .9 ? lerp(.62, 1.5, smooth01(.2, torsoL * .9, yr)) : lerp(1.5, 2.5, smooth01(torsoL * .9, torsoL * 1.06, yr)); };
    const jg = jacketGeo({ ctrl, y0: jy0, y1: jy1, open: openAt, depth: JK.depth, flare: JK.flare, thick: .01, rows: 34, cols: 40, front: .92, back: 1.05 });
    this.jacket = skinned(jg, [M.jacket, M.lining], 1, torsoW);
    // lapels on the chest (rigid with the ribcage): roll line follows the jacket's front edge
    const jEdge = yr => { const f = yf(yr), ha = openAt(f) / 2, w = interpTable(JK.w, yr)[0]; return new THREE.Vector3(w * Math.sin(ha), yr - torsoL * .45, w * JK.depth * .92 * Math.cos(ha)); };
    const jNorm = yr => { const f = yf(yr), ha = openAt(f) / 2 + .25, w = interpTable(JK.w, yr)[0]; return new THREE.Vector3(Math.sin(ha) / w, 0, Math.cos(ha) / (w * JK.depth * .92)).normalize(); };
    const plate = (geoFn, a, b, mat, lift, nrm) => {
      for (const s of [-1, 1]) {
        const mir = new THREE.Group(); if (s < 0) mir.scale.x = -1; J.chest.add(mir);
        const Y = a.clone().sub(b).normalize();
        const Z = nrm.clone().normalize(); Z.addScaledVector(Y, -Z.dot(Y)).normalize();
        const X = new THREE.Vector3().crossVectors(Y, Z);
        const g = new THREE.Group(); g.position.copy(a).addScaledVector(Z, lift); g.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(X, Y, Z)); mir.add(g);
        part(g, geoFn(a.distanceTo(b)), mat, { ow: .7 });
      }
    };
    const lapTop = jEdge(torsoL * .975), lapBrk = jEdge(torsoL * (def.id === 'taeo' ? .3 : .38));
    plate(len => lapelGeo({ len, width: hh * (def.id === 'taeo' ? .46 : .38), notch: hh * .1, peak: .3 }), lapTop, lapBrk, M.lapel, .008, jNorm(torsoL * .7));

    // ---- neck & head ----
    part(J.neck, taperTube([V(0, -.03, -.006), V(0, neckL * .55, .0), V(0, neckL + headR * .22, -.022)], t => lerp(D.neckR * 1.05, D.neckR * .8, t), { radial: 16, tubular: 6 }), M.skin);
    this.buildHead(J.head, headR, M, part, kit);

    // ---- inner details: hood (Siwoo) / collar + loose tie (Taeo) ----
    const topY = torsoL * .56; // neck joint in chest space
    if (def.id === 'siwoo') {
      const hood = taperTube([V(.075, topY - .05, .05), V(.092, topY - .01, -.02), V(.05, topY + .012, -.085), V(0, topY + .016, -.1), V(-.05, topY + .012, -.085), V(-.092, topY - .01, -.02), V(-.075, topY - .05, .05)],
        t => [.024 + .018 * Math.sin(t * Math.PI), .03 + .014 * Math.sin(t * Math.PI)], { radial: 12, tubular: 30 });
      part(J.chest, hood, M.inner);
      const bag = taperTube([V(0, topY + .005, -.1), V(0, topY - .06, -.135), V(0, topY - .13, -.135)], t => [.085 * (1 - .35 * t), .03 * (1 - .3 * t)], { radial: 14, tubular: 8, up: V(0, 0, 1) });
      part(J.chest, bag, M.inner);
      for (const s of [-1, 1]) {
        const cord = taperTube([V(s * .03, topY - .045, .085), V(s * .034, topY - .1, .094), V(s * .03, topY - .16, .097)], .0035, { radial: 6, tubular: 6 });
        part(J.chest, cord, M.tape, { ow: .5 });
      }
    } else {
      // open collar points + skin V + loosened tie
      const collar = jacketGeo({ ctrl: [[0, D.neckR + .012], [1, D.neckR + .016]], y0: topY - .03, y1: topY + .03, open: 1.15, depth: 1, thick: .004, rows: 3, cols: 24 });
      part(J.chest, collar, [M.inner, M.inner], { pos: [0, 0, .008], ow: .6 });
      plate(len => lapelGeo({ len, width: .056, notch: .001, peak: .85 }), new THREE.Vector3(.036, topY + .02, .05), new THREE.Vector3(.062, topY - .055, .1), M.inner, .004, new THREE.Vector3(.5, .3, .85));
      const vee = new THREE.Shape(); vee.moveTo(-.04, 0); vee.lineTo(.04, 0); vee.lineTo(0, -.085); vee.lineTo(-.04, 0);
      const vg = new THREE.ShapeGeometry(vee); part(J.chest, vg, M.skin, { pos: [0, topY - .012, .1], rot: [-.25, 0, 0], outline: false });
      const knot = taperTube([V(0, topY - .085, .118), V(0, topY - .12, .122)], t => [.017 - .004 * t, .01], { radial: 10, tubular: 3, capLen: .5 });
      part(J.chest, knot, M.tie, { ow: .7 });
      const blade = taperTube([V(0, topY - .115, .121), V(.004, topY - .2, .126), V(-.006, topY - .3, .124), V(-.004, topY - .37, .118)], t => [.018 + .012 * t, .0045], { radial: 8, tubular: 10, capLen: .3 });
      part(J.chest, blade, M.tie, { ow: .6 });
    }

    // ---- arms ----
    this.hands = {};
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R', ua = J['ua' + L], fa = J['fa' + L], ha = J['ha' + L];
      part(ua, taperTube([V(s * .006, -.008, 0), V(0, -upperL * .5, 0), V(0, -upperL - .012, 0)], t => [lerp(D.sleeve[0] * .96, D.sleeve[1], t), lerp(D.sleeve[0], D.sleeve[1], t) * .94], { radial: 16, tubular: 8, capLen: .55 }), M.jacket);
      if (def.id === 'siwoo') {
        part(fa, taperTube([V(0, .03, 0), V(0, -foreL * .44, 0)], t => D.cuff * (1 - .1 * t) * (1 + .07 * Math.sin(t * Math.PI * 4.5)), { radial: 16, tubular: 16, capLen: .5 }), M.jacket);
        part(fa, taperTube([V(0, -foreL * .3, 0), V(0, -foreL + .004, 0)], t => lerp(D.fore[0], D.fore[1], t), { radial: 12, tubular: 6 }), M.skin);
      } else {
        part(fa, taperTube([V(0, .045, 0), V(0, -foreL * .14, 0)], t => D.cuff * (1 + .06 * Math.sin(t * Math.PI * 3.5)), { radial: 16, tubular: 10, capLen: .5 }), M.jacket);
        part(fa, taperTube([V(0, -foreL * .08, 0), V(0, -foreL * .25, 0)], t => .058 * (1 + .05 * Math.sin(t * Math.PI * 2)), { radial: 16, tubular: 6, capLen: .4 }), M.inner);
        part(fa, taperTube([V(0, -foreL * .12, 0), V(0, -foreL * .6, .004), V(0, -foreL + .004, 0)], t => lerp(D.fore[0], D.fore[1], t) * (1 + .1 * Math.sin(t * Math.PI)), { radial: 12, tubular: 8 }), M.skin);
      }
      this.hands[L] = buildHand(kit, ha, { size: D.hand, side: s, curl: P.curl.show, skinMat: M.skin, tapeMat: def.id === 'siwoo' ? M.tape : null, thick: def.id === 'taeo' ? 1.12 : 1 });
    }

    // ---- legs: wide slacks breaking over the shoes ----
    const PR = D.pantsR;
    part(J.hips, taperTube([V(0, .115, .004), V(0, .02, -.004), V(0, -.07, -.002)], t => [lerp(D.seat[0], D.seat[0] * .9, t), lerp(D.seat[1], D.seat[1] * 1.04, t)], { radial: 20, tubular: 8, capLen: .45 }), M.pants);
    if (M.belt) {
      const belt = new THREE.TorusGeometry(1, .14, 8, 36); belt.rotateX(Math.PI / 2);
      part(J.hips, belt, M.belt, { pos: [0, .1, 0], scl: [D.seat[0] * .93, .1, D.seat[1] * 1.08], ow: .6 });
      part(J.hips, new THREE.BoxGeometry(.04, .03, .012), M.buckle, { pos: [0, .1, D.seat[1] * 1.08 + .004], ow: .6 });
    }
    for (const s of [-1, 1]) {
      const L = s > 0 ? 'L' : 'R', th = J['th' + L], sn = J['sh' + L], ft = J['ft' + L];
      part(th, taperTube([V(0, .05, 0), V(0, -thighL * .5, 0), V(0, -thighL - .01, .004)], t => [lerp(PR[0], PR[1], t), lerp(PR[0], PR[1], t) * .96], { radial: 18, tubular: 8 }), M.pants);
      part(sn, taperTube([V(0, .02, .004), V(0, -shinL * .5, 0), V(0, -shinL + .012, 0)], t => [lerp(PR[2], PR[3], t * t), lerp(PR[2], PR[3], t * t) * 1.02], { radial: 18, tubular: 10 }), M.pants);
      // hem break: a soft fold ring sitting on the shoe
      part(sn, taperTube([V(0, -shinL + .05, 0), V(0, -shinL + .005, .006)], t => [PR[3] * (1.02 + .1 * t), PR[3] * (1.05 + .12 * t)], { radial: 18, tubular: 4, capLen: .3 }), M.pants, { ow: .9 });
      const shoe = shoeGeo({ kind: D.shoeKind, len: D.shoeLen, width: D.shoeW, height: D.shoeH });
      part(ft, shoe.upper, M.shoe, { pos: [0, -footH, .01] });
      part(ft, shoe.sole, M.sole, { pos: [0, -footH, .01], ow: .8 });
    }

    // ---- props ----
    if (def.id === 'taeo') {
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
    if (def.id === 'siwoo') {
      this.ikTargets = {
        FL: { foot: [.15, footH, .13], pole: [.75, 1.5, 1.3], flat: true, yaw: .42 },
        FR: { foot: [-.15, footH, .13], pole: [-.75, 1.5, 1.3], flat: true, yaw: -.42 },
        L: { hand: [.2, .4, .52], pole: [.75, .7, .32] },
        R: { hand: [-.2, .4, .52], pole: [-.75, .7, .32] },
      };
    } else {
      this.ikTargets = {
        L: { hand: [.205, legL - .04, .05], pole: [.7, legL + .25, -.5], hide: true },
        R: { hand: [-.03, legL + torsoL * .62, .24], pole: [-.7, legL + .1, -.25] },
        FR: { foot: [-.07, footH, -.01], pole: [-.2, .5, 1], flat: true, yaw: -.18 },
      };
    }
  }

  buildHead(head, R, M, part, kit) {
    const def = this.def, F = FACE_LAYOUT, D2R = Math.PI / 180;
    const c = new THREE.Vector3(0, R * .56, R * .08); this.headC = c;
    const geo = new THREE.SphereGeometry(R, 56, 40), p = geo.attributes.position;
    const JW = def.id === 'taeo' ? { w: .8, jaw: .3, pow: 1.9, chin: .17 } : { w: .76, jaw: .44, pow: 1.35, chin: .22 };
    const sq = (a, b, x) => smooth01(a, b, x);
    for (let i = 0; i < p.count; i++) {
      let x = p.getX(i) / R, y = p.getY(i) / R, z = p.getZ(i) / R;
      const lat = Math.asin(clamp(-y, -1, 1)) / D2R, lon = Math.atan2(x, z) / D2R; // lat: + below the equator (texture v)
      const t = Math.max(0, -y), front = Math.max(0, z);
      x *= JW.w * (1 - JW.jaw * Math.pow(t, JW.pow)) * (1 - .06 * front * sq(10, 40, lat));
      z *= .9;
      if (y < 0) { y *= 1 + JW.chin * t * (.45 + .55 * front); z += .07 * t * front; }
      // nose ridge + tip
      const nose = Math.exp(-Math.pow(lon / 5.5, 2)) * sq(F.noseV - 14, F.noseV - 1, lat) * (1 - sq(F.noseV, F.noseV + 4, lat));
      z += .1 * nose;
      // brow ridge, cheekbones
      z += .025 * Math.exp(-Math.pow((lat - F.browV) / 5, 2)) * Math.exp(-Math.pow((Math.abs(lon) - F.eyeU) / 14, 2));
      z -= .02 * Math.exp(-Math.pow((lat - F.eyeV) / 6, 2)) * Math.exp(-Math.pow((Math.abs(lon) - F.eyeU) / 9, 2));
      p.setXYZ(i, x * R, y * R, z * R);
    }
    geo.computeVertexNormals();
    this.headMesh = part(head, geo, M.face, { pos: [c.x, c.y, c.z], receive: false });
    // ears
    for (const s of [-1, 1]) {
      const cauli = def.id === 'taeo' && s === 1;
      const ear = part(head, new THREE.SphereGeometry(R * .2, 12, 10), M.skin, { pos: [c.x + s * R * .73, c.y - R * .12, c.z - R * .1], scl: cauli ? [.62, 1.05, .82] : [.38, 1.0, .66], rot: [0, s * .25, 0], ow: .7 });
      if (cauli) for (const b of [[.25, .3, .2], [.2, -.25, .3]]) part(ear, new THREE.SphereGeometry(R * .075, 8, 6), M.skin, { pos: [b[0] * R * .2, b[1] * R * .2, b[2] * R * .2], outline: false });
    }
    if (def.id === 'siwoo') this.hairSiwoo(head, R, c, M, part);
    else this.hairTaeo(head, R, c, M, part);
  }

  // point on the skull (head-local). lat: degrees above the equator, lon: 0 = front, + toward the character's left (+X)
  skull(lat, lon, lift = 0, sc = 1) {
    const R = this.headR * sc, c = this.headC, la = lat * Math.PI / 180, lo = lon * Math.PI / 180;
    const p = new THREE.Vector3(R * .76 * Math.cos(la) * Math.sin(lo), R * Math.sin(la), R * .9 * Math.cos(la) * Math.cos(lo)).add(c);
    const n = new THREE.Vector3(Math.cos(la) * Math.sin(lo) / .76, Math.sin(la), Math.cos(la) * Math.cos(lo) / .9).normalize();
    return { p: p.addScaledVector(n, lift), n };
  }
  // hair lock rooted on the skull, flowing along `flow` (head-local, projected onto the scalp), +Z of the lock = outward
  lock(parent, mat, part, lat, lon, o) {
    const { p, n } = this.skull(lat, lon, o.lift ?? .006, o.sc ?? 1.04);
    const f = new THREE.Vector3(...o.flow).normalize();
    f.addScaledVector(n, -f.dot(n) * (o.hug ?? 1)).normalize();
    const y = f.clone().negate(), z = n.clone().addScaledVector(y, -n.dot(y)).normalize();
    if (o.roll) z.applyAxisAngle(y, o.roll);
    const x = new THREE.Vector3().crossVectors(y, z);
    const g = hairLock({ len: o.len, width: o.w, thick: o.t ?? o.w * .38, bend: o.bend ?? -.6, flip: o.flip ?? 0, twist: o.twist ?? 0, taper: o.taper ?? .9, root: o.root ?? .8 });
    const m = part(parent, g, mat, { ow: o.ow ?? .6, receive: false });
    m.position.copy(p); m.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(x, y, z));
    return m;
  }

  hairSiwoo(head, R, c, M, part) {
    const H = M.hair, L = (...a) => this.lock(head, H, part, ...a);
    // scalp cap (top + back/sides, open over the face)
    const cap = new THREE.SphereGeometry(R * 1.05, 32, 16, 0, TAU, 0, Math.PI * .34);
    part(head, cap, H, { pos: [c.x, c.y, c.z], scl: [.79, 1, .93], receive: false });
    const back = new THREE.SphereGeometry(R * 1.04, 32, 16, Math.PI / 2 + 1.25, TAU - 2.5, Math.PI * .3, Math.PI * .42);
    part(head, back, H, { pos: [c.x, c.y, c.z], scl: [.8, 1, .94], receive: false });
    const rnd = mulberry(7);
    // crown: layered locks flowing outward from the whorl
    for (let i = 0; i < 14; i++) {
      const lon = -180 + i * (360 / 14) + rnd() * 10, lat = 62 + rnd() * 10;
      const fl = new THREE.Vector3(Math.sin(lon * Math.PI / 180), -.25, Math.cos(lon * Math.PI / 180));
      L(lat, lon, { flow: [fl.x, fl.y, fl.z], len: R * (.95 + rnd() * .35), w: R * .44, t: R * .17, bend: -1.15, flip: .8, lift: .006 });
    }
    // back + nape: long layered locks, ends flicking out (wolf / mullet)
    for (let i = 0; i < 11; i++) {
      const lon = 118 + i * (124 / 10) + (rnd() - .5) * 6, lat = 30 + (i % 2) * 14 + rnd() * 6;
      L(lat, lon, { flow: [0, -1, -.15], len: R * (1.25 + rnd() * .4 + (i % 2 ? 0 : .25)), w: R * .42, t: R * .15, bend: -.6, flip: 1.05, lift: .006, ow: .55 });
    }
    // sides: over the ears, tips out
    for (const s of [-1, 1]) for (let k = 0; k < 3; k++) {
      L(36 - k * 4, s * (78 + k * 16), { flow: [s * .1, -1, -.2 - k * .1], len: R * (1.0 + k * .15), w: R * .4, t: R * .14, bend: -.5, flip: .75, lift: .006 });
    }
    // fringe: falls forward; the long lock covers the right eye (-X)
    const fr = [[-26, 1.45, .44, -.3], [-12, 1.3, .4, -.15], [3, 1.05, .36, .05], [17, .95, .34, .2], [31, .8, .3, .35]];
    for (const [lon, len, w, sway] of fr) {
      L(50, lon, { flow: [sway, -1, .12], len: R * len, w: R * w, t: R * .14, bend: -.25, flip: .35, hug: .35, lift: .012, roll: -sway * .3, ow: .55 });
    }
    L(56, -36, { flow: [-.15, -1, .15], len: R * 1.25, w: R * .36, t: R * .13, bend: -.2, flip: .3, hug: .35, lift: .012, ow: .55 });
  }

  hairTaeo(head, R, c, M, part) {
    const H = M.hair, L = (...a) => this.lock(head, H, part, ...a);
    // two-block undercut: short grey-black sides/back
    const under = new THREE.SphereGeometry(R * 1.015, 32, 18, Math.PI / 2 + 1.15, TAU - 2.3, Math.PI * .2, Math.PI * .52);
    part(head, under, M.side, { pos: [c.x, c.y, c.z], scl: [.78, 1, .925], receive: false, ow: .7 });
    // top: slicked-back mass with a little lift at the hairline
    const top = new THREE.SphereGeometry(R * 1.08, 32, 14, 0, TAU, 0, Math.PI * .31);
    part(head, top, H, { pos: [c.x, c.y + R * .03, c.z - R * .02], scl: [.82, 1.08, .97], receive: false });
    const rnd = mulberry(3);
    // combed-back streaks from the hairline toward the bun
    for (let i = 0; i < 9; i++) {
      const lon = -44 + i * 11 + (rnd() - .5) * 4;
      L(50, lon, { flow: [-Math.sin(lon * Math.PI / 180) * .2, .45, -1], len: R * (1.35 + rnd() * .2), w: R * .44, t: R * .16, bend: -1.25, flip: .25, lift: .012, sc: 1.07, ow: .5 });
    }
    // high half-up bun on the back of the crown (shows above the head from the front)
    const bun = new THREE.Group(); const bp = this.skull(58, 180, 0, 1.06);
    bun.position.copy(bp.p).addScaledVector(bp.n, R * .2).add(new THREE.Vector3(0, R * .08, 0));
    head.add(bun); bun.rotation.x = -.5;
    part(bun, new THREE.SphereGeometry(R * .31, 18, 14), H, { scl: [1.05, .95, 1], receive: false });
    for (let k = 0; k < 5; k++) {
      // wrapped strands around the knot
      const a0 = k / 5 * TAU;
      const pts = []; for (let j = 0; j <= 6; j++) { const a = a0 + j * .55; pts.push(new THREE.Vector3(Math.cos(a) * R * .3, Math.sin(a) * R * .27, (j - 3) * R * .035)); }
      part(bun, taperTube(pts, t => [R * .1 * Math.sin(Math.PI * (.15 + .85 * t)), R * .07], { radial: 8, tubular: 14 }), H, { receive: false, ow: .45 });
    }
    // gathered hair from the nape/sides up into the bun
    for (const s of [-1, 1]) L(30, s * 140, { flow: [-s * .3, 1, -.4], len: R * .8, w: R * .4, t: R * .12, bend: -.7, lift: .01, sc: 1.05, ow: .45 });
    // two loose strands falling onto the forehead (S-curve)
    L(51, -5, { flow: [-.12, -1, .1], len: R * 1.02, w: R * .075, t: R * .035, bend: -.2, flip: 1.0, hug: .3, lift: .016, twist: 1.6, taper: .5, root: 1, ow: .35 });
    L(51, 7, { flow: [.18, -1, .1], len: R * .82, w: R * .065, t: R * .03, bend: -.15, flip: .9, hug: .3, lift: .016, twist: -1.4, taper: .5, root: 1, ow: .35 });
    // raised hairline: short locks lifting up and back off the forehead
    for (const [lon, l] of [[-30, .8], [-15, .9], [18, .85], [33, .75]]) L(47, lon, { flow: [-Math.sin(lon * Math.PI / 180) * .3, 1, -.35], len: R * l, w: R * .42, t: R * .16, bend: -1.4, flip: .3, lift: .014, sc: 1.07, ow: .5 });
  }

  update(dt, rdt) {
    super.update(dt, rdt);
    const b = this.blend;
    const cs = this.curlDef.show, cf = this.clip ? 1 : this.curlDef.fight;
    const c = lerp(cs, cf, smooth01(.15, .85, b));
    this.hands.L.setCurl(c); this.hands.R.setCurl(c);
    if (this.handShowQ && !this.clip && !this.override && b < .999) {
      const q = new THREE.Quaternion(), r = new THREE.Quaternion(), ha = this.J.haR;
      ha.parent.updateWorldMatrix(true, false); ha.parent.getWorldQuaternion(q);
      this.root.getWorldQuaternion(r); r.multiply(this.handShowQ);
      ha.quaternion.slerp(q.invert().multiply(r), 1 - smooth01(0, .6, b));
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
    const mats = new Set();
    this.root.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) [].concat(o.material).forEach(m => mats.add(m)); });
    mats.forEach(m => m.dispose());
    Object.values(this.faces).forEach(t => t.dispose());
    this.skel && this.skel.dispose();
    this.root.removeFromParent();
  }
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
