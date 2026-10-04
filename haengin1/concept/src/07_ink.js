// ---------- ink kit: A 스타일리시 셀(color) / A′ 잉크 망가(mono) ----------
// Same shape as materialKit(S): solid / outline / part / flashables (+ hull helpers for skinned parts).
// Shading is rebuilt after three's toon lighting: light amount L = N·L × shadow (recovered in RE_Direct),
// then 2-tone notan + screen-space hatching / screentone / rim, keeping map, instanceColor, shadows and fog.
const INK_ROLE = { light: 0, dark: 1, skin: 2, face: 3, glow: 4, spot: 5, mid: 6 };
const inkLum = c => .2126 * c.r + .7152 * c.g + .0722 * c.b; // linear relative luminance

const INK_PARS = /* glsl */`
uniform vec3 uInk;
uniform vec3 uPaper;
uniform float uPx;
uniform vec3 uHatch;   // spacing(px), width(px), angle(rad)
uniform float uTone;   // screentone cell (px)
uniform float uRimK;
uniform float uRole;
uniform float uMono;
uniform float uSpot;
uniform float uMix;
uniform float uTh;
uniform float uGloss;
uniform float uHi;
uniform float uRimM;
uniform float uFold;
uniform float uFoldF;
uniform float uCel;       // 1 = character cel path (A: multiply shadow, no hatch / rim)
uniform vec3 uShade;      // per-role shadow multiplier (bible 6-3)
uniform vec3 uShadeCol;   // explicit shadow colour (linear) when uUseShadeCol = 1
uniform float uUseShadeCol;
uniform float uRimOn;
varying vec3 vInkObj;
// hanging-cloth fold field (object space, streaks along local Y)
float ink_folds(vec3 p) {
  float a = sin(p.x + sin(p.y * .23) * 2.1 + sin(p.z * .7) * 1.3);
  float b = sin(p.z * 1.31 + p.x * .43 + sin(p.y * .31 + 1.7) * 1.8);
  float c = sin(p.y * .55 + p.x * .2) * .5 + .5;
  return (a * .6 + b * .4) * (.55 + .45 * c);
}
float ink_lum(vec3 c) { return dot(c, vec3(.2126, .7152, .0722)); }
float ink_hash(float n) { return fract(sin(n * 91.345) * 47453.5453); }
// parallel pen lines in screen space. w = line width in px; lines break into dashes of random length
float ink_hatch(float ang, float sp, float w) {
  vec2 d = vec2(cos(ang), sin(ang));
  float p = dot(gl_FragCoord.xy, d) / sp;
  float id = floor(p);
  float along = dot(gl_FragCoord.xy, vec2(-d.y, d.x)) / (sp * 7.0) + ink_hash(id) * 9.0;
  float gap = step(.86 + ink_hash(id + 3.7) * .1, fract(along));
  float dist = abs(fract(p) - .5) * sp;
  float c = clamp(w * .5 - dist + .5, 0.0, 1.0) * clamp(w, 0.0, 1.0);
  return c * (1.0 - gap * step(w, sp * .55));
}
// rotated dot screen. amt = ink coverage 0..1
float ink_tone(float cell, float amt) {
  vec2 q = mat2(.7071, -.7071, .7071, .7071) * gl_FragCoord.xy / cell;
  vec2 f = fract(q) - .5;
  float r = length(f) * cell;
  float rad = sqrt(clamp(amt, 0.0, .9) / 3.14159) * cell;
  return clamp(rad - r + .5, 0.0, 1.0) * step(.02, amt);
}
`;

const INK_TOON_PARS = /* glsl */`
varying vec3 vViewPosition;
struct ToonMaterial { vec3 diffuseColor; };
float inkLight = 0.0;
void RE_Direct_Toon( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
  float ndl = max( dot( geometryNormal, directLight.direction ), 0.0 );
  inkLight += ndl * dot( directLight.color, vec3( .2126, .7152, .0722 ) );
  reflectedLight.directDiffuse += ndl * directLight.color * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Toon( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
  reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct RE_Direct_Toon
#define RE_IndirectDiffuse RE_IndirectDiffuse_Toon
`;

const INK_SHADE = /* glsl */`
{
#if NUM_DIR_LIGHTS > 0
  float sunI = 0.0;
  for ( int i = 0; i < NUM_DIR_LIGHTS; i ++ ) sunI = max( sunI, ink_lum( directionalLights[ i ].color ) );
  vec3 sunV = directionalLights[ 0 ].direction;
#else
  float sunI = 1.0; vec3 sunV = vec3( 0.0, 1.0, 0.0 );
#endif
  float L = clamp( inkLight / max( sunI, 1e-4 ), 0.0, 1.0 );
  float aa = clamp( fwidth( L ), .002, .08 );
  vec3 base = diffuseColor.rgb;
  float fres = 1.0 - clamp( dot( geometryNormal, geometryViewDir ), 0.0, 1.0 );
  float sideL = dot( geometryNormal, sunV );
  float sp = uHatch.x * uPx, hw = uHatch.y * uPx, ang = uHatch.z;
  int role = int( uRole + .5 );
  float th = uTh;
  float lit = smoothstep( th - aa, th + aa, L );
  float band = ( 1.0 - smoothstep( th, th + .24, L ) ) * lit;          // just above the terminator
  float rimEdge = 1.0 - uRimK * .5;
  float rim = smoothstep( rimEdge - .03, rimEdge + .03, fres ) * smoothstep( -.15, .25, sideL ) * uRimM;
  vec3 col = base;
  bool spot = role == 5 && uSpot > .5;
  if ( role == 4 ) {
    col = base;
  } else if ( ( uMono < .5 || spot ) && uCel > .5 ) {
    // ---- A cel (characters): one crisp shadow tone = albedo × mauve multiplier (or a given shadow colour) ----
    vec3 sh = uUseShadeCol > .5 ? uShadeCol : base * uShade;
    float ac = min( aa, .02 );   // crisp terminator (bible 7-3)
    col = mix( sh, base, smoothstep( th - ac, th + ac, L ) );
    if ( uRimOn > .5 ) col = mix( col, mix( base, uPaper, .3 ), rim * .5 );
  } else if ( uMono < .5 || spot ) {
    // ---- A: stylish cel (background). shadow = albedo pushed toward ink ----
    vec3 sh = mix( base, uInk, uMix );
    col = mix( sh, base, lit );
    if ( role == 1 ) {
      float hl = smoothstep( uHi + .16 - aa, uHi + .16 + aa, L );
      col = mix( col, mix( base, uPaper, .16 ), hl * .8 );
      col = mix( col, mix( base, uPaper, .38 ), rim * .85 );
    } else if ( role != 3 ) {
      col = mix( col, mix( base, uInk, min( 1.0, uMix + .25 ) ), ink_hatch( ang, sp, hw * band ) * .6 );
      col = mix( col, mix( col, uPaper, .35 ), rim * .35 );
    }
  } else {
    // ---- A′: ink manga, paper + sumi ----
    if ( role == 1 ) {
      col = uInk;
      float hb = smoothstep( uHi, uHi + .24, L );
      col = mix( col, uPaper, ink_hatch( -ang, sp, hw * 1.1 * hb ) );
      col = mix( col, uPaper, smoothstep( uHi + .2 - aa, uHi + .2 + aa, L ) * .92 );
      col = mix( col, uPaper, rim );
    } else if ( role == 6 ) {
      float amt = mix( .5, .2, lit );
      col = mix( uPaper, uInk, ink_tone( uTone * uPx, amt ) );
      col = mix( col, uInk, ink_hatch( ang, sp, hw * band ) );
      col = mix( col, uPaper, rim * .9 );
    } else {
      float v = 1.0;
#ifdef USE_MAP
      if ( role != 3 ) { float lm = ink_lum( base ); v = lm > .42 ? 1.0 : ( lm > .1 ? .5 : 0.0 ); }
      else v = 1.0;
#endif
      float c;   // ink coverage
      if ( role == 3 ) {
        c = ink_hatch( ang, sp, hw * .6 ) * ( 1.0 - smoothstep( .05 - aa, .05 + aa, L ) );
        c = max( c, ink_hatch( ang, sp, hw * band * .45 ) );
      } else {
        float fill = role == 2 ? ink_tone( uTone * uPx, .42 ) : 1.0;   // skin: screentone, cloth: solid sumi
        c = max( fill * ( 1.0 - lit ), ink_hatch( ang, sp, hw * band ) );
      }
      if ( v < .25 ) c = 1.0;
      else if ( v < .75 ) c = max( c, ink_tone( uTone * uPx, .38 ) );
      vec3 pap = role == 2 ? mix( uPaper, uInk, .1 ) : uPaper;   // skin: flat 10% grey so it parts from the white shirt
#ifdef USE_MAP
      if ( role == 3 ) { col = mix( base, uInk, c ); }
      else col = mix( pap, uInk, c );
#else
      col = mix( pap, uInk, c );
#endif
    }
  }
  if ( uGloss > 0.0 && role != 4 ) {
    // enamel: a hard white chip where the sun glints (loafers)
    float spec = dot( geometryNormal, normalize( sunV + geometryViewDir ) );
    float g0 = 1.0 - .045 * uGloss;
    col = mix( col, uPaper, smoothstep( g0 - .006, g0 + .006, spec ) * step( .05, L ) );
  }
  outgoingLight = col + totalEmissiveRadiance;
}
`;

const INK_FOLD = /* glsl */`
if ( uFold > 0.0 ) {
  float h = ink_folds( vInkObj * uFoldF ) * uFold;
  vec3 sx = dFdx( - vViewPosition ), sy = dFdy( - vViewPosition );
  vec3 r1 = cross( sy, normal ), r2 = cross( normal, sx );
  float det = dot( sx, r1 ) * faceDirection;
  vec3 grad = sign( det ) * ( dFdx( h ) * r1 + dFdy( h ) * r2 );
  normal = normalize( abs( det ) * normal - grad );
}
`;

const INK_HULL_VERT = /* glsl */`
#include <begin_vertex>
{
  float inkOe = inkO;
#ifdef INK_P
  // head profile line: the nose / lip hull (0 face-on, so no dark wedge under the nose) grows back to the profile
  // weight as the head turns away from the camera — the G-pen line from brow over nose and lips to chin in 3/4 / side
  vec3 hU = normalize( mat3( modelMatrix ) * vec3( 0.0, 1.0, 0.0 ) );
  vec3 hF = mat3( modelMatrix ) * vec3( 0.0, 0.0, 1.0 ), toC = cameraPosition - modelMatrix[ 3 ].xyz;
  hF -= hU * dot( hF, hU ); toC -= hU * dot( toC, hU );
  float turn = 1.0 - smoothstep( .64, .95, dot( normalize( hF ), normalize( toC ) ) );
  inkOe += inkQ * turn;
  transformed += normal * inkQ * turn;
#endif
  vec3 inkWn = normalize( mat3( modelMatrix ) * normal );
  float inkSc = max( length( modelMatrix[ 0 ].xyz ), 1e-4 );
  float inkSh = smoothstep( -.35, .55, - dot( inkWn, uSun ) );
  // the shadow-side extra follows the part's own weight (ow × inkW × wobble, baked into inkO): thin inner parts
  // (hair clumps, hems, tape) stay thin and do not poke through the layer over them
  inkSh *= clamp( inkOe * inkSc / uBase, 0.0, 1.6 );
  // close-up: scale the whole hull (baked inkO + shadow-side extra) by view depth / game camera distance,
  // so faces and hair clumps do not get 10 px borders in an 85 mm shot (line hierarchy, bible 3-3 / 7-4)
  float inkD = - ( modelViewMatrix * vec4( position, 1.0 ) ).z;
  float inkK = clamp( inkD / uRefD, uMinK, 1.0 );
  transformed += normal * ( uExtra * inkSh * inkK / inkSc - inkOe * ( 1.0 - inkK ) );
}
`;

// character cel colour (A): style tone + film desaturation (bible 6-4, ink.sat). Used by kit.solid({cel}) and the face textures
function celColor(S, hex) {
  const c = styleColor(S, hex), I = S.ink || {};
  if (I.sat && I.mode !== 'mono') { const h = {}; c.getHSL(h); c.setHSL(h.h, h.s * I.sat, h.l); }
  return c;
}
const celCss = (S, hex) => '#' + celColor(S, hex).getHexString();
// default shadow multipliers per role for the character cel path (bible 6-3)
const CEL_SHADE = { skin: [.80, .72, .72], face: [.93, .88, .88], light: [.78, .80, .88], mid: [.74, .74, .80], dark: [.70, .70, .78], spot: [.80, .74, .74], glow: [1, 1, 1] };

// hull geometries are pure functions of (source geometry, width, noise): shared by every kit so cached head / hair
// geometry does not pay for its hull again on a style switch. Keyed weakly by the source geometry, so the hulls of
// per-build body / cloth geometry go away with it (no growth on tab switches)
const _inkHullCache = new WeakMap();
function inkKit(S) {
  const I = Object.assign({ mode: 'color', ink: '#151722', paper: '#f6f4ee', shadowMix: .6, hatch: { spacing: 5, width: 1.4, angle: .95 }, tone: { size: 4.5 }, rim: .7, rimOn: true, spot: false }, S.ink || {});
  const shadeTbl = Object.assign({}, CEL_SHADE, I.shade || {});
  const mono = I.mode === 'mono';
  const px = Math.min(window.devicePixelRatio || 1, 2);
  const inkC = new THREE.Color(I.ink), paperC = new THREE.Color(I.paper);
  const sun = new THREE.Vector3(...(S.sun ? S.sun[2] : [-4, 8, 6])).sub(new THREE.Vector3(0, .8, 0)).normalize();
  const shared = {
    uInk: { value: inkC }, uPaper: { value: paperC }, uPx: { value: px },
    uHatch: { value: new THREE.Vector3(I.hatch.spacing, I.hatch.width, I.hatch.angle) },
    uTone: { value: I.tone.size }, uRimK: { value: I.rim }, uMono: { value: mono ? 1 : 0 }, uSpot: { value: I.spot ? 1 : 0 },
    uRimOn: { value: I.rimOn === false ? 0 : 1 },
  };
  const hullCache = _inkHullCache, outlines = new Map();
  const W = S.outline ? S.outline.w : 0;

  // per-role defaults (shadow mix toward ink, light threshold)
  const roleMix = { light: I.shadowMix * .5, skin: I.shadowMix * .42, dark: I.shadowMix, face: I.shadowMix * .2, glow: 0, spot: I.shadowMix * .55, mid: I.shadowMix * .6 };
  const roleTh = { light: .3, skin: .3, dark: .32, face: .2, glow: 0, spot: .3, mid: .3 };

  function inkMaterial(color, o, role) {
    const m = new THREE.MeshToonMaterial({ color, map: o.map || null, side: o.side || THREE.FrontSide });
    const own = { uRole: { value: INK_ROLE[role] }, uMix: { value: o.mix ?? roleMix[role] }, uTh: { value: o.th ?? roleTh[role] }, uGloss: { value: o.gloss || 0 }, uHi: { value: o.hi ?? .74 }, uRimM: { value: o.rimM ?? 1 }, uFold: { value: o.fold || 0 }, uFoldF: { value: o.foldF || 40 } };
    // character cel path: multiply shadow (o.shade overrides the role multiplier, o.shadow = explicit shadow hex)
    const sm = o.shade || shadeTbl[role] || [.8, .8, .8];
    Object.assign(own, { uCel: { value: o.cel ? 1 : 0 }, uShade: { value: new THREE.Vector3(...sm) }, uShadeCol: { value: o.shadow ? celColor(S, o.shadow) : new THREE.Color(0) }, uUseShadeCol: { value: o.shadow ? 1 : 0 } });
    m.onBeforeCompile = sh => {
      Object.assign(sh.uniforms, shared, own);
      sh.fragmentShader = sh.fragmentShader
        .replace('#include <common>', '#include <common>\n' + INK_PARS)
        .replace('#include <lights_toon_pars_fragment>', INK_TOON_PARS)
        .replace('#include <opaque_fragment>', INK_SHADE + '\n#include <opaque_fragment>');
    };
    m.customProgramCacheKey = () => 'ink1';
    m.userData.inkU = own;
    return m;
  }

  function hullMaterial(col, extra, perVert, prof) {
    const m = new THREE.MeshBasicMaterial({ color: col, side: THREE.BackSide });
    if (extra) {
      const u = { uSun: { value: sun }, uExtra: { value: W * 1.15 }, uRefD: { value: S.cam && S.cam.d ? S.cam.d : 4.5 }, uMinK: { value: .3 }, uBase: { value: Math.max(W * .5, 1e-5) } };
      m.onBeforeCompile = sh => {
        Object.assign(sh.uniforms, u);
        sh.vertexShader = sh.vertexShader.replace('#include <common>', '#include <common>\nuniform vec3 uSun;\nuniform float uExtra;\nuniform float uRefD;\nuniform float uMinK;\nuniform float uBase;\nattribute float inkO;' + (perVert ? '\n#define INK_W\nattribute float inkW;' : '') + (prof ? '\n#define INK_P\nattribute float inkQ;' : ''))
          .replace('#include <begin_vertex>', INK_HULL_VERT);
      };
      m.customProgramCacheKey = () => 'inkhull4' + (perVert ? 'w' : '') + (prof ? 'p' : '');
    }
    return m;
  }
  // low-frequency wobble for hand-drawn line weight (bible 7-4)
  const wob = (x, y, z, f) => Math.sin(x * f * 1.7 + Math.sin(y * f * 1.3) * 1.6) * .55 + Math.sin(y * f * 1.1 + z * f * .9 + 1.3) * .45;

  // smooth (position-merged) normals so split-normal geometry (boxes, seams) gets a crack-free hull
  function hullGeo(geo, w, noise = 0) {
    let byGeo = hullCache.get(geo); if (!byGeo) hullCache.set(geo, byGeo = new Map());
    const key = w.toFixed(6) + ':' + noise;
    if (byGeo.has(key)) return byGeo.get(key);
    const g = geo.clone(), p = g.attributes.position, iw = g.attributes.inkW, ip = g.attributes.inkP;
    if (!g.attributes.normal) g.computeVertexNormals();
    const n = g.attributes.normal, acc = new Map(), keys = new Array(p.count);
    for (let i = 0; i < p.count; i++) {
      const k = Math.round(p.getX(i) * 1e4) + ',' + Math.round(p.getY(i) * 1e4) + ',' + Math.round(p.getZ(i) * 1e4);
      keys[i] = k; let a = acc.get(k); if (!a) acc.set(k, a = new THREE.Vector3());
      a.x += n.getX(i); a.y += n.getY(i); a.z += n.getZ(i);
    }
    for (const a of acc.values()) { if (a.lengthSq() < 1e-12) a.set(0, 1, 0); a.normalize(); }
    const nn = new Float32Array(p.count * 3), oo = new Float32Array(p.count), qq = ip ? new Float32Array(p.count) : null;
    for (let i = 0; i < p.count; i++) {
      const a = acc.get(keys[i]); nn[i * 3] = a.x; nn[i * 3 + 1] = a.y; nn[i * 3 + 2] = a.z;
      let wi = w * (iw ? iw.getX(i) : 1);
      if (noise) wi *= 1 + noise * wob(p.getX(i), p.getY(i), p.getZ(i), 38);
      p.setXYZ(i, p.getX(i) + a.x * wi, p.getY(i) + a.y * wi, p.getZ(i) + a.z * wi);
      oo[i] = wi;
      if (qq) qq[i] = Math.max(0, ip.getX(i) * w - wi);    // extra width to reach the profile weight when turned
    }
    g.setAttribute('normal', new THREE.BufferAttribute(nn, 3));
    g.setAttribute('inkO', new THREE.BufferAttribute(oo, 1));
    if (qq) { g.setAttribute('inkQ', new THREE.BufferAttribute(qq, 1)); g.deleteAttribute('inkP'); }
    g.deleteAttribute('uv'); g.clearGroups();
    p.needsUpdate = true; byGeo.set(key, g);
    return g;
  }

  const kit = {
    S, flashables: [], mono,
    solid(hex, o = {}) {
      const src = new THREE.Color(hex);
      let role = o.role || (o.map ? 'light' : (inkLum(src) < .28 ? 'dark' : 'light'));
      if (!(role in INK_ROLE)) role = 'light';
      let color;
      if (o.map) color = new THREE.Color(0xffffff);
      else if (o.raw) color = src;
      else color = o.cel ? celColor(S, hex) : styleColor(S, hex);
      const m = inkMaterial(color, o, role);
      if (o.emissive) { m.emissive = new THREE.Color(o.emissive); m.emissiveIntensity = o.ei ?? 1; }
      m.userData.base = hex; m.userData.inkRole = role; m.userData.keyline = o.keyline; m.userData.cel = !!o.cel;
      return m;
    },
    // plain BackSide hull (used directly by env on instanced meshes): no GPU extrusion
    outline(mat, extra = false, perVert = false, prof = false) {
      if (!S.outline) return null;
      const m0 = Array.isArray(mat) ? mat[0] : mat;
      const white = mono && m0 && m0.userData && m0.userData.inkRole === 'dark' && m0.userData.keyline !== false;
      const k = (white ? 'p' : 'i') + (extra ? 'x' : '') + (perVert ? 'w' : '') + (prof ? 'q' : '');
      if (!outlines.has(k)) outlines.set(k, hullMaterial(white ? paperC : inkC, extra, perVert, prof));
      return outlines.get(k);
    },
    hullGeo,
    // hull for a mesh: thin on the lit side, thick on the shadow side (lost edge / heavy edge).
    // geo.attributes.inkW (0..1) scales the width per vertex; character (cel) parts get a +-25% hand-drawn wobble
    hull(geo, mat, ow = 1, scale = 1) {
      const pv = !!geo.attributes.inkW, m0 = Array.isArray(mat) ? mat[0] : mat;
      const om = kit.outline(mat, true, pv, !!geo.attributes.inkP);
      if (!om) return null;
      const nz = m0 && m0.userData && m0.userData.cel && !pv ? .25 : 0;
      return { geo: hullGeo(geo, (W * .5 * ow) / scale, nz), mat: om };
    },
    part(parent, geo, mat, o = {}) {
      const m = new THREE.Mesh(geo, mat);
      if (o.pos) m.position.set(...o.pos);
      if (o.rot) m.rotation.set(...o.rot);
      if (o.scl) m.scale.set(...o.scl);
      m.castShadow = o.shadow !== false; m.receiveShadow = !!o.receive;
      parent.add(m);
      if (o.outline !== false && S.outline) {
        const s = o.scl ? (o.scl[0] + o.scl[1] + o.scl[2]) / 3 : 1;
        const h = kit.hull(geo, mat, o.ow || 1, s);
        if (h) { const hm = new THREE.Mesh(h.geo, h.mat); hm.castShadow = false; hm.receiveShadow = false; m.add(hm); }
      }
      return m;
    },
  };
  return kit;
}
