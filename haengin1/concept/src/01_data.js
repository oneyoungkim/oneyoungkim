// ---------- helpers ----------
const TAU = Math.PI * 2;
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerp = (a, b, t) => a + (b - a) * t;
const easeOut = t => 1 - Math.pow(1 - t, 3);
const easeIn = t => t * t * t;
const easeInOut = t => (t < .5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);
const backOut = t => { const c1 = 1.70158, c3 = c1 + 1; return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2); };
function mulberry(seed) { return function () { let t = seed += 0x6D2B79F5; t = Math.imul(t ^ t >>> 15, t | 1); t ^= t + Math.imul(t ^ t >>> 7, t | 61); return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const REDUCED = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

// ---------- art styles ----------
const STYLES = {
  A: {
    key: 'A', name: '애니 셀셰이딩',
    heads: { siwoo: 6.3, taeo: 6.8 },
    body: { neck: .22, torso: 1.52, shoulder: .80, hip: .52, depth: .62, upperArm: 1.08, foreArm: .98, handR: .17, armR: .15, foreR: .13, thighR: .27, shinR: .20, footL: .95, squash: 1.08, jaw: .26, hair: 1.08 },
    shading: 'toon', ramp: [128, 255],
    outline: { w: .011, color: '#1c1a2e', tint: false },
    face: 'anime', tone: 'vivid',
    sky: ['#3f8fe8', '#a9d6ff', '#eaf5ff'], fog: ['#d6eaff', 30, 140],
    hemi: ['#eef6ff', '#a49e8e', 1.0], sun: ['#fff7ea', 2.2, [-3, 9, 7]], rim: null,
    aces: false, softShadow: false,
    env: { ground: '#d2cbbd', tileA: '#e6dfd1', tileB: '#dad2c2', mortar: '#b9b09f', stone: '#dcd6c9', stoneVar: .07, leaf: '#ffcc33', trunk: '#7a5a40', lamp: '#3a3f4d', glow: 0, city: '#a6b8cd', win: null, box: '#e8572a', cat: '#f2a03d', sign: '#7b5536' },
    fx: 'anime', word: { font: 'Black Han Sans', fill: '#ffd640', stroke: '#1c1a2e', shadow: '#e8572a' },
    dist: 1.0, cam: { d: 4.3, y: 1.35, look: .98 },
    card: {
      one: '웹툰 컷이 그대로 움직이는 느낌. 굵은 외곽선, 2단 명암, 쨍한 색.',
      swatch: ['#2b3566', '#e8572a', '#ffcc33', '#a9d6ff', '#1c1a2e'],
      meters: [['제작 난이도', 2], ['1인 개발 속도', 5], ['타격 무게감', 3], ['캐주얼함', 4]],
      ref: '하이파이 러시, 젠레스 존 제로, 원신',
      pipe: 'VRoid Studio로 캐릭터 → Blender 보정 → Unity + 툰 셰이더(lilToon) · UniVRM',
      fit: '웹소설 감성과 제일 잘 맞고, 캐릭터를 가장 빨리 많이 만들 수 있음',
      care: '애니풍이 흔해서 배경 톤과 외곽선 색으로 개성을 잡아야 함',
    },
  },
  B: {
    key: 'B', name: 'SD 치비',
    heads: { siwoo: 2.75, taeo: 3.05 },
    body: { neck: .03, torso: .64, shoulder: .27, hip: .22, depth: .78, upperArm: .32, foreArm: .29, handR: .105, armR: .075, foreR: .07, thighR: .125, shinR: .105, footL: .32, squash: .94, jaw: 0, hair: 1.12 },
    shading: 'toon', ramp: [170, 214, 255],
    outline: { w: .014, color: null, tint: true },
    face: 'chibi', tone: 'pastel',
    sky: ['#9d93ee', '#ffb4c6', '#ffe6cc'], fog: ['#ffd7cf', 26, 120],
    hemi: ['#fff1f4', '#c9a9b8', 1.6], sun: ['#ffe8d6', 1.6, [4, 8, 7]], rim: null,
    aces: false, softShadow: true,
    env: { ground: '#f0d6c8', tileA: '#f8e6d9', tileB: '#f1dccd', mortar: '#d9bcae', stone: '#e8d5d0', stoneVar: .05, leaf: '#ffd66b', trunk: '#a77b5e', lamp: '#7d6b8f', glow: 1, city: '#c7b3da', win: '#fff3b0', box: '#ff8a6b', cat: '#ffb057', sign: '#b4876a' },
    fx: 'cute', word: { font: 'Jua', fill: '#ff86ad', stroke: '#ffffff', shadow: '#7d6b8f' },
    dist: .8, cam: { d: 4.1, y: 1.3, look: .92 },
    card: {
      one: '2.5등신 장난감 피규어. 파스텔, 둥근 실루엣. 귀여운데 때리면 시원하다.',
      swatch: ['#7d6b8f', '#ff8a6b', '#ffd66b', '#ffb4c6', '#9d93ee'],
      meters: [['제작 난이도', 2], ['1인 개발 속도', 4], ['타격 무게감', 2], ['캐주얼함', 5]],
      ref: '파티 애니멀즈, 폴 가이즈, 카트라이더 캐릭터',
      pipe: 'Blender 직접 모델링(형태가 단순) → 애니메이션은 비율 때문에 직접 키프레임 비중 큼',
      fit: '가장 밝고 캐주얼, 모바일에 유리, 굿즈·이모티콘으로 확장하기 좋음',
      care: '올림픽 좌절·타이틀전 같은 진지한 장면의 무게감이 약해질 수 있음',
    },
  },
  C: {
    key: 'C', name: '스타일라이즈드',
    heads: { siwoo: 6.9, taeo: 7.3 },
    body: { neck: .30, torso: 1.74, shoulder: .92, hip: .56, depth: .62, upperArm: 1.22, foreArm: 1.10, handR: .24, armR: .19, foreR: .17, thighR: .32, shinR: .24, footL: 1.2, squash: 1.1, jaw: .16, hair: 1.04 },
    shading: 'pbr',
    outline: null,
    face: 'real', tone: 'warm',
    sky: ['#2c477e', '#ea9a6c', '#ffd7a3'], fog: ['#f2b98c', 24, 130],
    hemi: ['#a9c2ff', '#7a5a43', .75], sun: ['#ffb36e', 3.4, [-6, 3.6, 4.5]], rim: ['#9fc4ff', 2.2, [5, 4, -6]],
    aces: true, softShadow: true,
    env: { ground: '#8d877c', tileA: '#a39b8d', tileB: '#968e80', mortar: '#6f685d', stone: '#bdb3a4', stoneVar: .1, leaf: '#e9b437', trunk: '#5e4634', lamp: '#2e3138', glow: 1, city: '#5d6479', win: '#ffd08a', box: '#c94a26', cat: '#d98b3a', sign: '#6b4a32' },
    fx: 'real', word: null,
    dist: 1.0, cam: { d: 4.2, y: 1.38, look: 1.0 },
    card: {
      one: '반실사 비율에 손발을 크게 과장. 골든아워 조명, 묵직한 타격.',
      swatch: ['#2c477e', '#c94a26', '#e9b437', '#ea9a6c', '#bdb3a4'],
      meters: [['제작 난이도', 4], ['1인 개발 속도', 2], ['타격 무게감', 5], ['캐주얼함', 3]],
      ref: '시푸(Sifu), 포트나이트, 용과 같이 시리즈의 조명',
      pipe: 'Blender 모델링·텍스처링 → Mixamo/Cascadeur 애니메이션을 그대로 쓰기 좋음',
      fit: '실제 혜화동 재현과 격투의 무게감이 가장 잘 삶',
      care: '제작 시간이 가장 길고, 너무 어두워지지 않게 색 관리가 필요',
    },
  },
};

// ---------- fighters ----------
const FIGHTERS = {
  siwoo: {
    id: 'siwoo', name: '반시우', H: 1.62, build: .86, side: -1,
    skin: '#cf9670', hair: '#1b1720', hairStyle: 'curly', iris: '#3a2416',
    blazer: '#2b3566', shirt: '#f3f1ea', pants: '#7d8494', tie: '#a72c3c', tieStripe: '#e7c55a',
    shoes: '#f1f0ec', sole: '#e8572a', tape: '#f6f2e6', blazerOn: true, bandaid: true,
  },
  taeo: {
    id: 'taeo', name: '강태오', H: 1.83, build: 1.2, side: 1,
    skin: '#efc6a2', hair: '#14151b', hairSide: '#4a4d5a', hairStyle: 'twoblock', iris: '#4a2f1c',
    blazer: '#2b3566', shirt: '#f7f7f3', pants: '#7d8494', tie: '#a72c3c', tieStripe: '#e7c55a',
    shoes: '#222a38', sole: '#1d5bd0', blazerOn: false, cauli: true, arm: 1.12,
  },
};

// ---------- poses ----------
// joint order; values are Euler [x,y,z]. extras: drop (fraction of leg length), fwd (m, along facing)
const JN = ['hips', 'spine', 'chest', 'neck', 'head', 'uaL', 'faL', 'haL', 'uaR', 'faR', 'haR', 'thL', 'shL', 'ftL', 'thR', 'shR', 'ftR'];
const NJ = JN.length, PLEN = NJ * 3 + 2;
function P(o) {
  const a = new Float32Array(PLEN);
  JN.forEach((n, i) => { const v = o[n]; if (v) { a[i * 3] = v[0]; a[i * 3 + 1] = v[1]; a[i * 3 + 2] = v[2]; } });
  a[NJ * 3] = o.drop || 0; a[NJ * 3 + 1] = o.fwd || 0;
  return a;
}
const ov = (base, o) => Object.assign({}, base, o);

const STANCE = {
  hips: [0, -.5, 0], spine: [.05, .08, 0], chest: [.08, .12, 0], neck: [0, .1, 0], head: [.16, .2, 0],
  uaL: [-.78, .15, .38], faL: [-1.85, 0, 0], haL: [.15, 0, 0],
  uaR: [-.52, -.1, -.36], faR: [-2.25, 0, 0], haR: [.2, 0, 0],
  thL: [-.36, .3, .06], shL: [.34, 0, 0], ftL: [.02, .2, 0],
  thR: [.3, .25, -.12], shR: [.36, 0, 0], ftR: [-.22, .35, 0],
  drop: .07,
};
const SHOW = {
  siwoo: {
    hips: [0, -.22, 0], spine: [.04, .05, 0], chest: [.07, .06, 0], neck: [0, .05, 0], head: [.12, .1, -.04],
    uaL: [-.6, .1, .42], faL: [-1.95, 0, 0], haL: [.2, 0, 0],
    uaR: [-.48, -.1, -.42], faR: [-2.15, 0, 0], haR: [.2, 0, 0],
    thL: [-.16, .1, .12], shL: [.2, 0, 0], ftL: [0, .1, 0],
    thR: [.12, .1, -.12], shR: [.2, 0, 0], ftR: [-.1, .2, 0], drop: .04,
  },
  taeo: {
    hips: [0, .08, 0], chest: [-.04, -.05, 0], head: [-.04, -.06, .06],
    uaL: [.05, 0, .28], faL: [-.35, 0, 0], haL: [0, 0, 0],
    uaR: [-.42, 0, -.18], faR: [-1.2, 0, 0], haR: [0, 0, 0],
    thL: [0, 0, .1], shL: [.04, 0, 0], thR: [0, 0, -.1], shR: [.04, 0, 0], drop: .0,
  },
};

// attack keyframes (overrides on STANCE). `aim` lifts/lowers straight punches toward the opponent's head.
function attackClips(aim) {
  const S = STANCE;
  return {
    jab: { dur: .34, keys: [[0, S], [.05, ov(S, { uaL: [-1.0, .15, .3], chest: [.08, .02, 0] })], [.11, ov(S, { uaL: [-1.57 - aim, .12, .06], faL: [-.06, 0, 0], haL: [0, 0, 0], chest: [.05, -.32, 0], hips: [0, -.62, 0], head: [.16, .38, 0], fwd: .1 })], [.19, ov(S, { uaL: [-1.45 - aim, .12, .08], faL: [-.3, 0, 0], fwd: .08 })], [.34, S]], hit: [.11, { zone: 'head', power: 1, word: '퍽!' }], hand: 'L', whoosh: .03 },
    cross: { dur: .4, keys: [[0, S], [.06, ov(S, { hips: [0, -.62, 0], chest: [.1, .0, 0] })], [.14, ov(S, { uaR: [-1.57 - aim, -.12, -.05], faR: [-.06, 0, 0], haR: [0, 0, 0], chest: [.1, .5, 0], hips: [0, .08, 0], head: [.16, -.25, 0], thR: [.28, .7, -.1], ftR: [-.4, .9, 0], uaL: [-.6, .2, .3], faL: [-2.1, 0, 0], fwd: .16 })], [.24, ov(S, { uaR: [-1.4 - aim, -.1, -.1], faR: [-.4, 0, 0], chest: [.1, .4, 0], fwd: .12 })], [.4, S]], hit: [.14, { zone: 'head', power: 2, word: '빡!' }], hand: 'R', whoosh: .05 },
    hook: { dur: .46, keys: [[0, S], [.1, ov(S, { chest: [.06, -.5, 0], hips: [0, -.72, 0], uaL: [-.95, 0, .95], faL: [-1.35, 0, 0] })], [.2, ov(S, { chest: [.06, .42, 0], hips: [0, -.12, 0], uaL: [-1.32 - aim * .5, 0, 1.05], faL: [-1.5, 0, 0], haL: [0, 0, 0], head: [.16, -.15, 0], fwd: .1 })], [.3, ov(S, { chest: [.06, .3, 0], uaL: [-1.1, 0, .8], faL: [-1.6, 0, 0], fwd: .06 })], [.46, S]], hit: [.2, { zone: 'head', power: 2, word: '퍽!', kind: 'hook' }], hand: 'L', whoosh: .1 },
    upper: { dur: .5, keys: [[0, S], [.12, ov(S, { drop: .2, chest: [.32, .1, 0], hips: [0, -.25, 0], uaR: [.1, 0, -.3], faR: [-1.9, 0, 0] })], [.22, ov(S, { drop: .02, chest: [-.3, .35, 0], hips: [0, .12, 0], uaR: [-1.35 - aim * .5, 0, -.12], faR: [-1.45, 0, 0], head: [-.05, -.1, 0], fwd: .12 })], [.34, ov(S, { chest: [-.15, .25, 0], uaR: [-1.5, 0, -.15], faR: [-1.2, 0, 0], fwd: .08 })], [.5, S]], hit: [.22, { zone: 'head', power: 3, word: '콰직!', kind: 'upper' }], hand: 'R', whoosh: .14 },
    lowkick: { dur: .56, keys: [[0, S], [.12, ov(S, { hips: [0, -.2, 0], drop: .1, uaR: [.25, 0, -.4], faR: [-1.6, 0, 0] })], [.24, ov(S, { hips: [0, .95, 0], chest: [.0, -.45, 0], thR: [-.9, 0, -.9], shR: [.12, 0, 0], ftR: [.55, 0, 0], thL: [-.1, 0, .05], shL: [.26, 0, 0], uaR: [.55, 0, -.45], faR: [-1.1, 0, 0], uaL: [-.7, .2, .35], faL: [-2.0, 0, 0], head: [.16, .3, 0], fwd: .12, drop: .05 })], [.36, ov(S, { hips: [0, .6, 0], thR: [-.5, 0, -.5], shR: [.6, 0, 0], fwd: .08 })], [.56, S]], hit: [.24, { zone: 'leg', power: 2, word: '콰직!', kind: 'kick' }], hand: 'FR', whoosh: .12 },
    body: { dur: .46, keys: [[0, S], [.1, ov(S, { drop: .14, hips: [0, -.65, 0], chest: [.2, -.05, 0] })], [.2, ov(S, { drop: .2, chest: [.38, .42, 0], hips: [0, .05, 0], uaR: [-1.12, -.1, -.08], faR: [-.12, 0, 0], haR: [0, 0, 0], head: [.1, -.25, 0], fwd: .16 })], [.3, ov(S, { drop: .15, chest: [.3, .3, 0], uaR: [-1.0, -.1, -.1], faR: [-.5, 0, 0], fwd: .1 })], [.46, S]], hit: [.2, { zone: 'body', power: 2, word: '퍽!' }], hand: 'R', whoosh: .08 },
  };
}

// throw / knockdown poses
const POSE_GRAB = ov(STANCE, { hips: [0, -.2, 0], chest: [.15, .1, 0], uaL: [-1.35, 0, .15], faL: [-.45, 0, 0], uaR: [-1.3, 0, -.15], faR: [-.5, 0, 0], head: [.1, .1, 0], fwd: .05 });
const POSE_LOAD = { hips: [0, 0, 0], spine: [.15, 0, 0], chest: [.3, 0, 0], head: [.2, 0, 0], uaR: [-2.3, 0, -.25], faR: [-1.3, 0, 0], uaL: [-1.05, 0, .3], faL: [-.9, 0, 0], thL: [-.55, 0, .22], shL: [.95, 0, 0], ftL: [-.35, 0, 0], thR: [-.55, 0, -.22], shR: [.95, 0, 0], ftR: [-.35, 0, 0], drop: .28 };
const POSE_KAKE = { hips: [0, 0, 0], spine: [.45, 0, 0], chest: [.75, 0, 0], head: [.35, 0, 0], uaR: [-.7, 0, -.2], faR: [-.6, 0, 0], uaL: [-.55, 0, .25], faL: [-.5, 0, 0], thL: [-.75, 0, .22], shL: [1.1, 0, 0], ftL: [-.35, 0, 0], thR: [-.45, 0, -.22], shR: [.8, 0, 0], ftR: [-.3, 0, 0], drop: .3 };
const POSE_PUMP = ov(SHOW.taeo, { uaR: [-2.75, 0, -.25], faR: [-.5, 0, 0], chest: [-.1, 0, 0], head: [-.18, 0, 0], drop: .02 });
const POSE_FLY = { chest: [.1, 0, 0], head: [-.25, 0, 0], uaL: [-2.4, 0, .6], faL: [-.4, 0, 0], uaR: [-2.4, 0, -.6], faR: [-.4, 0, 0], thL: [-.7, 0, .15], shL: [1.0, 0, 0], thR: [-.35, 0, -.15], shR: [.6, 0, 0] };
const POSE_LIE = { head: [-.1, .25, 0], uaL: [0, 0, 1.35], faL: [-.35, 0, 0], uaR: [0, 0, -1.3], faR: [-.3, 0, 0], thL: [-.18, 0, .12], shL: [.35, 0, 0], ftL: [.5, 0, 0], thR: [-.05, 0, -.1], shR: [.15, 0, 0], ftR: [.5, 0, 0] };
const POSE_CROUCH = { chest: [.45, 0, 0], head: [-.1, 0, 0], uaL: [-.7, 0, .4], faL: [-1.4, 0, 0], uaR: [-.7, 0, -.4], faR: [-1.4, 0, 0], thL: [-1.35, 0, .16], shL: [2.0, 0, 0], ftL: [-.6, 0, 0], thR: [-.85, 0, -.16], shR: [1.7, 0, 0], ftR: [-.75, 0, 0], drop: .42 };

// additive flinch presets (scaled by power)
const FLINCH = {
  head: { head: [-.5, 0, 0], neck: [-.2, 0, 0], chest: [-.25, 0, 0], spine: [-.1, 0, 0] },
  hook: { head: [-.15, .75, .25], neck: [0, .2, 0], chest: [0, .32, -.08] },
  upper: { head: [-.9, 0, 0], neck: [-.35, 0, 0], chest: [-.42, 0, 0], spine: [-.15, 0, 0] },
  body: { chest: [.55, 0, 0], spine: [.3, 0, 0], head: [.25, 0, 0], uaL: [.4, 0, 0], uaR: [.4, 0, 0], drop: .1 },
  leg: { thL: [.3, 0, -.3], shL: [.45, 0, 0], hips: [0, 0, -.14], chest: [-.1, 0, .12], drop: .08 },
};
