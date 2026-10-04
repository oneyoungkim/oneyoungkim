// ---------- particles / impact effects ----------
class FX {
  constructor(scene, S) { this.S = S; this.group = new THREE.Group(); scene.add(this.group); this.items = []; this.T = fxTex(); }
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
  slam(pos) {
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
