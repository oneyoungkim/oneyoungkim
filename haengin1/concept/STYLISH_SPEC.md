# 스타일리시 리디자인 스펙 (A / A′) — 빌더 공통 계약서

사용자 피드백: "A 애니 셀셰이딩 느낌은 괜찮은데 캐릭터가 너무 스타일리시하지 않다. 이 정도로 스타일리시해야 한다."
레퍼런스: **`concept/REFERENCES.md`** (2026-10-05 대표가 다시 준 캐릭터 C1~C5 + 배경 한옥 골목 일러스트를 글로 옮긴 것. 원본 이미지 파일은 없다).

## 1. 목표 룩

공통 (레퍼런스에서 뽑은 핵심):
1. **비율**: 머리 작고 길쭉. 시우 7.6등신, 태오 8.4등신. 목 길고, 다리 길고, 손 크고 손가락 길다.
2. **실루엣**: 오버핏 교복 재킷(어깨가 떨어지고 밑단이 엉덩이 아래까지, 앞을 연 채), 통 넓은 슬랙스(무릎 아래로 일자, 밑단이 신발 위에 접힘), 로퍼/스니커즈.
3. **얼굴**: 좁고 날카로운 V 턱, 반쯤 감긴 날카로운 아몬드 눈(눈동자 작게), 가는 직선 눈썹, 콧날 선 하나, 작은 입. 표정은 쿨하거나 여유로운 미소.
4. **머리카락**: 공 모양 X. 끝이 뾰족한 **머리 다발(lock)** 여러 개로 구성. 결 방향이 보여야 함.
5. **명암**: 2단 하드 셰이딩. 그림자는 잉크색 쪽으로 깊게. 밝은 재질(피부·셔츠)은 그림자에 해칭, 어두운 재질(재킷·바지·머리)은 먹색 바탕에 흰 하이라이트 띠·림라이트·흰 해칭.
6. **포즈**: 서 있기만 해도 그림이 되는 포즈. 쇼 포즈(카메라를 볼 때)가 특히 중요.
7. **담배 금지**: 17세 캐릭터. 레퍼런스의 담배 자리는 **바나나우유(빨대)** 로 대체 (태오의 트레이드마크).

두 스타일:
- **A 스타일리시 셀 (`STYLES.A`, `S.ink.mode = 'color'`)**: 절제된 색. 남색-먹색 재킷, 흰 셔츠, 피부색, 포인트 컬러(시우 주황 / 태오 파랑). 그림자 = 고유색을 잉크색(`S.ink.ink`)으로 `shadowMix`만큼 섞은 색. 그림자 경계 띠에 옅은 해칭.
- **A′ 잉크 망가 (`STYLES.I`, `S.ink.mode = 'mono'`)**: 종이(`S.ink.paper`)와 먹(`S.ink.ink`) 2색 + 선택적 스폿 컬러(`S.ink.spot`이 true면 시우 밑창 주황, 태오 포인트 파랑만 색 유지). 밝은 재질: 빛=종이, 반그림자=해칭, 그림자=먹 또는 스크린톤. 어두운 재질: 먹 바탕 + 흰 하이라이트/림/흰 해칭. 배경도 같은 규칙(흑백 원고가 움직이는 느낌).

## 2. 캐릭터 디자인

### 반시우 (162cm, 7.6등신, 마른 체형)
- 머리: 검은 **울프컷/멀릿**. 층진 머리 다발, 뒷머리는 목덜미까지, 끝이 살짝 말림(원래 곱슬 설정을 '웨이브 끝'으로). **긴 앞머리가 오른쪽 눈(캐릭터 기준 오른쪽 = -X쪽)을 반쯤 덮음**.
- 얼굴: 살짝 그을린 피부, 반쯤 감긴 날카로운 눈, 일자 눈썹이 미간 쪽으로 내려옴(무표정·노려봄), 콧등 반창고(시그니처, 유지).
- 옷: 오버사이즈 남색 교복 재킷 앞 열림, 소매를 팔뚝까지 걷어 주름 뭉침. 안에 **회색 후드티**, 후드가 재킷 깃 밖으로 나옴. 넥타이 없음. 통 넓은 차콜 슬랙스. 흰 하이탑 스니커즈 + **주황 밑창**. 손 마디에 흰 테이프.
- 쇼 포즈: **양키 스쿼트** — 발 붙이고 깊게 쪼그려 앉아 무릎을 벌리고, 팔뚝을 무릎 위에 걸쳐 손을 늘어뜨림. 고개를 살짝 들고 옆으로 기울여 카메라를 노려봄. (레퍼런스 1의 앞 남자)
- 파이트 스탠스: 낮고 날렵한 하이 가드, 턱 당김, 가벼운 바운스.

### 강태오 (183cm, 8.4등신, 넓은 어깨·역삼각형)
- 머리: 옆·뒤 **투블럭 언더컷**(짧은 회흑색), 윗머리를 뒤로 넘겨 **하프업 상투(번)**, 이마로 흘러내린 **잔머리 두 가닥**. (레퍼런스 3·4의 번)
- 얼굴: 긴 얼굴, 강한 턱, 굵은 눈썹, 기본 표정은 **실눈 미소**(여유). 왼쪽 귀 만두귀(유지).
- 옷: 흰 셔츠 단추 두 개 풀고 느슨한 남색 넥타이, 소매를 팔꿈치까지 걷어 굵은 팔뚝 노출. 남색 재킷은 앞을 연 채 입음(길고 날카로운 라펠). 통 넓은 슬랙스 + 벨트. 검정 로퍼(광택 하이라이트) + **파랑 포인트**(양말이나 재킷 안감 중 하나).
- 소품: **바나나우유**(노란 항아리 모양 병 + 흰 뚜껑 + 빨대). 상표 글자는 넣지 말 것.
- 쇼 포즈: 콘트라포스토로 서서 왼손은 바지 주머니, 오른손은 바나나우유를 가슴 앞에 듦. 고개를 살짝 기울이고 실눈 미소. (레퍼런스 4의 게토 + 1의 뒷 남자)
- 파이트 스탠스: 유도 베이스, 손을 펴서 앞으로(깃 잡으러 가는 손), 무게중심 낮게.

## 3. 좌표·관절 규약 (반드시 지킬 것)

- 단위 m. 캐릭터 root = 두 발 사이 바닥, 로컬 +Z가 정면, +Y 위, 캐릭터의 왼쪽 = +X.
- 관절 그룹(THREE.Group) 이름: `JN = ['hips','spine','chest','neck','head','uaL','faL','haL','uaR','faR','haR','thL','shL','ftL','thR','shR','ftR']`. L = 캐릭터 왼쪽(+X).
- 팔다리 메시는 관절 원점에서 **로컬 -Y 방향**으로 뻗는다. 모든 회전 0 = 차렷(팔 아래로, 다리 곧게, 발끝 +Z).
- Euler 순서 XYZ(three 기본). 매달린 팔다리에 rotation.x 음수 = 앞으로(+Z) 들기, 무릎 rotation.x 양수 = 정강이 뒤로 접기, rotation.z 양수 = +X 쪽으로 벌리기. 척추·목·머리 x 양수 = 앞으로 숙임, y 양수 = 왼쪽(+X)으로 돌림.
- 계층: `root`(월드 위치+yaw, rotation.order 'YXZ') > `tilt` > `off` > `hips`(position.y = legL*(1-drop)) > `spine` > `chest` > `neck` > `head`; `chest` > `uaL/uaR` > `faL/faR` > `haL/haR`; `hips` > `thL/thR` > `shL/shR` > `ftL/ftR`.
- 기본 오프셋 (기존 `Fighter` 기준, 바꿔도 되지만 의미는 유지): hips y=legL, chest y=torsoL*.45 (spine 안), neck y=torsoL*.56 (chest 안), head y=neckL, 어깨 (±sh*.92, torsoL*.47, 0), fa (0,-upperL,0), ha (0,-foreL,0), 고관절 (±hip*.52, -hip*.1, 0), sh (0,-thighL,0), ft (0,-shinL,0).
- 포즈 데이터: `P(obj)` → Float32Array. obj 키는 JN의 [x,y,z] + `drop`(다리 길이 대비 엉덩이 내림 비율) + `fwd`(정면 방향 전진 m). `STANCE`, `SHOW`, `attackClips(aim, base)`는 `src/01_data.js`.

## 4. 파일 소유권과 인터페이스

빌드: `node concept/build.mjs` (전체 페이지) / 테스트: `node concept/build.mjs --parts 01,02,03,07 --extra 테스트.js --out 경로.html --bare`
파일은 숫자 순서로 하나의 ES 모듈에 이어 붙는다(`THREE`는 전역 import). **`99_app.js`가 마지막**이고 `boot()`를 호출한다. 남의 파일은 고치지 말 것(필요하면 최종 보고에 요청으로 적기).

| 파일 | 담당 | 내보내는 것 |
|---|---|---|
| `src/07_ink.js` | 잉크 셰이더 | `inkKit(S)` |
| `src/08_shapes.js` | 지오메트리 | `taperTube`, `hairLock`, `jacketGeo`, `lapelGeo`, `shoeGeo`, `buildHand`, `buildBananaMilk` |
| `src/09_ik.js` | IK | `solveTwoBone`, `Fighter.prototype.applyIK` |
| `src/10_faces.js` | 얼굴 | `FACE_LAYOUT`, `stylishFaceTexture(def, S, expr)` |
| `src/11_stylish.js` | 캐릭터(2단계) | `STYLISH_DEF`, `STYLISH_POSES`, `class StylishFighter extends Fighter` |
| `src/04_env.js`, `src/05_fx.js` | 배경·이펙트(2단계) | 기존 함수에 `S.ink` 지원 추가, `fx:'ink'` 레시피 |
| `src/01_data.js`, `src/03_rig.js`, `src/99_app.js`, `src/page.html` | 메인(통합) | — |

### 4-1. `inkKit(S)` — `materialKit(S)`와 같은 모양
```js
const kit = inkKit(S);
kit.solid(hex, { map, role, rough, raw, emissive, ei })  // → Material (MeshToonMaterial + onBeforeCompile 권장: 그림자 수신 유지)
//   role: 'light' | 'dark' | 'skin' | 'face' | 'glow' | 'spot'  (생략 시 hex 밝기로 자동: 상대휘도 < .28 → 'dark')
//   'face' = 텍스처 얼굴: 거의 평면, 아주 옅은 1단 그림자만 (얼굴 선이 텍스처에 있으므로)
//   'spot' = mono 모드에서도 색 유지 (S.ink.spot이 true일 때)
kit.outline(mat)        // → BackSide MeshBasicMaterial(잉크색) 또는 null
kit.part(parent, geo, mat, { pos, rot, scl, outline, ow, shadow, receive })  // → Mesh (+ 아웃라인 헐 자식)
kit.flashables          // 피격 번쩍임용: emissive를 가진 재질 배열. emissiveIntensity를 올리면 최종색이 하얗게 떠야 함
```
- InstancedMesh의 instanceColor, `map`, 그림자 맵, 안개(fog)가 모두 동작해야 함.
- 해칭/스크린톤은 화면 공간(`gl_FragCoord`) 패턴, 간격 단위는 CSS px × devicePixelRatio(최대 2).
- 빛의 양 추정: `reflectedLight.directDiffuse`와 `diffuseColor`로 N·L×그림자를 복원하거나, 동등한 방법. 림: `1 - dot(normal, geometryViewDir)`.

### 4-2. `08_shapes.js`
- `taperTube(points, radius, opts)` — points: `THREE.Vector3[]`(곡선 제어점, CatmullRom으로 보간), radius: `t => number | [rx, rz]`(t∈[0,1]), opts `{ radial=12, tubular=24, caps=true, up=Vector3 }`. 매끈한 노멀, 끝 반지름 0이면 뾰족한 끝. 아웃라인 헐(노멀 방향 extrude)에 균열이 없어야 함(인덱스 공유 또는 이음매 노멀 동일).
- `hairLock({ len, width, thick, bend, twist, taper })` — 원점에서 시작해 로컬 -Y로 뻗고 bend 방향(+Z 기준 휘어짐)으로 휘는, 납작하고 끝이 뾰족한 머리 다발 지오메트리. 앞머리·뒷머리·삐침 모두 이걸로.
- `jacketGeo({ ctrl, y0, y1, open, depth, flare })` — 앞이 열린 재킷 몸판 셸(양면 렌더 가정, 두께 약간). ctrl = `[[yFrac, halfWidth], ...]`, open = 앞 트임 각도(rad), flare = 밑단 벌어짐.
- `lapelGeo({ len, width, notch })` — 라펠 한 쪽 (얇은 판).
- `shoeGeo({ kind: 'loafer' | 'hightop', len, width, height })` — 신발 + 밑창은 별도 지오메트리 반환 `{ upper, sole }`.
- `buildHand(kit, parent, { size, side, curl, skinMat, tapeMat })` → `{ group, setCurl(c) }`. 손바닥 + 손가락 4개(마디 2개씩) + 엄지. curl 0 = 편 손, 1 = 주먹.
- `buildBananaMilk(kit)` → Group (높이 약 12cm: 노란 항아리 몸통, 흰 뚜껑, 빨대). 원점 = 병 바닥 중심.

### 4-3. `09_ik.js`
- `solveTwoBone(upper, lower, L1, L2, targetWorld, poleWorld, weight)` — upper/lower는 관절 그룹(뼈는 로컬 -Y로 뻗음). 끝점이 target에 닿도록 두 그룹의 로컬 quaternion을 설정하고, 기존(FK) 회전에서 weight만큼 slerp. 굽힘 평면은 pole 쪽. 굽힘 축은 로컬 +X에 정렬(손목·발목 비틀림 일관성).
- `Fighter.prototype.applyIK = function (w, busy)` — `this.ikTargets`가 있고 `busy`가 false이며 w>0.001일 때만 동작. 형식(모두 root 로컬 좌표, m):
  `{ L: { hand:[x,y,z], pole:[x,y,z], hide?:bool }, R: {...}, FL: { foot:[x,y,z], pole:[x,y,z], flat:true, yaw:0 }, FR: {...} }`
  다리 IK 뒤에 `flat`이면 발 그룹을 바닥과 평행(root 기준 yaw만)으로 맞춘다. 호출 전 `this.root.updateMatrixWorld(true)`.
- Node에서 수치 테스트: three(r170) `three.module.js`를 로컬 경로에서 import 해서 끝점 오차 < 1mm 검증.

### 4-4. `10_faces.js`
- `FACE_LAYOUT = { eyeU, eyeV, browV, noseV, mouthV, chinV }` (도 단위, 얼굴 정면 = 텍스처 u .25, 적도 = v 0, 아래가 +). 캐릭터 담당이 코 돌출·턱 위치를 여기에 맞춤.
- `stylishFaceTexture(def, S, expr)` → CanvasTexture(1024×512 equirect, `canvasTex()` 사용). expr: `normal | hurt | ko | win`. `def.id`로 시우/태오 구분(시우: 노려보는 반감은 눈 + 반창고, 태오: 실눈 미소 기본). mono 모드에서는 피부 = 종이색, 선 = 먹.

## 5. 테스트 방법

- 스크린샷: `PAGE=... OUT=... STEPS='[...]' READY='window.__ready' THREE=<three.module.min.js 경로> NODE_PATH=<playwright가 있는 node_modules> node concept/tools/shot.cjs` (사용법은 파일 상단 주석). `OUT/logs.txt`의 에러는 0이어야 함.
- `--bare` 테스트 페이지는 `#gl` 캔버스 하나만 있다. 테스트 스크립트에서 renderer/scene/camera를 직접 만들고 다 그린 뒤 `window.__ready = true`.
- 스크린샷은 Read 툴로 직접 보고, 레퍼런스 이미지와 나란히 비교하며 판단할 것.
- 테스트 파일·스크린샷은 레포 밖 작업 폴더(세션 scratchpad)에 둘 것.
- git commit 금지.
