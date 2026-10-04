// ---------- director: moves, finishers ----------
const $ = id => document.getElementById(id);
const app = { S: null, f: {}, hitstop: 0, slow: 0, victim: null, timers: [], tweens: [], heat: { siwoo: .35, taeo: .35 }, introShown: false, demo: [], demoGap: 0 };
const other = who => (who === 'siwoo' ? 'taeo' : 'siwoo');
function after(t, fn) { app.timers.push({ t, fn }); }
function tween(dur, fn, done) { app.tweens.push({ t: 0, dur, fn, done }); }

function domPulse(el, cls, ms = 1200) { el.classList.remove(cls); void el.offsetWidth; el.classList.add(cls); clearTimeout(el._t); el._t = setTimeout(() => el.classList.remove(cls), ms); }
function screenFlash(op, color = '#ffffff') {
  if (REDUCED) return; const el = $('fx-flash'); el.style.background = color;
  el.animate([{ opacity: op }, { opacity: 0 }], { duration: 160, easing: 'ease-out' });
}
function bigWord(text) { const el = $('fx-big'); el.textContent = text; domPulse(el, 'on', 1150); }
function banner(text) { $('fx-banner-t').textContent = text; domPulse($('fx-banner'), 'on', 1000); if (!REDUCED && app.S.fx !== 'real') domPulse($('fx-lines'), 'on', 650); Sound.heat(); }
function nameCard(who) {
  const el = $('ncard'), info = who === 'taeo' ? ['혜성고 유도부 에이스 · 전국체전 우승', '강태오', 'blue'] : ['혜성고 2학년 3반 · 별명 행인1', '반시우', 'red'];
  $('ncard-s').textContent = info[0]; $('ncard-n').textContent = info[1]; el.dataset.c = info[2]; domPulse(el, 'on', 1900);
}

function resolveHit(A, V, spec, hand) {
  const S = app.S, power = spec.power;
  const kind = spec.kind === 'hook' ? 'hook' : spec.kind === 'upper' ? 'upper' : spec.zone;
  const hp = A.worldOf(hand === 'FR' ? 'ftR' : 'ha' + hand), vp = V.worldOf(spec.zone === 'body' ? 'chest' : spec.zone === 'leg' ? 'shL' : 'head');
  const pos = hp.lerp(vp, .45);
  const dir = new THREE.Vector3().subVectors(V.root.position, A.root.position).setY(0).normalize();
  app.hitstop = [0, .06, .095, .16][power] * (S.fx === 'real' ? 1.2 : 1); app.victim = V;
  app.cam.add([0, .28, .45, .8][power]); if (power >= 2) app.cam.punch = 1;
  V.hitFlinch(kind, .55 + power * .3); V.flash(); V.knock([0, .05, .1, .16][power]); V.setExpr('hurt', .45);
  app.fx.hit(pos, power, dir); app.fx.word(pos, spec.word, power);
  Sound.hit(power, S.key);
  if (power >= 2 && S.fx !== 'real') screenFlash(S.fx === 'cute' ? .22 : .32, S.fx === 'cute' ? '#ffe6f0' : '#ffffff');
  app.heat[A.def.id] = Math.min(1, app.heat[A.def.id] + .1 * power);
  app.cat && (app.cat.look = 1);
  if (window.__freezeOnHit) app.paused = true;
}

function runClips(A, V, names, speed, onEach, done) {
  const clips = A.clips; let i = 0;
  const next = () => {
    if (i >= names.length) { done && done(); return; }
    const c = Object.assign({}, clips[names[i]]); const idx = i++;
    c.onHit = spec => (onEach ? onEach(idx, c, spec) : resolveHit(A, V, spec, c.hand));
    c.onWhoosh = () => Sound.whoosh(app.S.key);
    c.onDone = next; A.whooshed = false; A.play(c, speed);
  };
  next();
}

function knockdown(V, then) {
  V.override = { pose: P(POSE_LIE), w: 0 }; V.setExpr('ko', 2.2);
  const lift = V.headR * .9 * (app.S.face === 'chibi' ? 1.2 : 1);
  tween(.42, u => { const e = easeIn(u); V.tiltA = -Math.PI / 2 * e; V.override.w = u; V.adj.y = lift * Math.sin(Math.PI / 2 * e); }, () => {
    app.cam.add(.5); app.fx.slam(V.worldOf('chest')); Sound.slam(app.S.key);
    after(1.0, () => getUp(V, -Math.PI / 2, then));
  });
}
function getUp(V, from, then) {
  V.setPivot(V.legL);
  const y0 = V.adj.y, lie = P(POSE_LIE), cr = P(POSE_CROUCH), tmp = new Float32Array(PLEN);
  tween(.5, u => { const e = easeInOut(u); V.tiltA = lerp(from, 0, e); V.adj.y = lerp(y0, 0, e); for (let k = 0; k < PLEN; k++) tmp[k] = lerp(lie[k], cr[k], e); V.override.pose = tmp; V.override.w = 1; }, () => {
    V.tiltA = 0; V.setPivot(0);
    const ax = V.adj.x, az = V.adj.z;
    tween(.35, u => { V.override.w = 1 - easeOut(u); V.adj.x = ax * (1 - u); V.adj.z = az * (1 - u); }, () => { V.override = null; V.adj.set(0, 0, 0); V.setExpr('normal'); then && then(); });
  });
}

function doRush(A, V, done) {
  banner('기세 액션 · 행인 러시'); app.heat.siwoo = 1; app.cam.focus.copy(V.worldOf('chest')); app.cam.focusW = 1;
  const seq = ['jab', 'cross', 'jab', 'cross', 'hook', 'upper'];
  runClips(A, V, seq, 1.35, (i, c, spec) => {
    const last = i === seq.length - 1;
    resolveHit(A, V, last ? spec : Object.assign({}, spec, { power: 1, word: ['퍽!', '빡!', '퍽!', '빡!', '퍽!'][i] }), c.hand);
    if (last) { app.slow = REDUCED ? 0 : .75; app.hitstop = .2; app.heat.siwoo = 0; bigWord('DOWN!'); knockdown(V, done); }
  }, null);
}

function doThrow(A, V, done) {
  banner('기세 액션 · 한판 업어치기'); app.heat.taeo = 1;
  const d = app.S.dist, xs = V.home.x, xt = A.home.x, z = A.home.z;
  const grab = P(POSE_GRAB), load = P(POSE_LOAD), kake = P(POSE_KAKE), pump = P(POSE_PUMP), fly = P(POSE_FLY), lie = P(POSE_LIE);
  const mix = (a, b, u) => { const o = new Float32Array(PLEN); for (let k = 0; k < PLEN; k++) o[k] = lerp(a[k], b[k], u); return o; };
  Sound.whoosh(app.S.key); V.setExpr('hurt', 3);
  A.override = { pose: grab, w: 0, yaw: true, root: f => f.pos.set(f._x, 0, z) }; A._x = xt;
  tween(.22, u => { A.override.w = u; A._x = lerp(xt, xs + .42 * d, easeOut(u)); }, () => {
    V.setPivot(V.legL); V.override = { pose: fly, w: 0, yaw: false, root: f => f.pos.set(f._cx, f._cy - f.pivot, z) };
    V._cx = xs; V._cy = V.legL;
    tween(.26, u => { const e = easeInOut(u); A.yaw = lerp(-Math.PI / 2, Math.PI / 2, e); A.override.pose = mix(grab, load, e); A._x = lerp(xs + .42 * d, xs + .2 * d, e); V._cx = lerp(xs, xs + .08 * d, e); V.tiltA = .45 * e; V.override.w = .6 * e; }, () => {
      const p0 = new THREE.Vector3(V._cx, V._cy, z), p1 = new THREE.Vector3(A._x, A.legL + A.torsoL + .5, z), p2 = new THREE.Vector3(A._x + V.def.H * .48 * (app.S.face === 'chibi' ? .8 : 1), V.headR * 1.1, z);
      Sound.whoosh(app.S.key);
      tween(.5, u => {
        const e = easeInOut(u), a = 1 - e, b = e;
        V._cx = a * a * p0.x + 2 * a * b * p1.x + b * b * p2.x; V._cy = a * a * p0.y + 2 * a * b * p1.y + b * b * p2.y;
        V.tiltA = lerp(.45, Math.PI * 1.5, e); V.override.pose = mix(fly, lie, Math.max(0, u - .6) / .4); V.override.w = .6 + .4 * u;
        A.override.pose = mix(load, kake, easeOut(u));
      }, () => {
        app.hitstop = .2; app.victim = V; app.cam.add(1); app.cam.punch = 1; app.slow = REDUCED ? 0 : .5;
        app.fx.slam(new THREE.Vector3(V._cx, 0, z)); if (app.S.word) app.fx.word(new THREE.Vector3(V._cx, .35, z), '쿵!', 3);
        Sound.slam(app.S.key); V.flash(); V.setExpr('ko', 2.4); bigWord('한판!'); A.setExpr('win', 1.6);
        if (app.S.fx !== 'real') screenFlash(.4);
        tween(.55, u => { A.override.pose = mix(kake, pump, easeOut(u)); }, () => {
          after(.55, () => {
            const cy0 = V._cy;
            tween(.5, u => { const e = easeInOut(u); V.tiltA = lerp(Math.PI * 1.5, TAU, e); V._cy = lerp(cy0, V.legL, easeOut(u)); V.override.pose = mix(lie, P(POSE_CROUCH), e); }, () => {
              V.tiltA = 0; V.setPivot(0);
              const el = $('fx-flash'); el.style.background = 'var(--stage)';
              el.animate([{ opacity: 0 }, { opacity: 1, offset: .45 }, { opacity: 1, offset: .55 }, { opacity: 0 }], { duration: 520 });
              after(.24, () => { A.override = null; V.override = null; A.yaw = A.facingYaw(); V.adj.set(0, 0, 0); A.kb = V.kb = 0; A.setExpr('normal'); V.setExpr('normal'); });
              after(.5, done);
            });
          });
        });
      });
    });
  });
}

const director = {
  busy: false, queue: [], idle: 0,
  request(who, move) {
    if (!app.f.siwoo) return;
    if (this.busy) { if (!this.queue.length) this.queue.push([who, move]); return; }
    this.start(who, move);
  },
  start(who, move) {
    const A = app.f[who], V = app.f[other(who)];
    this.busy = true; this.idle = 0;
    const wasShow = A.mode === 'show';
    A.setMode('fight'); V.setMode('fight');
    if (!app.introShown) { app.introShown = true; nameCard(other(who)); }
    const fin = () => { this.busy = false; if (this.queue.length) { const [w, m] = this.queue.shift(); this.start(w, m); } };
    after(wasShow ? .3 : 0, () => {
      if (move === 'rush') return doRush(A, V, fin);
      if (move === 'throw') return doThrow(A, V, fin);
      const names = { onetwo: ['jab', 'cross'], hook: ['hook'], lowkick: ['lowkick'], jab: ['jab'], body: ['body'] }[move];
      runClips(A, V, names, 1, null, fin);
    });
  },
  toShow() { this.queue = []; app.demo = []; for (const f of Object.values(app.f)) f.setMode('show'); app.introShown = false; },
  update(dt) {
    if (!this.busy && !app.demo.length) { this.idle += dt; if (this.idle > 6 && app.f.siwoo && app.f.siwoo.mode === 'fight') this.toShow(); }
  },
};

// ---------- scene / style switching ----------
let renderer, scene, camera, lights = [];
function setStyle(key) {
  const S = STYLES[key]; app.S = S;
  for (const f of Object.values(app.f)) f.dispose();
  app.env && app.env.dispose(); app.fx && app.fx.dispose(); lights.forEach(l => l.removeFromParent()); lights = [];
  app.timers = []; app.tweens = []; director.busy = false; director.queue = []; app.demo = []; app.introShown = false;
  renderer.toneMapping = S.aces ? THREE.ACESFilmicToneMapping : THREE.NoToneMapping; renderer.toneMappingExposure = 1.0;
  renderer.shadowMap.type = S.softShadow ? THREE.PCFSoftShadowMap : THREE.PCFShadowMap;
  const hemi = new THREE.HemisphereLight(S.hemi[0], S.hemi[1], S.hemi[2]); scene.add(hemi); lights.push(hemi);
  const sun = new THREE.DirectionalLight(S.sun[0], S.sun[1]); sun.position.set(...S.sun[2]); sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048); Object.assign(sun.shadow.camera, { left: -5, right: 5, top: 4, bottom: -2, near: .5, far: 30 }); sun.shadow.bias = -.0004; sun.shadow.normalBias = .02;
  sun.target.position.set(0, .8, 0); scene.add(sun, sun.target); lights.push(sun, sun.target);
  if (S.rim) { const rim = new THREE.DirectionalLight(S.rim[0], S.rim[1]); rim.position.set(...S.rim[2]); scene.add(rim); lights.push(rim); }
  const kit = S.ink && typeof inkKit !== 'undefined' ? inkKit(S) : materialKit(S);
  app.env = buildEnv(S, kit, scene); app.cat = app.env.cat; app.cat.look = 0;
  app.fx = new FX(scene, S);
  for (const id of ['siwoo', 'taeo']) {
    const Cls = S.stylish && typeof StylishFighter !== 'undefined' ? StylishFighter : Fighter;
    const f = new Cls(FIGHTERS[id], S, scene); app.f[id] = f;
  }
  // aim straight punches at the opponent's head height
  for (const id of ['siwoo', 'taeo']) {
    const A = app.f[id], V = app.f[other(id)];
    const sy = A.legL + A.torsoL * .92, hy = V.legL + V.torsoL + V.neckL + V.headR, dx = Math.abs(A.home.x - V.home.x);
    A.clips = attackClips(clamp(Math.atan2(hy - sy, dx * .9), -.45, .45), A.fightPoseDef);
  }
  app.cam.setStyle(S);
  document.querySelectorAll('.tab').forEach(t => t.setAttribute('aria-selected', String(t.dataset.style === key)));
  document.querySelectorAll('.card').forEach(c => c.classList.toggle('on', c.dataset.style === key));
  $('stage').dataset.style = key;
}

// ---------- page content ----------
function renderCards() {
  const dots = n => '●'.repeat(n) + `<span class="off">${'●'.repeat(5 - n)}</span>`;
  $('cards').innerHTML = Object.values(STYLES).map(S => `
    <article class="card" data-style="${S.key}">
      <div class="top"><span class="L">${S.key}</span><h3>${S.name}</h3></div>
      <p class="one">${S.card.one}</p>
      <div class="sw" aria-label="대표 색">${S.card.swatch.map(c => `<i style="background:${c}"></i>`).join('')}</div>
      <ul class="meters">${S.card.meters.map(([k, v]) => `<li><span>${k}</span><b aria-label="5점 중 ${v}점">${dots(v)}</b></li>`).join('')}</ul>
      <dl class="kv"><dt>레퍼런스</dt><dd>${S.card.ref}</dd><dt>제작 방식</dt><dd>${S.card.pipe}</dd><dt>잘 맞는 점</dt><dd>${S.card.fit}</dd><dt>주의할 점</dt><dd>${S.card.care}</dd></dl>
      <button class="pick" data-style="${S.key}" id="pick-${S.key}">스테이지에서 보기</button>
    </article>`).join('');
}
function renderTape() {
  const rows = [['나이', '17', '17'], ['키', '162cm', '183cm'], ['몸무게', '48kg', '88kg'], ['리치', '163cm', '191cm'], ['베이스', '길거리 · 복싱', '유도 2단'], ['링네임', 'EXTRA', 'IPPON'], ['기세 액션', '행인 러시', '한판 업어치기'], ['최종 체급', '플라이급 56.7kg', '라이트헤비급 93kg']];
  $('tape').innerHTML = `<div class="h lv"><div class="name">반시우</div><div class="sub">RED CORNER</div></div><div class="h mid">VS</div><div class="h rv"><div class="name">강태오</div><div class="sub">BLUE CORNER</div></div>` +
    rows.map(([k, a, b]) => `<div class="lv">${a}</div><div class="mid">${k}</div><div class="rv">${b}</div>`).join('');
}

// ---------- boot ----------
async function boot() {
  renderCards(); renderTape();
  const canvas = $('gl');
  try { renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' }); }
  catch (e) { $('loading').textContent = '이 브라우저에서는 3D(WebGL)를 켤 수 없습니다. 다른 브라우저로 열어 주세요.'; return; }
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2)); renderer.shadowMap.enabled = true;
  scene = new THREE.Scene(); camera = new THREE.PerspectiveCamera(34, 16 / 9, .1, 400);
  app.cam = new CamRig(camera, STYLES.A);
  if (document.fonts) await Promise.race([Promise.all(['Black Han Sans', 'Jua', 'Gothic A1'].map(f => document.fonts.load(`40px "${f}"`))), new Promise(r => setTimeout(r, 1800))]).catch(() => { });
  let start = 'A'; try { start = (window.claude?.hot?.data?.style) || localStorage.getItem('hy-style') || 'A'; } catch (e) { }
  if (!STYLES[start]) start = 'A';
  setStyle(start);
  $('loading').hidden = true;
  window.claude?.hot?.snapshot?.(() => ({ style: app.S.key }));

  const stage = $('stage');
  const resize = () => { const r = stage.getBoundingClientRect(); if (!r.width) return; renderer.setSize(r.width, r.height, false); camera.aspect = r.width / r.height; camera.updateProjectionMatrix(); };
  new ResizeObserver(resize).observe(stage); resize();
  let visible = true; new IntersectionObserver(es => { visible = es[0].isIntersecting; }).observe(stage);

  // orbit drag
  let drag = null;
  canvas.addEventListener('pointerdown', e => { drag = { x: e.clientX, y: e.clientY, id: e.pointerId }; canvas.setPointerCapture(e.pointerId); $('hint').hidden = true; });
  canvas.addEventListener('pointermove', e => { if (!drag) return; const c = app.cam; c.tAz = clamp(c.tAz - (e.clientX - drag.x) * .006, -1.2, 1.2); if (e.pointerType === 'mouse') c.tEl = clamp(c.tEl + (e.clientY - drag.y) * .004, .02, .55); drag.x = e.clientX; drag.y = e.clientY; });
  const end = () => { drag = null; }; canvas.addEventListener('pointerup', end); canvas.addEventListener('pointercancel', end);
  canvas.addEventListener('dblclick', () => { app.cam.tAz = .1; app.cam.tEl = .14; });

  // UI
  const choose = k => { setStyle(k); try { localStorage.setItem('hy-style', k); } catch (e) { } };
  document.querySelectorAll('.tab').forEach(t => t.addEventListener('click', () => choose(t.dataset.style)));
  $('cards').addEventListener('click', e => { const b = e.target.closest('.pick'); if (b) { choose(b.dataset.style); stage.scrollIntoView({ behavior: REDUCED ? 'auto' : 'smooth', block: 'center' }); } });
  document.querySelectorAll('.mv').forEach(b => b.addEventListener('click', () => { Sound.ensure(); app.demo = []; director.request(b.dataset.who, b.dataset.move); }));
  const keys = { q: ['siwoo', 'onetwo'], w: ['siwoo', 'hook'], e: ['siwoo', 'lowkick'], r: ['siwoo', 'rush'], u: ['taeo', 'jab'], i: ['taeo', 'body'], o: ['taeo', 'throw'] };
  window.addEventListener('keydown', e => {
    if (e.metaKey || e.ctrlKey || e.altKey) return; const k = e.key.toLowerCase();
    if (keys[k]) { Sound.ensure(); director.request(...keys[k]); } else if (k === '1' || k === '2' || k === '3') choose('ABC'[+k - 1]);
  });
  $('btn-demo').addEventListener('click', () => { Sound.ensure(); app.demo = [['siwoo', 'onetwo'], ['taeo', 'jab'], ['siwoo', 'hook'], ['taeo', 'body'], ['siwoo', 'lowkick'], ['siwoo', 'rush'], ['taeo', 'throw']]; app.demoGap = 0; });
  $('btn-sound').addEventListener('click', e => { Sound.on = !Sound.on; e.currentTarget.textContent = Sound.on ? '소리 켜짐' : '소리 꺼짐'; e.currentTarget.setAttribute('aria-pressed', String(Sound.on)); });
  $('btn-pose').addEventListener('click', () => director.toShow());

  // loop
  let last = performance.now();
  const frame = now => {
    requestAnimationFrame(frame);
    const rdt = Math.min((now - last) / 1000, 1 / 20); last = now;
    if (!visible || document.hidden) return;
    if (app.paused) { renderer.render(scene, camera); return; }
    let dt = rdt;
    if (app.hitstop > 0) { app.hitstop -= rdt; dt = 0; } else if (app.slow > 0) { app.slow -= rdt; dt = rdt * .3; }
    for (let i = app.timers.length - 1; i >= 0; i--) { const t = app.timers[i]; t.t -= dt; if (t.t <= 0) { app.timers.splice(i, 1); t.fn(); } }
    for (let i = app.tweens.length - 1; i >= 0; i--) { const t = app.tweens[i]; if (!t) continue; t.t += dt; const u = Math.min(1, t.t / t.dur); t.fn(u); if (u >= 1) { app.tweens.splice(app.tweens.indexOf(t), 1); t.done && t.done(); } }
    for (const f of Object.values(app.f)) { f.jitter = app.hitstop > 0 && f === app.victim ? 1 : 0; f.update(dt, rdt); }
    director.update(dt);
    if (app.demo.length && !director.busy) { app.demoGap += rdt; if (app.demoGap > .45) { app.demoGap = 0; director.request(...app.demo.shift()); } }
    app.fx.update(app.hitstop > 0 ? rdt * .15 : app.slow > 0 ? rdt * .45 : rdt);
    if (app.cat) { const c = app.cat; c.look = Math.max(0, c.look - rdt * .5); c.head.rotation.y = c.look * -.9 + Math.sin(now / 1300) * .15; c.tail.rotation.z = Math.sin(now / 260) * .35; }
    for (const id of ['siwoo', 'taeo']) { app.heat[id] = Math.max(.15, app.heat[id] - rdt * .02); $('heat-' + id).style.transform = `scaleX(${app.heat[id].toFixed(3)})`; }
    app.cam.update(rdt, camera.aspect);
    renderer.render(scene, camera);
  };
  requestAnimationFrame(frame);
  window.__hy = { app, setStyle, director, STYLES };
}
boot();
