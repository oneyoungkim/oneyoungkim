// ---------- ink FX sprites (A/I): 먹 붓 터치, 흰 섬광 조각, 속도선, 먹 점, 만화 먼지 구름 ----------
// 흰색으로 그린 것(brush/splat/lines/ring)은 SpriteMaterial.color로 먹색을 입힌다. star/shard/dust는 흰 면 + 먹 테를 그대로 쓴다.
const _inkFx = new Map();
function inkFxTex(S) {
  const inkHex = S.ink ? S.ink.ink : '#111111';
  if (_inkFx.has(inkHex)) return _inkFx.get(inkHex);
  const R = mulberry(2024), T = {};
  const mk = (w, h, draw) => { const c = mkCanvas(w, h), g = c.getContext('2d'); g.lineCap = 'round'; g.lineJoin = 'round'; draw(g, w, h); return canvasTex(c); };
  // 붓 획: 초승달 모양, 머리는 굵고 꼬리는 갈라지며(비백) 가늘어진다
  T.brush = mk(512, 256, (g, W, H) => {
    const r = H * 2, cx = W / 2, cy = H * .5 - 26 + r, a0 = -Math.PI / 2 - .47, a1 = -Math.PI / 2 + .47, N = 64; // 획 가운데가 텍스처 중심을 지난다
    const wAt = t => H * .26 * Math.pow(Math.sin(Math.PI * Math.min(1, t * 1.15)), .55) * (1 - t * .35);
    g.fillStyle = '#fff'; g.beginPath();
    for (let i = 0; i <= N; i++) { const t = i / N, a = a0 + (a1 - a0) * t, rr = r + wAt(t) * .5; g.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); }
    for (let i = N; i >= 0; i--) { const t = i / N, a = a0 + (a1 - a0) * t, rr = r - wAt(t) * .5; g.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); }
    g.closePath(); g.fill();
    // 비백: 꼬리 쪽으로 갈수록 결 사이가 빈다
    g.globalCompositeOperation = 'destination-out'; g.strokeStyle = '#000';
    for (let k = 0; k < 15; k++) {
      const off = (R() - .5) * .9, t0 = .3 + R() * .5; g.lineWidth = 1 + R() * 2.4; g.beginPath();
      for (let i = 0; i <= 30; i++) { const t = t0 + (1 - t0) * i / 30, a = a0 + (a1 - a0) * t, rr = r + wAt(t) * off; if (i) g.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); else g.moveTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); }
      g.stroke();
    }
    g.globalCompositeOperation = 'source-over'; g.fillStyle = '#fff';
    for (let k = 0; k < 12; k++) { const t = R() * .35, a = a0 + (a1 - a0) * t, rr = r + (R() - .2) * H * .38; g.beginPath(); g.arc(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr, 1.5 + R() * 4, 0, TAU); g.fill(); }
  });
  // 먹 튀김: 울퉁불퉁한 덩어리 + 작은 방울
  T.splat = mk(128, 128, (g, W) => {
    g.fillStyle = '#fff'; g.beginPath();
    for (let i = 0; i <= 18; i++) { const a = i / 18 * TAU, rr = W * (.24 + R() * .12); g.lineTo(W / 2 + Math.cos(a) * rr, W / 2 + Math.sin(a) * rr); }
    g.closePath(); g.fill();
    for (let k = 0; k < 6; k++) { const a = R() * TAU, rr = W * (.36 + R() * .1); g.beginPath(); g.arc(W / 2 + Math.cos(a) * rr, W / 2 + Math.sin(a) * rr, 2 + R() * 5, 0, TAU); g.fill(); }
  });
  // 흰 섬광 조각: 가늘고 긴 마름모, 먹 테
  T.shard = mk(64, 256, (g, W, H) => {
    g.beginPath(); g.moveTo(W / 2, 6); g.lineTo(W * .78, H * .42); g.lineTo(W / 2, H - 6); g.lineTo(W * .26, H * .5); g.closePath();
    g.lineWidth = 7; g.strokeStyle = inkHex; g.stroke(); g.fillStyle = '#ffffff'; g.fill();
  });
  // 충격 섬광: 들쭉날쭉한 흰 별 + 먹 테
  T.star = mk(256, 256, (g, W) => {
    const n = 11; g.beginPath();
    for (let i = 0; i < n * 2; i++) { const a = i / (n * 2) * TAU + R() * .08, rr = W * (i % 2 ? .13 + R() * .05 : .34 + R() * .13); g.lineTo(W / 2 + Math.cos(a) * rr, W / 2 + Math.sin(a) * rr); }
    g.closePath(); g.lineWidth = 9; g.strokeStyle = inkHex; g.stroke(); g.fillStyle = '#ffffff'; g.fill();
  });
  // 집중선: 바깥에서 안으로 가늘어지는 먹 선 (가운데는 비움)
  T.lines = mk(512, 512, (g, W) => {
    g.fillStyle = '#fff';
    for (let i = 0; i < 70; i++) {
      const a = R() * TAU, r0 = W * (.2 + R() * .1), r1 = W * (.44 + R() * .06), w = .006 + R() * .014;
      g.beginPath(); g.moveTo(W / 2 + Math.cos(a) * r0, W / 2 + Math.sin(a) * r0);
      g.lineTo(W / 2 + Math.cos(a - w) * r1, W / 2 + Math.sin(a - w) * r1); g.lineTo(W / 2 + Math.cos(a + w) * r1, W / 2 + Math.sin(a + w) * r1); g.closePath(); g.fill();
    }
  });
  // 붓으로 그린 고리 (메치기 착지 땅바닥)
  T.ring = mk(256, 256, (g, W) => {
    g.strokeStyle = '#fff';
    for (let k = 0; k < 4; k++) { const a0 = R() * TAU, len = TAU * (.55 + R() * .35); g.lineWidth = 5 + R() * 9; g.beginPath(); g.arc(W / 2, W / 2, W * (.4 + (R() - .5) * .04), a0, a0 + len); g.stroke(); }
  });
  // 만화 먼지 구름: 흰 원 뭉치 + 먹 테 (가리비 테두리)
  T.dust = mk(256, 256, (g, W) => {
    const cs = [[.5, .55, .2], [.32, .58, .15], [.68, .6, .15], [.42, .4, .15], [.6, .42, .13]].map(([x, y, r]) => [x * W, y * W, r * W * (.9 + R() * .2)]);
    g.fillStyle = inkHex; cs.forEach(([x, y, r]) => { g.beginPath(); g.arc(x, y, r + 6, 0, TAU); g.fill(); });
    g.fillStyle = '#ffffff'; cs.forEach(([x, y, r]) => { g.beginPath(); g.arc(x, y, r, 0, TAU); g.fill(); });
  });
  _inkFx.set(inkHex, T);
  return T;
}

// ---------- particles / impact effects ----------
class FX {
  constructor(scene, S) {
    this.S = S; this.group = new THREE.Group(); scene.add(this.group); this.items = []; this.T = fxTex();
    this.ink = S.fx === 'ink' || !!S.ink; // A/I: 먹 레시피
    if (this.ink) { this.K = inkFxTex(S); this.inkC = S.ink ? S.ink.ink : '#111111'; this.accent = S.ink && S.ink.mode !== 'mono' && S.word ? S.word.shadow : null; }
  }
  sprite(tex, o) {
    const m = new THREE.SpriteMaterial({ map: tex, color: o.color || '#ffffff', transparent: true, depthWrite: false, depthTest: !o.top, blending: o.add ? THREE.AdditiveBlending : THREE.NormalBlending, rotation: o.rot || 0, fog: false });
    const s = new THREE.Sprite(m); s.position.copy(o.pos); s.renderOrder = o.top ? 20 : 5; this.group.add(s);
    this.items.push({ s, m, life: 0, max: o.life, s0: o.s0, s1: o.s1, vel: o.vel || new THREE.Vector3(), grav: o.grav || 0, spin: o.spin || 0, pop: !!o.pop, aspect: o.aspect || 1, o0: o.opacity ?? 1 });
  }
  flat(tex, pos, s0, s1, life, color) {
    const m = new THREE.MeshBasicMaterial({ map: tex, color, transparent: true, depthWrite: false, fog: false });
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), m); mesh.rotation.x = -Math.PI / 2; mesh.position.copy(pos); this.group.add(mesh);
    this.items.push({ s: mesh, m, life: 0, max: life, s0, s1, vel: new THREE.Vector3(), grav: 0, spin: 0, flat: true, o0: 1 });
  }
  hit(pos, power, dir) {
    const S = this.S, T = this.T, R = Math.random;
    const out = () => new THREE.Vector3(dir.x * (.5 + R()) + (R() - .5), .2 + R() * 1.2, dir.z * (.5 + R()) + (R() - .5));
    if (this.ink) return this.inkHit(pos, power, dir, out);
    if (S.fx === 'anime') {
      this.sprite(T.flash, { pos, s0: .2, s1: .9 + power * .25, life: .14, add: true, top: true });
      this.sprite(T.burst, { pos, s0: .1, s1: .45 + power * .2, life: .16, rot: R() * TAU, top: true, pop: true });
      for (let i = 0; i < 6 + power * 4; i++) this.sprite(T.spark, { pos: pos.clone(), s0: .09, s1: .02, life: .22 + R() * .1, vel: out().multiplyScalar(2.4 + power), rot: R() * TAU, color: R() < .5 ? '#fff36b' : '#ffffff', top: true });
    } else if (S.fx === 'cute') {
      this.sprite(T.flash, { pos, s0: .2, s1: .7 + power * .2, life: .14, add: true, color: '#ffe0ec', top: true });
      for (let i = 0; i < 3 + power * 2; i++) this.sprite(R() < .65 ? T.star : T.heart, { pos: pos.clone(), s0: .05, s1: .16 + R() * .1, life: .5 + R() * .25, vel: out().multiplyScalar(1.4), grav: -2.5, spin: (R() - .5) * 8, top: true, pop: true });
      this.sprite(T.ring, { pos, s0: .1, s1: .6 + power * .2, life: .25, color: '#ffffff', top: true, opacity: .8 });
    } else {
      this.sprite(T.flash, { pos, s0: .15, s1: .45 + power * .15, life: .09, add: true, color: '#ffd9a8', top: true, opacity: .7 });
      for (let i = 0; i < 8 + power * 5; i++) this.sprite(T.drop, { pos: pos.clone(), s0: .035 + R() * .03, s1: .02, life: .45 + R() * .25, vel: out().multiplyScalar(1.6 + power * .6), grav: -7, top: false });
      for (let i = 0; i < power; i++) this.sprite(T.puff, { pos: pos.clone(), s0: .1, s1: .5, life: .4, vel: out().multiplyScalar(.3), color: '#e9dccb', opacity: .45 });
    }
  }
  word(pos, text, power) {
    if (!this.S.word) return;
    const t = wordTexture(this.S, text), p = pos.clone(); p.y += .32; p.x += (Math.random() - .5) * .2;
    this.sprite(t, { pos: p, s0: .2, s1: .55 + power * .12, life: .55, aspect: 2, top: true, pop: true, vel: new THREE.Vector3(0, .35, 0) });
  }
  // 먹 레시피: 흰 섬광 별 + 먹 붓 획(공격 방향) + 집중선 + 흰 조각 + 먹 방울. A(color)는 포인트 색 튀김 한 겹.
  inkHit(pos, power, dir, out) {
    const K = this.K, R = Math.random, ink = this.inkC;
    const sx = Math.abs(dir.x) > .2 ? Math.sign(dir.x) : (R() < .5 ? -1 : 1); // 화면에서 맞는 방향 (카메라가 -Z를 봄)
    if (this.accent) this.sprite(K.splat, { pos, s0: .2, s1: .5 + power * .14, life: .16, rot: R() * TAU, color: this.accent, top: true, pop: true });
    this.sprite(K.brush, { pos, s0: .3, s1: .42 + power * .14, life: .2 + power * .03, aspect: 2, rot: (sx > 0 ? -.35 : Math.PI + .35) + (R() - .5) * .7, color: ink, top: true, pop: true });
    if (power >= 2) this.sprite(K.lines, { pos, s0: .9, s1: 1.5 + power * .35, life: .17, rot: R() * TAU, color: ink, top: true, opacity: .9 });
    this.sprite(K.star, { pos, s0: .12, s1: .36 + power * .15, life: .13, rot: R() * TAU, top: true, pop: true });
    for (let i = 0; i < 4 + power * 3; i++) {
      const v = out().multiplyScalar(2.6 + power * .8); v.x += sx * 1.2;
      this.sprite(K.shard, { pos: pos.clone(), s0: .13 + R() * .06, s1: .03, life: .2 + R() * .1, vel: v, aspect: .3, rot: Math.atan2(v.y, v.x) - Math.PI / 2, top: true });
    }
    for (let i = 0; i < 2 + power * 2; i++) this.sprite(K.splat, { pos: pos.clone(), s0: .04 + R() * .05, s1: .02, life: .35 + R() * .2, vel: out().multiplyScalar(1.8 + power * .5), grav: -6, rot: R() * TAU, color: ink, top: true });
  }
  inkSlam(pos) {
    const K = this.K, R = Math.random, ink = this.inkC;
    this.flat(K.ring, pos.clone().setY(.03), .4, 3.0, .5, ink);
    this.sprite(K.lines, { pos: pos.clone().setY(.5), s0: 1.2, s1: 3.2, life: .3, rot: R() * TAU, color: ink, top: true, opacity: .85 });
    for (let i = 0; i < 9; i++) { const a = (i / 9) * TAU + R() * .3; this.sprite(K.dust, { pos: pos.clone().setY(.18), s0: .25, s1: .75 + R() * .3, life: .55 + R() * .25, vel: new THREE.Vector3(Math.cos(a) * 2.2, .25 + R() * .4, Math.sin(a) * 1.2), rot: R() * TAU, pop: true }); }
    for (let i = 0; i < 8; i++) this.sprite(K.splat, { pos: pos.clone().setY(.2), s0: .05 + R() * .05, s1: .02, life: .45, vel: new THREE.Vector3((R() - .5) * 4, 1.5 + R() * 2, (R() - .5) * 2), grav: -8, rot: R() * TAU, color: ink });
  }
  slam(pos) {
    if (this.ink) return this.inkSlam(pos);
    const T = this.T, R = Math.random;
    this.flat(T.ring, pos.clone().setY(.03), .3, 3.2, .45, this.S.fx === 'cute' ? '#ffe3ef' : '#fff6e6');
    for (let i = 0; i < 14; i++) { const a = R() * TAU; this.sprite(T.puff, { pos: pos.clone().setY(.15), s0: .3, s1: 1.1, life: .7 + R() * .3, vel: new THREE.Vector3(Math.cos(a) * 2.4, .4 + R() * .6, Math.sin(a) * 1.4), color: this.S.fx === 'real' ? '#cdbfae' : '#ffffff', opacity: .7 }); }
  }
  update(dt) {
    for (let i = this.items.length - 1; i >= 0; i--) {
      const it = this.items[i]; it.life += dt; const u = Math.min(1, it.life / it.max);
      if (u >= 1) { it.s.removeFromParent(); it.m.dispose(); if (it.flat) it.s.geometry.dispose(); this.items.splice(i, 1); continue; }
      const k = it.pop ? backOut(Math.min(1, u * 3)) : easeOut(u);
      const sc = lerp(it.s0, it.s1, k);
      if (it.flat) it.s.scale.set(sc, sc, 1); else it.s.scale.set(sc * it.aspect, sc, 1);
      it.vel.y += it.grav * dt; it.s.position.addScaledVector(it.vel, dt); it.vel.multiplyScalar(Math.exp(-dt * 2.5));
      if (it.spin) it.m.rotation += it.spin * dt;
      it.m.opacity = it.o0 * (it.pop ? (u < .7 ? 1 : 1 - (u - .7) / .3) : 1 - u * u);
    }
  }
  dispose() { this.items.forEach(it => { it.s.removeFromParent(); it.m.dispose(); }); this.items = []; this.group.removeFromParent(); }
}

// ---------- synthesized SFX (no audio files) ----------
const Sound = {
  ctx: null, on: true, nb: null,
  ensure() {
    if (!this.ctx) { try { this.ctx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { return null; } }
    if (this.ctx.state === 'suspended') this.ctx.resume();
    if (!this.nb) { const n = this.ctx.sampleRate; this.nb = this.ctx.createBuffer(1, n, n); const d = this.nb.getChannelData(0); for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1; }
    return this.ctx;
  },
  noise(t, dur, f, q, gain, type = 'bandpass', f2) {
    const c = this.ctx, s = c.createBufferSource(); s.buffer = this.nb;
    const bq = c.createBiquadFilter(); bq.type = type; bq.frequency.setValueAtTime(f, t); if (f2) bq.frequency.exponentialRampToValueAtTime(f2, t + dur); bq.Q.value = q;
    const g = c.createGain(); g.gain.setValueAtTime(gain, t); g.gain.exponentialRampToValueAtTime(.001, t + dur);
    s.connect(bq).connect(g).connect(c.destination); s.start(t); s.stop(t + dur + .02);
  },
  tone(t, dur, f0, f1, gain, type = 'sine') {
    const c = this.ctx, o = c.createOscillator(); o.type = type; o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(f1, t + dur);
    const g = c.createGain(); g.gain.setValueAtTime(gain, t); g.gain.exponentialRampToValueAtTime(.001, t + dur);
    o.connect(g).connect(c.destination); o.start(t); o.stop(t + dur + .02);
  },
  hit(power, style) {
    if (!this.on || !this.ensure()) return; const t = this.ctx.currentTime, k = .45 + power * .2;
    const pitch = style === 'B' ? 1.8 : style === 'C' ? .8 : 1;
    this.tone(t, .16 + power * .04, 170 * pitch, 42 * pitch, .9 * k);
    this.noise(t, .06, 1900 * pitch, .9, .7 * k);
    this.noise(t, .12, 400 * pitch, .7, .5 * k, 'lowpass');
    if (style === 'B') this.tone(t + .01, .12, 900, 1500, .12, 'triangle');
  },
  whoosh(style) { if (!this.on || !this.ensure()) return; const t = this.ctx.currentTime; this.noise(t, .14, 700, 1.2, .18, 'bandpass', 2600); },
  slam(style) {
    if (!this.on || !this.ensure()) return; const t = this.ctx.currentTime;
    this.tone(t, .55, 95, 28, 1.1); this.noise(t, .4, 380, .6, .8, 'lowpass'); this.noise(t, .08, 2400, .8, .5);
    if (style === 'B') this.tone(t + .02, .2, 600, 1200, .15, 'triangle');
  },
  heat() { if (!this.on || !this.ensure()) return; const t = this.ctx.currentTime; this.tone(t, .35, 220, 880, .12, 'sawtooth'); this.noise(t, .3, 1200, .8, .12, 'bandpass', 5000); },
};

// ---------- camera: orbit + trauma shake + punch-in ----------
class CamRig {
  constructor(camera, S) { this.c = camera; this.az = .1; this.el = .14; this.tAz = .1; this.tEl = .14; this.trauma = 0; this.punch = 0; this.focus = new THREE.Vector3(); this.focusW = 0; this.setStyle(S); this.t = 0; }
  setStyle(S) { this.S = S; this.base = S.cam; }
  add(tr) { this.trauma = Math.min(1, this.trauma + tr * (REDUCED ? .3 : 1)); }
  update(rdt, aspect) {
    this.t += rdt;
    this.az += (this.tAz - this.az) * Math.min(1, rdt * 6); this.el += (this.tEl - this.el) * Math.min(1, rdt * 6);
    const b = this.base, half = Math.tan(THREE.MathUtils.degToRad(this.c.fov / 2)) * aspect;
    let d = Math.max(b.d, 1.55 / half) * (1 - this.punch * .1);
    this.punch *= Math.exp(-rdt * 5); this.focusW *= Math.exp(-rdt * 1.6);
    const tgt = new THREE.Vector3(0, b.look, 0).lerp(this.focus, this.focusW * .6);
    d *= 1 - this.focusW * .22;
    const pos = new THREE.Vector3(Math.sin(this.az) * Math.cos(this.el), Math.sin(this.el), Math.cos(this.az) * Math.cos(this.el)).multiplyScalar(d).add(tgt);
    pos.y += b.y - b.look - .1;
    const s = this.trauma * this.trauma, n = k => Math.sin(this.t * 61 * k) * .6 + Math.sin(this.t * 37 * k + 1.3) * .4;
    pos.x += s * .14 * n(1); pos.y += s * .1 * n(1.3); tgt.x += s * .05 * n(.7);
    this.c.position.copy(pos); this.c.lookAt(tgt); this.c.rotation.z += s * .05 * n(.9);
    this.trauma = Math.max(0, this.trauma - rdt * 1.8);
  }
}
