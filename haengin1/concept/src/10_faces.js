// ---------- stylish faces (equirect 1024×512 on the head sphere; face front at u=.25, equator v=0, +down) ----------
// Degrees. The head mesh in 11_stylish.js places the nose ridge / chin from these numbers.
const FACE_LAYOUT = { eyeU: 23, eyeV: 5, eyeW: 10.5, browV: -6, noseV: 21, mouthV: 37, chinV: 62 };

function stylishFaceTexture(def, S, expr = 'normal') {
  const W = 1024, H = 512, c = mkCanvas(W, H), g = c.getContext('2d');
  const F = FACE_LAYOUT, mono = S.ink && S.ink.mode === 'mono';
  const D = W / 360;
  const X = u => W * (.25 + u / 360), Y = v => H * (.5 + v / 180);
  // equirect squeeze: a mark drawn at latitude v gets wider in u by 1/cos(v)
  const sx = v => 1 / Math.cos(v * Math.PI / 180);
  const inkC = mono ? S.ink.ink : '#1a1520';
  const paper = mono ? S.ink.paper : '#fbf7f0';
  const skin = mono ? S.ink.paper : css(S, def.skin);
  const skinDark = mono ? S.ink.ink : shade(S, def.skin, .62);
  const isSiwoo = def.id === 'siwoo';
  g.fillStyle = skin; g.fillRect(0, 0, W, H);
  g.lineCap = 'round'; g.lineJoin = 'round';
  const P = (u, v) => [X(u), Y(v)];
  const stroke = (w, col = inkC) => { g.lineWidth = w; g.strokeStyle = col; g.stroke(); };
  // tapered brush stroke along a quadratic: width w0 → w1
  const brush = (a, ctl, b, w0, w1, col = inkC, n = 14) => {
    g.fillStyle = col;
    for (let i = 0; i <= n; i++) {
      const t = i / n, it = 1 - t;
      const x = it * it * a[0] + 2 * it * t * ctl[0] + t * t * b[0], y = it * it * a[1] + 2 * it * t * ctl[1] + t * t * b[1];
      g.beginPath(); g.arc(x, y, Math.max(.4, lerp(w0, w1, t) / 2), 0, TAU); g.fill();
    }
  };

  // ---- cheeks: a few manga hatch strokes (Taeo smiling / hurt) ----
  if (expr === 'hurt' || (!isSiwoo && expr !== 'ko')) {
    g.strokeStyle = mono ? inkC : 'rgba(205,90,80,.55)'; g.lineWidth = 2;
    for (const s of [-1, 1]) for (let k = 0; k < 3; k++) {
      const u = s * (F.eyeU + 1 + k * 2.6), v = F.eyeV + 11;
      g.beginPath(); g.moveTo(X(u + 1.6), Y(v - 1.4)); g.lineTo(X(u - 1.2), Y(v + 2)); g.stroke();
    }
  }

  // ---- nose: one bridge line on the shadow side + tip tick ----
  const nv = F.noseV;
  brush(P(-1.2, nv - 9), P(-1.6, nv - 4), P(-.6, nv), 2.6, 4.4, skinDark);
  brush(P(-1.8, nv + .8), P(0, nv + 2), P(2.4, nv + .6), 4, 1.6, inkC);

  // ---- eyes ----
  const eyes = (s) => {
    const cu = s * F.eyeU, cv = F.eyeV, ew = F.eyeW * sx(cv);
    const inner = cu - s * ew, outer = cu + s * ew;
    if (expr === 'hurt') {
      // squeezed: > <
      brush(P(outer, cv - 3), P(cu, cv - .5), P(inner + s * 1.5, cv + 1), 5, 3);
      brush(P(inner + s * 1.5, cv + 1), P(cu, cv + 1.5), P(outer, cv + 4), 3, 4.5);
      return;
    }
    if (expr === 'ko') {
      g.beginPath(); g.moveTo(X(cu - 5), Y(cv - 4)); g.lineTo(X(cu + 5), Y(cv + 4)); g.moveTo(X(cu + 5), Y(cv - 4)); g.lineTo(X(cu - 5), Y(cv + 4)); stroke(4);
      return;
    }
    const smile = !isSiwoo && expr !== 'hurt';
    if (smile) {
      // 실눈: closed arc, thick in the middle, flick at the outer end
      brush(P(inner, cv + 2), P(cu, cv - 4.2), P(outer, cv + 1.2), 5, 11);
      brush(P(outer - s * .5, cv + 1.1), P(outer + s * 1.8, cv + .5), P(outer + s * 3.8, cv - 1.2), 8, 1.5);
      g.beginPath(); g.moveTo(X(cu + s * ew * .2), Y(cv + 3.8)); g.quadraticCurveTo(X(cu + s * ew * .7), Y(cv + 4.6), X(outer), Y(cv + 2.8)); stroke(2.6);
      return;
    }
    // sharp half-lidded almond (Siwoo; win = narrower glare)
    const lidDrop = expr === 'win' ? 1.6 : (isSiwoo ? .9 : 0);
    const top = cv - 2.6 + lidDrop, bot = cv + 2.4;
    const innerY = cv + .6 + (isSiwoo ? -.4 : 0), outerY = cv - 1.2 + (isSiwoo ? .8 : 0);
    // white
    g.beginPath(); g.moveTo(X(inner), Y(innerY));
    g.quadraticCurveTo(X(cu - s * ew * .1), Y(top - 1.4), X(outer), Y(outerY));
    g.quadraticCurveTo(X(cu + s * ew * .15), Y(bot + 1.2), X(inner), Y(innerY));
    g.closePath(); g.fillStyle = paper; g.fill();
    g.save(); g.clip();
    // small iris, cut by the lid, looking slightly inward
    const ir = 3.4, iu = cu - s * 1.2, iv = cv + .9;
    g.fillStyle = mono ? inkC : def.iris; g.beginPath(); g.ellipse(X(iu), Y(iv), ir * D * sx(cv), ir * D * 1.05, 0, 0, TAU); g.fill();
    g.fillStyle = inkC; g.beginPath(); g.ellipse(X(iu), Y(iv), ir * .55 * D * sx(cv), ir * .6 * D, 0, 0, TAU); g.fill();
    g.fillStyle = paper; g.beginPath(); g.arc(X(iu + s * .9), Y(iv - 1.3), 2.2, 0, TAU); g.fill();
    g.restore();
    // heavy upper lid with a sharp outer wing
    brush(P(inner - s * .4, innerY + .2), P(cu - s * ew * .15, top - 1.8), P(outer, outerY), 5, 11);
    brush(P(outer - s * .4, outerY), P(outer + s * 2, outerY - .4), P(outer + s * 4.2, outerY - 1.8 + (isSiwoo ? 1 : 0)), 9, 1.5);
    // short lower lid at the outer third
    g.beginPath(); g.moveTo(X(cu + s * ew * .05), Y(bot + .9)); g.quadraticCurveTo(X(cu + s * ew * .6), Y(bot + .9), X(outer - s * .3), Y(outerY + 1.2)); stroke(2.8);
    // double-lid crease
    g.beginPath(); g.moveTo(X(cu - s * ew * .2), Y(top - 3.2)); g.quadraticCurveTo(X(cu + s * ew * .4), Y(top - 3.8), X(outer + s * .4), Y(outerY - 2.6)); stroke(2, skinDark);
  };
  eyes(-1); eyes(1);

  // ---- brows: thin straight blades; Siwoo's dip toward the glabella ----
  for (const s of [-1, 1]) {
    const bv = F.browV, inn = s * (F.eyeU - F.eyeW * .95), out = s * (F.eyeU + F.eyeW * 1.25);
    const angry = isSiwoo ? 3.4 : (expr === 'hurt' ? 2.6 : -.4);
    const relaxed = !isSiwoo && expr !== 'hurt' ? -1.2 : 0;
    const thick = isSiwoo ? 8 : 10.5;
    brush(P(inn, bv + angry), P((inn + out) / 2, bv - 1 + relaxed), P(out, bv + .6 + relaxed * .3), thick, thick * .35);
  }

  // ---- mouth ----
  const mv = F.mouthV, mw = 6 * sx(mv);
  if (expr === 'hurt') {
    g.beginPath(); g.moveTo(X(-mw), Y(mv)); g.lineTo(X(mw), Y(mv)); g.lineTo(X(mw * .8), Y(mv + 3)); g.lineTo(X(-mw * .8), Y(mv + 3)); g.closePath();
    g.fillStyle = paper; g.fill(); stroke(3);
    g.beginPath(); g.moveTo(X(-mw * .9), Y(mv + 1.5)); g.lineTo(X(mw * .9), Y(mv + 1.5)); stroke(1.6);
  } else if (expr === 'ko') {
    g.beginPath(); g.ellipse(X(0), Y(mv + 1), 3 * D * sx(mv), 2.4 * D, 0, 0, TAU); g.fillStyle = inkC; g.fill();
  } else if (expr === 'win' || !isSiwoo) {
    const wide = expr === 'win' ? 1.15 : 1;
    if (expr === 'win' || !isSiwoo) {
      // easy grin: thin upturned line, teeth only on win
      if (expr === 'win') {
        g.beginPath(); g.moveTo(X(-mw * wide), Y(mv - .8)); g.quadraticCurveTo(X(0), Y(mv + 5.2), X(mw * wide), Y(mv - 1.6)); g.closePath();
        g.fillStyle = inkC; g.fill();
        g.save(); g.clip(); g.fillStyle = paper; g.fillRect(X(-mw * wide), Y(mv - 1.6), 2 * mw * wide * D, 2.1 * D); g.restore();
      } else {
        brush(P(-mw * .95, mv - .6), P(0, mv + 2.8), P(mw * .95, mv - 1.4), 2.6, 4.4);
        brush(P(mw * .95, mv - 1.4), P(mw * 1.1, mv - 1.9), P(mw * 1.25, mv - 2.8), 4, 1.2);
      }
    }
  } else {
    // Siwoo: short flat line, one corner pulled down
    brush(P(-mw * .7, mv + .2), P(0, mv - .5), P(mw * .75, mv + 1.1), 3.4, 4.6);
    g.beginPath(); g.moveTo(X(-mw * .3), Y(mv + 3.4)); g.lineTo(X(mw * .3), Y(mv + 3.4)); stroke(1.4, skinDark);
  }

  // ---- jaw shadow tick under the chin ----
  g.beginPath(); g.moveTo(X(-6), Y(F.chinV - 1)); g.quadraticCurveTo(X(0), Y(F.chinV + 1.5), X(6), Y(F.chinV - 1)); stroke(1.5, skinDark);

  // ---- band-aid across the bridge (Siwoo) ----
  if (def.bandaid) {
    g.save(); g.translate(X(-.5), Y(F.noseV - 6.5)); g.rotate(-.22);
    const bw = 15 * D, bh = 4 * D;
    g.fillStyle = mono ? paper : '#f1d3a6'; g.strokeStyle = inkC; g.lineWidth = 2;
    g.beginPath(); g.roundRect(-bw / 2, -bh / 2, bw, bh, bh / 2); g.fill(); g.stroke();
    g.fillStyle = mono ? 'rgba(0,0,0,0)' : '#e3b882'; g.fillRect(-bw * .16, -bh / 2 + 2, bw * .32, bh - 4);
    g.strokeStyle = inkC; g.lineWidth = 1.2; g.strokeRect(-bw * .16, -bh / 2 + 2, bw * .32, bh - 4);
    g.fillStyle = inkC; for (const i of [-3, -2, 2, 3]) { g.beginPath(); g.arc(i * bw * .11, 0, 1.4, 0, TAU); g.fill(); }
    g.restore();
  }
  return canvasTex(c);
}
