// ---------- color per style ----------
function styleColor(S, hex) {
  const c = new THREE.Color(hex);
  const hsl = {}; c.getHSL(hsl);
  if (S.tone === 'pastel') { hsl.s *= .82; hsl.l = hsl.l + (1 - hsl.l) * .2; }
  else if (S.tone === 'warm') { hsl.s *= .9; }
  else if (S.tone === 'vivid') { hsl.s = Math.min(1, hsl.s * 1.06); }
  c.setHSL(hsl.h, hsl.s, hsl.l);
  return c;
}
const css = (S, hex) => '#' + styleColor(S, hex).getHexString();
function shade(S, hex, k) { const c = styleColor(S, hex); c.multiplyScalar(k); return '#' + c.getHexString(); }

function canvasTex(c) { const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4; return t; }
function mkCanvas(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; }

// ---------- faces (equirect texture for a sphere head; face front at u=0.25) ----------
function faceTexture(def, S, expr) {
  const W = 1024, H = 512, c = mkCanvas(W, H), g = c.getContext('2d');
  const skin = css(S, def.skin);
  g.fillStyle = skin; g.fillRect(0, 0, W, H);
  const X = ud => W * (.25 + ud / 360), Y = vd => H * (.5 + vd / 180), D = 2.844; // px per degree
  const ink = S.face === 'chibi' ? css(S, '#3a2a3e') : '#1d1820';
  const kind = S.face;
  const isSiwoo = def.id === 'siwoo';
  const prm = kind === 'anime' ? { ex: 16.5, ey: 3, ew: 8.6, eh: 10.5, brow: -10, mouth: 22 }
    : kind === 'chibi' ? { ex: 17, ey: 10, ew: 6.4, eh: 8.8, brow: -2, mouth: 25 }
      : { ex: 14.5, ey: 2, ew: 6.8, eh: 3.6, brow: -7, mouth: 21 };
  g.lineCap = 'round'; g.lineJoin = 'round';

  // cheeks / blush
  if (kind !== 'real' || !isSiwoo) {
    g.fillStyle = kind === 'chibi' ? 'rgba(255,120,140,.42)' : 'rgba(240,120,110,.18)';
    for (const s of [-1, 1]) { g.beginPath(); g.ellipse(X(s * (prm.ex + 6)), Y(prm.ey + (kind === 'chibi' ? 8 : 10)), 6 * D, 3 * D, 0, 0, TAU); g.fill(); }
  }
  // nose
  if (kind === 'anime') { g.strokeStyle = shade(S, def.skin, .72); g.lineWidth = 3; g.beginPath(); g.moveTo(X(.6), Y(11)); g.lineTo(X(-.6), Y(13.5)); g.stroke(); }
  if (kind === 'real') {
    const gr = g.createRadialGradient(X(-2), Y(12), 1, X(-2), Y(12), 7 * D); gr.addColorStop(0, shade(S, def.skin, .82)); gr.addColorStop(1, skin);
    g.fillStyle = gr; g.beginPath(); g.ellipse(X(-1.5), Y(12), 4 * D, 6 * D, 0, 0, TAU); g.fill();
    g.fillStyle = shade(S, def.skin, .55); for (const s of [-1, 1]) { g.beginPath(); g.ellipse(X(s * 2.2), Y(15.5), 1.2 * D, .7 * D, 0, 0, TAU); g.fill(); }
  }

  // eyes
  for (const s of [-1, 1]) {
    const cx = X(s * prm.ex), cy = Y(prm.ey), ew = prm.ew * D, eh = prm.eh * D;
    if (expr === 'hurt') {
      g.strokeStyle = ink; g.lineWidth = kind === 'chibi' ? 9 : 7;
      if (kind === 'real') { g.beginPath(); g.moveTo(cx - ew, cy); g.quadraticCurveTo(cx, cy - eh * .6, cx + ew, cy + eh * .2); g.stroke(); }
      else { g.beginPath(); g.moveTo(cx - s * ew * .9, cy - eh * .6); g.lineTo(cx + s * ew * .5, cy); g.lineTo(cx - s * ew * .9, cy + eh * .6); g.stroke(); }
      continue;
    }
    if (expr === 'ko') {
      g.strokeStyle = ink; g.lineWidth = 5;
      if (kind === 'real') { g.beginPath(); g.moveTo(cx - ew, cy + 2); g.quadraticCurveTo(cx, cy + eh * .6, cx + ew, cy + 2); g.stroke(); }
      else { g.beginPath(); for (let a = 0; a < 6 * Math.PI; a += .2) { const r = a / (6 * Math.PI) * Math.min(ew, eh); g.lineTo(cx + Math.cos(a) * r, cy + Math.sin(a) * r); } g.stroke(); }
      continue;
    }
    if (expr === 'win') {
      g.strokeStyle = ink; g.lineWidth = kind === 'chibi' ? 9 : 7;
      g.beginPath(); g.moveTo(cx - ew * .9, cy + eh * .2); g.quadraticCurveTo(cx, cy - eh * .9, cx + ew * .9, cy + eh * .2); g.stroke();
      continue;
    }
    if (kind === 'anime') {
      g.fillStyle = '#ffffff'; g.beginPath(); g.ellipse(cx, cy, ew, eh, 0, 0, TAU); g.fill();
      g.save(); g.beginPath(); g.ellipse(cx, cy, ew, eh, 0, 0, TAU); g.clip();
      const ix = cx + s * -ew * .08, iy = cy + eh * .1;
      const gr = g.createLinearGradient(0, iy - eh, 0, iy + eh); gr.addColorStop(0, '#120c10'); gr.addColorStop(1, def.iris);
      g.fillStyle = gr; g.beginPath(); g.ellipse(ix, iy, ew * .66, eh * .9, 0, 0, TAU); g.fill();
      g.fillStyle = '#0b0709'; g.beginPath(); g.ellipse(ix, iy, ew * .3, eh * .42, 0, 0, TAU); g.fill();
      g.fillStyle = '#fff'; g.beginPath(); g.arc(ix - ew * .25, iy - eh * .38, eh * .2, 0, TAU); g.fill();
      g.beginPath(); g.arc(ix + ew * .22, iy + eh * .42, eh * .09, 0, TAU); g.fill();
      g.restore();
      // upper lash with a flick at the outer corner
      g.strokeStyle = ink; g.lineWidth = 9;
      g.beginPath(); g.ellipse(cx, cy + 2, ew * 1.04, eh * 1.0, 0, Math.PI * 1.12, Math.PI * 1.88); g.stroke();
      g.lineWidth = 6; g.beginPath(); g.moveTo(cx + s * ew * .92, cy - eh * .45); g.lineTo(cx + s * ew * 1.28, cy - eh * .7); g.stroke();
      g.lineWidth = 3; g.beginPath(); g.ellipse(cx, cy, ew * .8, eh * 1.0, 0, Math.PI * .3, Math.PI * .7); g.stroke();
    } else if (kind === 'chibi') {
      g.fillStyle = ink; g.beginPath(); g.ellipse(cx, cy, ew, eh, 0, 0, TAU); g.fill();
      g.fillStyle = '#fff'; g.beginPath(); g.arc(cx - ew * .32, cy - eh * .36, ew * .42, 0, TAU); g.fill();
      g.beginPath(); g.arc(cx + ew * .3, cy + eh * .42, ew * .18, 0, TAU); g.fill();
    } else {
      // almond eye
      g.fillStyle = '#f4efe8';
      g.beginPath(); g.moveTo(cx - ew, cy); g.quadraticCurveTo(cx, cy - eh * 1.5, cx + ew, cy); g.quadraticCurveTo(cx, cy + eh * 1.2, cx - ew, cy); g.fill();
      g.save(); g.clip();
      g.fillStyle = def.iris; g.beginPath(); g.arc(cx + s * -ew * .05, cy - eh * .05, eh * 1.05, 0, TAU); g.fill();
      g.fillStyle = '#100a08'; g.beginPath(); g.arc(cx + s * -ew * .05, cy - eh * .05, eh * .5, 0, TAU); g.fill();
      g.fillStyle = 'rgba(255,255,255,.9)'; g.beginPath(); g.arc(cx - ew * .18, cy - eh * .45, eh * .25, 0, TAU); g.fill();
      g.restore();
      g.strokeStyle = '#231a18'; g.lineWidth = 5; g.beginPath(); g.moveTo(cx - ew * 1.05, cy + 1); g.quadraticCurveTo(cx, cy - eh * 1.55, cx + ew * 1.05, cy + 1); g.stroke();
      g.strokeStyle = shade(S, def.skin, .7); g.lineWidth = 2.5; g.beginPath(); g.moveTo(cx - ew * .8, cy - eh * 1.25); g.quadraticCurveTo(cx, cy - eh * 2.1, cx + ew * .9, cy - eh * 1.1); g.stroke();
    }
  }

  // brows
  g.strokeStyle = kind === 'chibi' ? ink : css(S, def.hair);
  g.lineWidth = kind === 'chibi' ? 7 : (kind === 'real' ? 9 : 8);
  for (const s of [-1, 1]) {
    const b = prm.brow, inX = s * (prm.ex - prm.ew * .9), outX = s * (prm.ex + prm.ew * 1.1);
    const angry = isSiwoo ? 3.2 : -.8;
    const hurt = expr === 'hurt' ? 2.5 : 0;
    g.beginPath(); g.moveTo(X(inX), Y(b + angry + hurt)); g.quadraticCurveTo(X((inX + outX) / 2), Y(b - 2.2), X(outX), Y(b + .6)); g.stroke();
  }

  // mouth
  g.strokeStyle = kind === 'chibi' ? ink : shade(S, def.skin, .45); g.lineWidth = kind === 'chibi' ? 6 : 4.5;
  const my = Y(prm.mouth);
  if (expr === 'hurt' || expr === 'ko') {
    g.fillStyle = '#5a2228'; g.beginPath(); g.ellipse(X(0), my + 6, 3.4 * D, (expr === 'ko' ? 2.6 : 2) * D, 0, 0, TAU); g.fill();
  } else if (expr === 'win' || !isSiwoo) {
    if (expr === 'win' || kind !== 'real') {
      g.fillStyle = '#7a2c33'; g.beginPath(); g.moveTo(X(-5), my); g.quadraticCurveTo(X(0), my + 7 * D, X(5), my); g.closePath(); g.fill();
      g.fillStyle = '#fff'; g.fillRect(X(-3.6), my, 7.2 * D, 1.6 * D);
    } else { g.beginPath(); g.moveTo(X(-5), my); g.quadraticCurveTo(X(0), my + 3 * D, X(5), my - 1); g.stroke(); }
  } else {
    g.beginPath(); g.moveTo(X(-3.5), my + 2); g.quadraticCurveTo(X(0), my - 2, X(3.8), my + 3); g.stroke();
  }

  // band-aid (Siwoo)
  if (def.bandaid) {
    const bx = kind === 'chibi' ? X(-21) : X(0), by = kind === 'chibi' ? Y(prm.ey + 9) : Y(prm.ey + 8);
    g.save(); g.translate(bx, by); g.rotate(-.18);
    const bw = (kind === 'chibi' ? 9 : 13) * D, bh = 3.4 * D;
    g.fillStyle = '#f0cfa2'; g.strokeStyle = 'rgba(80,50,30,.55)'; g.lineWidth = 2;
    g.beginPath(); g.roundRect(-bw / 2, -bh / 2, bw, bh, bh / 2); g.fill(); g.stroke();
    g.fillStyle = '#e2b582'; g.fillRect(-bw * .17, -bh / 2 + 2, bw * .34, bh - 4);
    g.fillStyle = 'rgba(120,80,50,.35)';
    for (let i = -3; i <= 3; i++) if (Math.abs(i) > 1) { g.beginPath(); g.arc(i * bw * .12, 0, 1.6, 0, TAU); g.fill(); }
    g.restore();
  }
  return canvasTex(c);
}

// ---------- uniform (lathe UV: front at u=.5, character's left at u=.75) ----------
function outfitTexture(def, S, yA, yB) {
  const W = 512, H = 256, c = mkCanvas(W, H), g = c.getContext('2d');
  const X = du => (.5 + du) * W, Y = yn => (1 - (yn - yA) / (yB - yA)) * H;
  const base = def.blazerOn ? def.blazer : def.shirt;
  g.fillStyle = css(S, base); g.fillRect(0, 0, W, H);
  const line = S.outline ? 'rgba(20,18,40,.85)' : 'rgba(20,18,40,.35)';
  g.lineCap = 'round'; g.lineJoin = 'round';
  const tie = (top, bot, wTop, wBot) => {
    g.fillStyle = css(S, def.tie);
    g.beginPath(); g.moveTo(X(-wTop), Y(top)); g.lineTo(X(wTop), Y(top)); g.lineTo(X(wBot), Y(bot + .05)); g.lineTo(X(0), Y(bot)); g.lineTo(X(-wBot), Y(bot + .05)); g.closePath(); g.fill();
    g.save(); g.clip(); g.strokeStyle = css(S, def.tieStripe); g.lineWidth = 3;
    for (let k = 0; k < 14; k++) { const yy = Y(top) + k * 14; g.beginPath(); g.moveTo(X(-.05), yy); g.lineTo(X(.05), yy + 12); g.stroke(); }
    g.restore();
    g.fillStyle = shade(S, def.tie, .8); g.beginPath(); g.roundRect(X(-wTop * 1.15), Y(top + .07), wTop * 2.3 * W, (Y(top) - Y(top + .07)) * 1.05, 4); g.fill();
  };
  if (def.blazerOn) {
    // open blazer: shirt strip, lapels, loosened tie, school badge
    g.fillStyle = css(S, def.shirt);
    g.beginPath(); g.moveTo(X(-.085), Y(1.05)); g.lineTo(X(-.035), Y(.55)); g.lineTo(X(-.05), Y(-.2)); g.lineTo(X(.05), Y(-.2)); g.lineTo(X(.035), Y(.55)); g.lineTo(X(.085), Y(1.05)); g.closePath(); g.fill();
    tie(.86, .42, .016, .024);
    g.strokeStyle = line; g.lineWidth = 3;
    for (const s of [-1, 1]) { g.beginPath(); g.moveTo(X(s * .085), Y(1.05)); g.lineTo(X(s * .035), Y(.55)); g.lineTo(X(s * .05), Y(-.2)); g.stroke(); g.beginPath(); g.moveTo(X(s * .07), Y(.85)); g.lineTo(X(s * .11), Y(.8)); g.lineTo(X(s * .05), Y(.66)); g.stroke(); }
    g.fillStyle = css(S, '#e3b84a'); g.beginPath(); const bx = X(.12), by = Y(.7); g.moveTo(bx - 12, by - 12); g.lineTo(bx + 12, by - 12); g.lineTo(bx + 12, by + 2); g.lineTo(bx, by + 14); g.lineTo(bx - 12, by + 2); g.closePath(); g.fill();
    g.strokeStyle = line; g.lineWidth = 2; g.stroke(); g.beginPath(); g.moveTo(X(.08), Y(.62)); g.lineTo(X(.16), Y(.62)); g.stroke();
    g.fillStyle = shade(S, def.blazer, .6); for (const yy of [.42, .25]) { g.beginPath(); g.arc(X(-.055), Y(yy), 5, 0, TAU); g.fill(); }
    g.strokeStyle = line; g.lineWidth = 2.5; for (const s of [-1, 1]) { g.beginPath(); g.moveTo(X(s * .09), Y(.18)); g.lineTo(X(s * .17), Y(.18)); g.stroke(); }
  } else {
    // shirt: placket, buttons, pocket, collar, tidy tie
    g.strokeStyle = 'rgba(120,128,150,.45)'; g.lineWidth = 2; g.beginPath(); g.moveTo(X(.004), Y(1.05)); g.lineTo(X(.004), Y(-.2)); g.stroke();
    g.fillStyle = 'rgba(150,156,175,.9)'; for (const yy of [.72, .56, .4, .24, .08]) { g.beginPath(); g.arc(X(.012), Y(yy), 3.2, 0, TAU); g.fill(); }
    g.strokeStyle = 'rgba(120,128,150,.55)'; g.lineWidth = 2; g.strokeRect(X(.07), Y(.78), .07 * W, Y(.66) - Y(.78));
    g.strokeStyle = 'rgba(150,156,175,.35)'; g.lineWidth = 2;
    for (const s of [-1, 1]) for (let k = 0; k < 3; k++) { g.beginPath(); g.moveTo(X(s * (.13 + k * .03)), Y(.95)); g.quadraticCurveTo(X(s * (.15 + k * .03)), Y(.6), X(s * (.12 + k * .03)), Y(.3)); g.stroke(); }
    tie(.93, .45, .014, .022);
    g.fillStyle = css(S, def.shirt); g.strokeStyle = 'rgba(110,118,140,.7)'; g.lineWidth = 2;
    for (const s of [-1, 1]) { g.beginPath(); g.moveTo(X(s * .02), Y(.97)); g.lineTo(X(s * .085), Y(1.05)); g.lineTo(X(s * .075), Y(.9)); g.closePath(); g.fill(); g.stroke(); }
  }
  return canvasTex(c);
}

// ---------- environment textures ----------
function pavingTexture(S) {
  const W = 512, H = 256, c = mkCanvas(W, H), g = c.getContext('2d'), r = mulberry(7);
  const e = S.env; g.fillStyle = css(S, e.mortar); g.fillRect(0, 0, W, H);
  const rows = 4, rh = H / rows;
  for (let j = 0; j < rows; j++) {
    let x = (j % 2) * -40;
    while (x < W) {
      const w = 70 + r() * 70; g.fillStyle = css(S, r() < .5 ? e.tileA : e.tileB);
      g.fillRect(x + 3, j * rh + 3, w - 6, rh - 6);
      if (S.shading === 'pbr') { for (let k = 0; k < 40; k++) { g.fillStyle = `rgba(0,0,0,${r() * .08})`; g.fillRect(x + r() * w, j * rh + r() * rh, 2, 2); } }
      x += w;
    }
  }
  const t = canvasTex(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; return t;
}
function labelTexture(lines, bg, fg, w = 512, h = 256, font = 'Black Han Sans') {
  const c = mkCanvas(w, h), g = c.getContext('2d');
  g.fillStyle = bg; g.fillRect(0, 0, w, h);
  g.fillStyle = fg; g.textAlign = 'center'; g.textBaseline = 'middle';
  lines.forEach(([txt, size, y]) => { g.font = `${size}px "${font}", "Gothic A1", sans-serif`; g.fillText(txt, w / 2, y * h); });
  return canvasTex(c);
}

// ---------- FX sprites ----------
function fxTextures() {
  const T = {};
  const mk = (size, draw) => { const c = mkCanvas(size, size), g = c.getContext('2d'); draw(g, size); return canvasTex(c); };
  T.flash = mk(128, (g, s) => { const gr = g.createRadialGradient(s / 2, s / 2, 0, s / 2, s / 2, s / 2); gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(.35, 'rgba(255,250,230,.75)'); gr.addColorStop(1, 'rgba(255,240,200,0)'); g.fillStyle = gr; g.fillRect(0, 0, s, s); });
  T.burst = mk(256, (g, s) => {
    const cx = s / 2, n = 14; g.beginPath();
    for (let i = 0; i < n * 2; i++) { const a = i / (n * 2) * TAU, r = (i % 2 ? .2 : (.42 + (i % 4 === 0 ? .06 : 0))) * s; g.lineTo(cx + Math.cos(a) * r, cx + Math.sin(a) * r); }
    g.closePath(); g.fillStyle = '#fff36b'; g.strokeStyle = '#1c1a2e'; g.lineWidth = 10; g.lineJoin = 'round'; g.stroke(); g.fill();
    g.beginPath(); g.arc(cx, cx, s * .14, 0, TAU); g.fillStyle = '#fff'; g.fill();
  });
  T.spark = mk(64, (g, s) => { g.fillStyle = '#fff'; g.beginPath(); g.moveTo(s / 2, 0); g.lineTo(s * .62, s / 2); g.lineTo(s / 2, s); g.lineTo(s * .38, s / 2); g.closePath(); g.fill(); });
  T.star = mk(128, (g, s) => {
    g.beginPath(); for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + i / 10 * TAU, r = (i % 2 ? .22 : .46) * s; g.lineTo(s / 2 + Math.cos(a) * r, s / 2 + Math.sin(a) * r); }
    g.closePath(); g.lineJoin = 'round'; g.lineWidth = 10; g.strokeStyle = '#fff'; g.stroke(); g.fillStyle = '#ffe07a'; g.fill();
  });
  T.heart = mk(128, (g, s) => {
    g.beginPath(); g.moveTo(s / 2, s * .8); g.bezierCurveTo(s * .05, s * .5, s * .2, s * .1, s / 2, s * .32); g.bezierCurveTo(s * .8, s * .1, s * .95, s * .5, s / 2, s * .8);
    g.lineWidth = 10; g.strokeStyle = '#fff'; g.stroke(); g.fillStyle = '#ff8fb3'; g.fill();
  });
  T.puff = mk(128, (g, s) => { const gr = g.createRadialGradient(s / 2, s / 2, 0, s / 2, s / 2, s / 2); gr.addColorStop(0, 'rgba(255,255,255,.85)'); gr.addColorStop(.6, 'rgba(255,255,255,.45)'); gr.addColorStop(1, 'rgba(255,255,255,0)'); g.fillStyle = gr; g.fillRect(0, 0, s, s); });
  T.drop = mk(64, (g, s) => { g.fillStyle = 'rgba(225,240,255,.95)'; g.beginPath(); g.moveTo(s / 2, s * .1); g.quadraticCurveTo(s * .8, s * .6, s / 2, s * .9); g.quadraticCurveTo(s * .2, s * .6, s / 2, s * .1); g.fill(); });
  T.ring = mk(256, (g, s) => { const gr = g.createRadialGradient(s / 2, s / 2, s * .3, s / 2, s / 2, s / 2); gr.addColorStop(0, 'rgba(255,255,255,0)'); gr.addColorStop(.6, 'rgba(255,255,255,.9)'); gr.addColorStop(1, 'rgba(255,255,255,0)'); g.fillStyle = gr; g.fillRect(0, 0, s, s); });
  return T;
}
const wordCache = new Map();
function wordTexture(S, text) {
  const key = S.key + text; if (wordCache.has(key)) return wordCache.get(key);
  const w = S.word, c = mkCanvas(512, 256), g = c.getContext('2d');
  g.font = `${S.key === 'B' ? 150 : 140}px "${w.font}", "Gothic A1", sans-serif`; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.lineJoin = 'round'; g.translate(256, 132); g.rotate(-.08);
  g.fillStyle = w.shadow; g.fillText(text, 10, 10);
  g.lineWidth = 26; g.strokeStyle = w.stroke; g.strokeText(text, 0, 0);
  g.fillStyle = w.fill; g.fillText(text, 0, 0);
  const t = canvasTex(c);
  if (document.fonts && document.fonts.check(`40px "${w.font}"`)) wordCache.set(key, t);
  return t;
}
