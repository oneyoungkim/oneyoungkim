# 행인1의 메인이벤트 — Unity 프로젝트

- 프로젝트 위치: `haengin1/unity/HaenginMainEvent` (레포 안, 한글 경로 `C:\클로드\...` 그대로 사용 — 아래 '주의 2' 참고)
- 에디터: **Unity 6000.3.25f1 (Unity 6.3 LTS)**, 렌더 파이프라인 **URP 17.3** (Hub의 "Universal 3D" 템플릿 17.0.14에서 시작)
- 회사명 / 제품명: `makethis1` / `행인1의 메인이벤트` (빌드 실행 파일 이름은 영문 `HaenginMainEvent.exe`)
- 입력: Input System 전용(Player Settings › Active Input Handling = Input System Package (New))
- 에셋 직렬화: Force Text, 메타 파일: Visible Meta Files (기본값 확인함)

## 1. 여는 법

1. Unity Hub › Projects › **Add** › `...\haengin1\unity\HaenginMainEvent` 폴더 선택 → 에디터 6000.3.25f1 로 연다.
2. 처음 열면 `Library/` 를 새로 만드느라 몇 분 걸린다(패키지 내려받기 포함, git 패키지는 Git 이 PATH 에 있어야 함).
3. 장면: `Assets/_Project/Scenes/Zone1.unity`(M1 결과물 — 걸어 다닐 수 있는 1구역, 빌드 첫 장면) · `Sandbox.unity`(모델 보기, 빌드 두 번째).
4. 같은 프로젝트를 Unity 두 개가 동시에 열면 안 된다(`Temp/UnityLockfile`). 에디터를 켜 둔 채로 아래 배치 명령을 돌리지 말 것.

## 2. 패키지 (Packages/manifest.json)

| 패키지 | 버전 | 용도 |
|---|---|---|
| com.unity.render-pipelines.universal | 17.3.0 | URP (템플릿 기본, 에디터 내장 버전) |
| com.unity.cinemachine | 3.1.7 | 3인칭 카메라·화면 흔들림. Sandbox 의 `CM_FullBody` |
| com.unity.inputsystem | 1.20.0 | 패드·키보드 입력 |
| com.unity.timeline | 1.8.13 | 컷신 |
| com.unity.probuilder | 6.1.2 | 혜화동 그레이박스 |
| com.unity.cloud.gltfast | 6.20.0 | **GLB/glTF 임포트** (시우 Tripo 모델). `.glb` 기본 임포터 |
| com.unity.toonshader | 0.15.1-preview | **Unity Toon Shader(UTS3)** — 셀 셰이딩 + 외곽선. 셰이더 이름 `Toon/Toon` |
| com.vrmc.gltf (UniGLTF) | git `v0.131.3` | UniVRM 의존 패키지 |
| com.vrmc.vrm (VRM-1.0) | git `v0.131.3` | VRM 1.0 임포트(VRoid 캐릭터). VRM 0.x 파일도 1.0 으로 변환해 읽음 |
| com.unity.ai.navigation | 2.0.14 | NavMesh (템플릿 기본) |
| com.unity.ugui / test-framework / visualscripting / ide.* / 2d.sprite | 템플릿 기본 | — |

git URL (UniVRM 공식 README 방식, 태그 고정):
```
"com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.3",
"com.vrmc.vrm":  "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.3"
```

메모
- **툰 셰이더는 UTS 를 골랐다**: Unity 공식 패키지라 레지스트리에서 버전 고정이 되고(lilToon 은 git), 이 에디터·URP 17.3 에서 컴파일 에러 없이 들어갔다. 무엇보다 FUJIMOTO_STYLE 6장 규칙과 그대로 맞는다 — `Use BaseMap as 1st ShadeMap` + `1st_ShadeColor` = **그림자 = 고유색 × 곱셈값**, 외곽선은 인버티드 헐(`Outline_Color` = #1A1417), 스펙큘러·림 끔. 아직 preview 표기(0.x)라 업데이트 때 재질 속성 이름이 바뀔 수 있음.
- **glTFast 와 UniGLTF 가 둘 다 `.glb` 임포터를 등록**하므로 Player Settings › Scripting Define Symbols(Standalone)에 `UNIGLTF_DISABLE_DEFAULT_GLB_IMPORTER;UNIGLTF_DISABLE_DEFAULT_GLTF_IMPORTER` 를 넣어 UniGLTF 쪽을 끄고 glTFast 를 기본으로 했다(`UNITY_PIPELINE_URP` 는 UTS 가 자동으로 넣은 것). UniGLTF 로 읽고 싶으면 에셋 Inspector 의 Importer 드롭다운에서 바꾼다.
- 템플릿을 Hub 없이 풀어서 만들었더니 Unity 가 '예전 프로젝트 업그레이드'로 보고 analytics·purchasing(IAP 4, 지원 종료)·xr.legacyinputhelpers·collab-proxy·2d.tilemap·multiplayer.center 를 끼워 넣어서 지웠다(Git 을 쓰므로 Unity Version Control 불필요).
- Unity ↔ Claude 연결(MCP): 레지스트리에 `com.unity.ai.assistant` 2.20.0-pre.1(정식판 없음, 안의 MCP 서버는 **폐지 예고**)과 `com.unity.pipeline` 0.8.0-exp.1(실험판)이 있다. Unity 공식 문서는 이제 **Unity CLI**(`unity pipeline install` → `unity mcp configure claude`)로 붙는 방식을 권한다. 이 PC 에는 Unity CLI 가 아직 없다(PATH 에 `%LOCALAPPDATA%\Unity\bin` 항목만 있고 폴더 없음). 설치하지 않았다.

## 3. 폴더 구조

```
HaenginMainEvent/
├─ Assets/
│  ├─ _Project/                     ← 우리 것은 전부 여기
│  │  ├─ Art/Characters/Siwoo/      Siwoo.fbx(모델 + 대기 Idle_6) · SiwooWalk.fbx(Quick_Walk) · SiwooRun.fbx(Run_02) (Humanoid, tools/glb2fbx.py 로 만든 리깅 모델) · SiwooTex.jpg · 예전 siwoo_tripo_v1.glb(정적) · siwoo_tripo_v1_rigged.glb(팔 스키닝 깨짐)
│  │  ├─ Art/Characters/Taeo/       Taeo.fbx(모델 + 대기 Idle_3, Humanoid) · TaeoWalk.fbx(Quick_Walk, 나중 대비 — 아직 안 씀) · TaeoTex.jpg — Sandbox 에만
│  │  ├─ Art/Env/                   맵 아트 패스 입력(2026-10-24, concept/art/env 에서 복사): Buildings/Z1_*.fbx 7 + zone1_buildings_manifest.json · Props/*.glb 40(LOD 3단, tools/env/prop_lods.py) · Textures/M_*.png 26 · Sky/(원경 3×2·구름 9·SkyArc.asset) · Signs/sign_atlas.json
│  │  ├─ Anim/                      Siwoo.controller(대기·걷기·달리기 블렌드 트리) · Taeo.controller(대기) — CharSetup 이 만듦
│  │  ├─ Data/                      zone1.json (1구역 배치도, tools/zone1_gen.py 가 만듦 — 손으로 고치지 말 것)
│  │  ├─ Materials/                 M_SiwooAnim_Toon · M_Taeo_Toon(리깅 모델, UTS) · M_Siwoo_Toon(예전 정적) · M_SiwooRigged_Toon · M_Floor_Sand(URP Lit)
│  │  │  └─ Zone1/                  M_Z1_<종류>.mat — 그레이박스 단색 재질 + T_Z1_Stone·Paving·Beam.png 생성 텍스처(Zone1Builder 가 만듦). 길·인도·성곽길·계단·주차장 재질은 EnvSetup 이 아트 텍스처 + UTS 로 바꿈
│  │  │  └─ Env/                    M_<재질>.mat 33(UTS, 키트 규약 21 + 아트 5 + 키트 평면색 7) · Props/M_P_<소품>.mat 40 · Signs/M_Sign_<아틀라스 칸>.mat · Sky/M_Sky_{North,East,South,West}·M_Cloud_00~08 · ../Fx/M_Outline.mat
│  │  ├─ Fonts/                     NotoSansKR-Bold.ttf + OFL.txt(OFL 1.1, 게임 동봉 가능) · KR_Bold_SDF.asset(TMP 동적 한글 글꼴) · KR_Label.mat(이름표, 오버레이)
│  │  ├─ Scenes/                    Sandbox.unity · Zone1.unity + Zone1_Mesh.asset(생성 메시 73개)
│  │  ├─ Input/                     HInput.inputactions — M1 입력(Explore 맵 + Menu 맵(일시정지 메뉴), 키보드·마우스·패드)
│  │  ├─ Prefabs/                   Player.prefab — 시우 리깅 FBX(툰, Animator + LocoAnim) + CharacterController·PlayerMotor·PInput + Visual(BodyLean) + CamTarget (M1Setup 이 만듦)
│  │  │  └─ Env/                    P_<소품>.prefab 40 — LODGroup 3단 + UTS 재질 + GPU 인스턴싱(EnvSetup.PropPrefabs). 콜라이더 없음(벽에 붙는 장식, 충돌은 Zone1 상자 그대로)
│  │  ├─ Scripts/                   런타임 코드(어셈블리 Haengin.Game): Player/ PlayerMotor·PInput·BodyLean·LocoAnim·MoveTuning · Cam/ CamRig·CamInput·CamTuning·CamClearance · Core/ RigFactory·Layers·GameState·ModelFit·RouteData · Route/ RouteGuide·RouteHud·NameTags · UI/ GameUi(조작 안내·알림·일시정지 메뉴) · Dev/ DebugHud(F1)
│  │  ├─ Editor/                    BatchTools.cs · CharSetup.cs(리깅 캐릭터 임포트·재질·애니메이터) · SandboxSetup.cs · TempPathGuard.cs · Zone1Builder.cs · Zone1Data.cs · MiniJson.cs · StableIds.cs(결정적 fileID) · KoreanFont.cs · EnvImport.cs(Art/Env 들여오기 설정) (어셈블리 Haengin.EditorTools)
│  │  │  └─ Game/                   M1Setup.cs · RouteSetup.cs · … · EnvSetup.cs(맵 아트 패스: 재질·소품 프리팹·원경·외곽선·조명 프리셋 + Zone1 에 키트 세우기, 비교 사진) (어셈블리 Haengin.EditorGame — 게임 코드를 참조하는 장면 설정)
│  │  ├─ Shaders/                   FxSprite · ShockCut · InkMode · ScreenOutline(배경 화면 공간 외곽선) · SkyCard(원경 카드) · CloudCard(구름·달)
│  │  ├─ Tests/                     PlayMode 테스트(어셈블리 Haengin.Tests): MoveTests · ZoneTests · InputTests · RouteTests · AnimTests · Lab
│  │  └─ Settings/                  Sandbox_FilmVolume.asset · Zone1_FilmVolume.asset (필름 후처리, 같은 레시피) · MoveTuning.asset · CamTuning.asset (조정값, 없을 때만 만듦)
│  ├─ _Template/                    URP 템플릿 샘플(Readme, TutorialInfo, SampleScene) — 지우지 않고 격리
│  ├─ TextMesh Pro/                 TMP 기본 리소스(TMP Settings·셰이더·LiberationSans) — com.unity.ugui 2.0 의 Essential Resources. 한글은 _Project/Fonts
│  ├─ Settings/                     URP 파이프라인 에셋(PC/Mobile_RPAsset·Renderer, 전역 설정) — 템플릿 것이지만 실제 사용 중
│  └─ InputSystem_Actions.inputactions   프로젝트 전역 입력 액션(템플릿 기본)
├─ Packages/  ProjectSettings/      커밋 대상
└─ Library/ Temp/ Logs/ UserSettings/ Builds/   커밋 안 함(.gitignore)
```

`Haengin.EditorTools` 어셈블리는 게임 코드(`Haengin.Game`)에 기대지 않게 따로 떼어 두었다. 게임 코드가 필요한 장면 설정은 `Haengin.EditorGame`(Editor/Game) 이 맡고, `Zone1Builder.BeforeSave` 확장 지점에 등록해서 Zone1 을 다시 만들 때 리그를 붙인다(한 방향 참조만). 다만 배치 실행에서 스크립트 컴파일 에러가 있으면 Unity 가 `Scripts have compiler errors.` 를 남기고 `-executeMethod` 전에 종료 코드 1로 끝난다(검수에서 확인) → **컴파일 에러는 종료 코드 1과 로그의 `error CS` 줄로 판단**한다. ImportCheck 는 컴파일이 통과한 뒤의 임포트 에러를 세는 용도.

### Sandbox 장면 구성 (`SandboxSetup.Build` 가 만든 것)
- 배경 = 종이색 **#F4EFE6**(카메라 단색), 바닥 = 모래 베이지 **#E9E0CC** 300m 평면, 선형 안개 #E9E0CC 30~140m
- Directional Light: 따뜻한 흰빛, 앞-왼쪽 위(48°, 150°), 부드러운 그림자(강도 .7)
- **Siwoo**(x −0.55) · **Taeo**(x +0.65) — 2026-10-06 리깅 모델(Humanoid FBX, 정면 +Z, 배율 1: 시우 1.74m · 태오 1.83m) + 툰 재질 + 애니메이터(대기 동작). 편집 모드 촬영(`BatchTools.Screenshot`)은 대기 클립 1초 자세로 찍는다(`-animTime` 초)
- **Siwoo_Static**(원점, **비활성**): 예전 정적 Tripo GLB(Y +90°, 키 1.74 로 스케일) · **Siwoo_Rigged**(x=1.6, **비활성**, 팔 스키닝 깨진 예전 리깅 GLB) — 참고용
- Main Camera(+CinemachineBrain, SMAA, 후처리 켬) + **CM_FullBody**(FOV 30°, 두 사람 전신이 들어오게 거리 4.19m)
- `SandboxSetup.Build` 는 이제 빌드 목록이 비어 있을 때만 Sandbox 하나로 정한다(예전엔 M1 빌드 목록을 덮어썼음)
- Global Volume: 채도 -15(=×.85), 대비 -6, 스플릿 톤(그림자 #2F4A5A 쪽 / 밝은 쪽 #F3D9B0 쪽), 톤매핑 없음, 블룸 끔
- 툰 재질(M_Siwoo_Toon): 기본 텍스처 그대로, 1단 그림자 = 텍스처 × (.78,.71,.74) 모브, 2단 = 1단 × (.84,.80,.80), 경계 feather .02, 스펙큘러·림 0, 외곽선 #1A1417 폭 2.6(오브젝트 공간 ×0.001, 1080p 전신 컷에서 실루엣 2~3px). 텍스처 한 장에 피부·옷·머리가 섞여 있어 부위별 곱셈값은 아직 못 나눔(부위 마스크 또는 재질 분리 필요).

### Zone1 장면 구성 (`Zone1Builder.Build` 가 만든 것, 2026-10-05 · 2차 다듬기 반영)
혜화동 1구역(성대 후문 ~ 와룡공원) M1 그레이박스. 배치도 `Data/zone1.json` → `Scenes/Zone1.unity`. 데이터 규칙은 `docs/06_M1_그레이박스_설계.md` 14장, 레이어·계단 충돌 규칙은 `docs/07_M1_조작_설계.md` 3-5·3-6.
- **좌표**: zone1.json 그대로(원점 = 혜화동 로터리, +X 동, +Z 북, 1 = 1m). 장면을 옮기지 않는다(다음 구역과 같은 좌표로 이어 붙이기 위해). 1구역은 x −274 ~ −130, z 70 ~ 174.
- **장면 루트** `Zone1/`
  - `Ground` — `Terrain`(73×53 높이 격자 메시 + MeshCollider) · `Apron`(구역 밖으로 내려가는 지형, 보이기만) · 패드 6개(윗면 = 패드 높이, 아래로 3m)
  - `Roads` — 길 16줄. 폴리라인을 폭만큼 넓힌 띠(위 = 걷는 면, 아래로 3m) + MeshCollider. 겹치는 곳 깜빡임 방지로 종류별 1~5cm 차등
  - `Blocks/<종류>` — 상자 138개(BoxCollider). 소나무 = 줄기 + 잎 상자(콜라이더는 지름 0.6m 캡슐), 풀숲 = 콜라이더 없음. 2차: 명륜3가 골목(체크포인트 4→6) 양옆 단독주택·담장·축대 14개(06 문서 5장 끝)
  - `Walls` — 한양도성 성곽: 체성(폴리라인을 두께 2.5m 로 세운 벽, 기초 −1.5 ~ +4.5m, 돌 줄눈 텍스처) + `미석` + `여장(타·타구)` + `옥개석·총안`, 충돌은 체성 위로 10m 더. 이름표 '한양도성' 2곳
  - `Stairs` — 보이는 단(충돌 없음, 단 윗면이 경사선 위아래로 반 단씩) + 자식 `경사판 충돌`(보이지 않는 BoxCollider, Ground)
  - `Landmarks/<종류>` — 전봇대(+전선 두 가닥)·가로등·표지·정류장·전단 보드·출입문(가장 가까운 건물 면에 붙임)·암문(성벽 안쪽 면)·전망 표지·전투 무대 원(반투명)·원경(말바위·북악산 = 둥근 산, 성북동 지붕·남쪽 도심 = 상자 무리, 충돌 없음). 배드민턴 네트(새벽 전용)는 꺼 둠
  - `Route/CP01~CP10` — 부품 `원판`·`테두리`(걷는 면에 붙음) · `기둥`(3m)·`깃발` · `빛 기둥`(30m, 위로 투명) · `이름표` · `트리거`(반경 × 3m, Ignore Raycast). 색 = 길잡이 주황 #E2582C(시작 지점과 겹치는 1번은 기둥·깃발·빛 기둥 없음). `Route` 에 **`RouteGuide`**(순서대로 켬: 지금 목표만 기둥·빛 기둥·깃발·이름표, 지난 것은 원판만 옅게, 안 온 것은 숨김) — 공개 API `RouteGuide.Current`·`CurrentTarget`·`Index`·`Distance`·`Reached`/`Completed` 이벤트(06 문서 8장)
  - 장면 루트 `HUD 길잡이` — Canvas + `RouteHud`: 화면 위 가운데 "다음: 후문 상가거리 · 20m"(TMP UGUI, 한글). 도착하면 1.6초 "도착: …"
  - `Zone1` 에 **`NameTags`** — 이름표 29개(체크포인트 10 · 성곽 2 · 관문·전망·암문 · 정류장·표지 · 출입문 · 전단 보드)를 카메라 쪽으로 돌리고, 거리 밖·가림이면 숨기고, 화면에서 겹치면 중요한 것만 남김(06 문서 7장 끝)
  - `Spawn` — 시작 자리(위치·yaw = zone1.json spawn: 2차부터 성대 후문 앞 **차도 남쪽 차선** (−197.4, 30.7, 97.3) yaw 34°, 07 문서 4-9) + 바닥 화살표. (`ScaleRef_Siwoo (EditorOnly)` 키 기준 인형은 M1 리그가 붙을 때 `M1Setup` 이 지운다)
  - `Bounds` — 경계 사각형 네 변의 보이지 않는 벽(PlayerOnly)
  - `Shots` — 배치 스크린샷 자리(꺼진 Camera 로 화각 포함): `Shot_Start`(시작 지점 눈높이 1.6m) · `Shot_StartCam`(시작 지점 게임 카메라 4m·8°·FOV 45) · `Shot_Uphill`(시우네 앞 → 꼭대기 계단) · `Shot_Waryong`(공터·성벽·성곽길) · `Shot_Reveal`(계단참에서 성곽)
- 장면 루트에 `Directional Light`(Sandbox 와 같은 색·세기·부드러운 그림자, 방향만 남남동 위 48°) · `Global Volume (Film)`(Sandbox 와 같은 필름 레시피) · `Main Camera`(SMAA, 후처리, 처음 자리 = Shot_StartCam). 하늘 = #EEE3D1, 안개 = #D4CEC3(하늘보다 L* 7 어둡게 — 먼 실루엣이 하늘에 안 녹게), 선형 안개 40~380m.
- **재질**: 06 문서 10장 색표(zone1.json `colors`, 2차 = 명도 사다리) 그대로 종류별 URP Lit 단색(스무스니스 0, 스펙큘러·반사 끔). 성곽 = 돌 줄눈, 성곽길·돌 계단참 = 판석 생성 텍스처 × 색(메시 UV 가 미터 단위). 체크포인트·전투 무대 표시만 URP Unlit 반투명.
- **레이어**(07 문서 3-5, 빌더가 비어 있으면 이름을 넣음): 6 Ground(지형·길·패드·계단 경사판) / 7 Wall(건물·담·성곽·옹벽·차단물·후문 기둥) / 8 Player / 9 PlayerOnly(전봇대·가로등·표지·정류장·난간·나무 줄기·소품·경계벽) / 10 Interact / 11 CamBlock. 물리 충돌 행렬(Player×CamBlock 끔)은 아직 안 건드림.
- **지형 깎기**: 2m 격자 삼각형이 좁은 길(2.2~3m) 위로 최대 0.5m(남쪽 인도 1.2m) 솟아서, 걷는 면 둘레 2m 안 격자점을 그 면 높이 −0.12m 아래로 내린다(980점, 최대 1.59m). zone1.json 은 그대로 두고 장면에서만 깎는다.
- **자기 점검**(로그 `[Zone1Builder]`): 블록·기둥 바닥이 땅보다 뜨면 아래로 늘리고 기록 / 체크포인트·시작 지점 발밑 높이 / 길·계단을 시우 캡슐(반지름 0.25·키 1.74)로 0.5m마다 훑어 건물·담에 걸리는지 / 시우 크기 NavMesh 를 임시로 구워(저장 안 함) 체크포인트 1→10 이 이어지는지 — '길만'(지형은 걸을 수 없음) · '맨땅 포함' 두 판.
  2026-10-05 결과(2차): 떠 있던 블록 보정 11개(최대 1.05m, 주차장 난간·동쪽 옹벽·계단 옆 단층집 등 낮은 땅 쪽) · 묻힌 블록 0 · 기둥 바닥 보정 5개(최대 0.98m) · 체크포인트 높이차 최대 0.07m · 길 가운데선 막힘 **0**(골목 벽을 세운 뒤에도) · 가장자리만 닿음 11곳(인도 연석 쪽 전봇대·정류장, 주차장 난간 0.1m) · NavMesh 9구간 **모두 완주**(카메라 2차 시작 자리 기준 길만 206.7m / 맨땅 포함 199.0m; 06 문서 그래프 거리 227.6m 보다 짧은 건 넓은 면을 대각선으로 가로질러서).
- **결정적 저장(2차)**: 저장 직후 `StableIds` 가 장면 fileID 를 계층 경로 해시로, 메시 서브 에셋 fileID 를 메시 이름 해시로 바꾸고 다시 읽어 깨진 참조 0 을 확인한다(로그 `결정적 저장: …`). 재질·메시 에셋·필름 볼륨·생성 텍스처·글꼴 에셋은 제자리 갱신(GUID 유지). **같은 zone1.json 으로 다시 돌리면 Zone1.unity·Zone1_Mesh.asset·재질이 바이트 단위로 같다**(2026-10-05 두 번 돌려 확인) → 커밋 diff 는 실제로 바뀐 오브젝트 줄만. 오브젝트 이름·순서를 바꾸면 그 아래 fileID 가 바뀐다.
- **한글 이름표·HUD 글꼴(2차)**: `_Project/Fonts/NotoSansKR-Bold.ttf`(이 PC 의 NotoSansKR-VF.ttf 2.04 를 굵기 700 정적 인스턴스로 뽑은 것, OFL 1.1 — `OFL.txt` 동봉) → `KoreanFont.Ensure` 가 TMP 동적 글꼴 에셋 `KR_Bold_SDF`(표본 64pt, 2048 아틀라스, 모자라면 아틀라스 추가)를 없을 때만 만든다. Clear Dynamic Data On Build 켬 + 빌더가 끝에 아틀라스를 비워 저장 파일이 늘 같은 빈 상태. 이름표 재질 `KR_Label` = 모바일 SDF 오버레이(깊이 검사 없음 — 벽에 반쯤 묻혀 잘리지 않음, 가려진 이름표는 `NameTags` 가 숨김) + 종이색 외곽선.
- **시작 화면(2차에서 고침)**: 1차 시작 자리(남쪽 인도, 닫힌 후문 차단 펜스 앞 2m)는 등 뒤 4m 카메라가 펜스 너머에 놓여 `CamClearance` 다리 가림 검사가 카메라를 2.16m 로 당겼다(인물이 화면 2/3, 무릎 아래 잘림). 시작 자리를 차도 남쪽 차선(펜스에서 4.5m)으로 옮겨 정적 자리 `Shot_StartCam` 과 실제 게임 카메라 모두 펜스 앞 인도 위에 선다 → 거리 4.0m · 당김 0 · 인물 화면 높이 50% · 발 아래 10%(T21). 펜스·담장은 판이라 `Wall` 그대로(레이어로 고치지 않은 이유: 07 문서 3-5 끝).

### 맵 아트 패스 (`EnvSetup.AttachKit` 이 Zone1 에 붙이는 것, 2026-10-24 — docs/11 7장 체크리스트 · docs/10 P1·P3 · docs/09 6-5-3)
- **입력** `Art/Env/`(위 폴더 구조). 모두 `concept/art/env` 의 복사본 — 원본은 손대지 않는다. zone1.json 이 바뀌어도 건물 FBX 는 `tools/env/build_env.py` 로 다시 만들어야 바뀐다(자판기 자리만 옮긴 2026-10-24 는 FBX 바이트 그대로임을 확인).
- **장면 루트** `Zone1/Env/Z1_<구역>`(FBX 프리팹을 **완전히 풀어서**(Unpack) 원점에 — 결정적 저장의 fileID 가 계층 경로 기준이라 프리팹 인스턴스보다 안전) · `Env/Lot`(주차장 배치) · `Env/Props`(회색 소품 교체) · `Zone1/Sky`(원경) · `Lights/빛 Sock_*`(키트 빛 자리 11 + 상자 창 판 대신 키트 `WinLit_*` 39묶음).
  - 건물·담·성곽·전봇대: `_LOD0/1/2` → LODGroup(전환값 = manifest `lod_screen_heights`, 컬링 0.006), `_Detail_LOD0` 는 LOD0 에 같이·그림자 끔. 모두 Static(Batching·Occluder·Occludee), 레이어 Default, 충돌 없음(**충돌은 회색 상자 콜라이더 그대로** — M1 T14·카메라 불변).
  - 소품 자리 `Prop_<소품>_<n>` 338 → `Prefabs/Env/P_<소품>` 프리팹 인스턴스(자식, 위치·회전 0). LOD 전환 = docs/10 10장(12m 50% · 30m 20% · 1m 미만 60m 컬링)을 화면 높이로 환산.
  - `WinLit_<구역>_t##` → `LightSpot(Window, Threshold = 문턱, Weight = 판 수)` + 렌더러 꺼짐(DayLight 가 프리셋 `Windows` 비율로 켬, 발광 = `M_Glass_Lit`). `StorySetup.AddLights` 의 상자 창 판 260개는 키트가 있으면 만들지 않는다.
  - `Sign_<n>` → 재질 `M_Sign_<아틀라스 칸>`(Tiling/Offset 으로 `sign_atlas.json` 사각형) + 자식 TMP 글자(상호 = manifest `tenant`, Black Han Sans(KR_Brush_SDF) 또는 Noto(KR_Bold_SDF), 색은 docs/10 7장 표, 세로 간판은 글자 세로 쌓기, projecting 은 양면). 1층 띠와 같은 높이에 온 2층 띠(번개배달)는 위로 올림.
  - `Sock_shop_light`(7) → 점광 ShopSign · `Sock_ceiling_light`(2) → Security · `Sock_lamp_globe`(2) → Street(흰빛 고정). 가로등 빛 반지름 9m·보안등 7m(빛 웅덩이).
  - **회색 상자 숨김**(MeshRenderer 만 끔, 콜라이더 유지): `Blocks/{shop,house,hanok,campus,pavilion,gate,fence,retaining,railing}` · `Walls`(성곽) · `Landmarks/{pole,door,sign,vista,wall_gate}` + `전선`(이름표 TMP 는 남김). 키트 밖 회색 소품은 GLB 로 교체: 벤치 → `park_bench`, 운동기구 → `exercise_machine`, 마을버스 정류장 → `bus_stop_shelter`, 보안등 → `street_lamp_trail`(점광 자리는 그대로). 나무·풀숲·가로등(4.5m)·표지·차단 펜스는 아직 회색.
  - **주차장(09 3-2 원고 동선)**: 서쪽 상가 뒷벽(= 국밥집 뒷벽으로 삼음)을 따라 로컬 z 남 → 북: 시우 자전거(−5.0, `delivery_bicycle`) · **자판기(−3.0, zone1_gen `prop` 상자 자리 그대로 — 충돌·HeatSurface·빛 자리 기준)** · 바퀴 쓰레기통(−1.95) · [서쪽 계단 입구 −1.7~1.7 비움] · LPG(2.3) · 솥(3.1) · **국밥집 뒷문(4.2, 녹색 철문 판)** · 상자(5.3). 입구 서쪽 절반에 내려간 차단기(`parking_barrier`, 걷는 길은 동쪽). `Settings/Encounter_Y4.asset` 의 적 대기 자리를 자판기 앞(서쪽 벽에서 3~4.5m)으로 옮김(에셋 값 덮어씀).
  - **바닥·길 재질**: Zone1Builder 의 `M_Z1_{road,driveway,alley,asphalt,sidewalk,wallpath,pad_stone,stairs}` 를 아트 텍스처(M_Asphalt 4m · M_Paving 1.2m · M_Granite 1.5m) + UTS 로 바꾼다(가로등 빛 웅덩이가 툰 단으로 끊기게). 지형 메시(`ground`)는 UV 가 없어 단색 그대로.
  - **원경**(`SkyCards`, ExecuteAlways): 카메라를 따라다니는 반지름 600m 호 4장(북·동·남 + 서 = 북 카드 좌우 뒤집음, `Haengin/SkyCard` — 층 마스크 R/G/B × 시간대 색, 밤엔 가운데 층 창 불빛 점) + 구름 5(낮·노을) + 초승달(밤). 색은 `sky_palette.json`(= LightingPreset 하늘색), `DayLight.Apply` 가 함께 바꾼다. 안개·조명 없음, 깊이 안 씀. 회색 '원경' 산·상자는 숨김.
- **재질**(`EnvSetup.Toon`): UTS `Toon/Toon`, Base = 텍스처(Tiling = 1/한 장 m, manifest), `Use BaseMap as 1st`, 1단 그림자 = 고유색 × (.78,.76,.74)(2단 색 = 1단 색 → 한 단), 스펙큘러·림·MatCap 0, **헐 외곽선 패스 끔**(`SetShaderPassEnabled("SRPDefaultUnlit", false)`), `Unlit_Intensity 1`(주변광이 밤 밝기를 정함), GPU 인스턴싱. 함정 둘(2026-10-24 실측): ① **FBX 를 탄젠트 없이(`importTangents None`) 들이면 UTS 그늘 면이 검게 된다**(UTS 는 노멀 맵이 없어도 탄젠트 공간을 쓴다) → `EnvImport` 가 CalculateMikk. ② UTS 에는 DepthNormals 패스가 없어 **SSAO·법선 텍스처를 쓰는 기능은 UTS 면에서 깨진다** → 렌더러의 SSAO 를 껐고(09 3-7 '블룸·SSAO 없음'), 화면 공간 외곽선은 깊이만 쓴다.
- **화면 공간 외곽선**(`Haengin/ScreenOutline`, 렌더러 기능 `Outline`, 후처리 앞, 요구 = 깊이·색, 카메라 깊이·스텐실 바인딩): 실루엣(이웃이 더 멀면) + 모서리(깊이 2차 차분), 굵기 1.5px → 30m 0.75px → 55~80m 0, 색 #1A1417. 캐릭터 UTS 재질은 스텐실 1 을 쓰고(StencilMask) 외곽선 패스는 `Ref 1 NotEqual` 로 건너뛴다(캐릭터는 헐 외곽선 — 선 위계 09 3-5).
- **2차 패스(2026-10-24, `EnvSetup.Decor.cs`)**: `Env/Decor/{바닥,벽,잡동사니}` + `Env/Veg`.
  - 바닥: 지형 메시에 평면 UV(월드 m)를 넣어 `M_Dirt` 적용(Zone1Builder.Smooth) · 인도 양 가장자리 연석(상자 0.14×0.07, 충돌 없음) · 후문 앞 횡단보도(7줄) · 골목·진입로가 큰길과 만나는 곳 정지선 · 큰길 맨홀 40m 마다 · 인도 빗물받이 20m 마다. 재질 `M_Paint_White`·`M_Iron_Dark`(UTS 평면색).
  - 벽: 건물 정면·길가 면 밑 얼룩 판(`M_Decal_Atlas` grime 14칸, 이름 해시 시드) · 주차장 옹벽(남·동) 낙서 4 · 전봇대·게시판 포스터(`M_Sign_Atlas` poster 8칸). 판 셰이더 `Haengin/DecalQuad`(알파 블렌드, 깊이 오프셋, 안개, 주변광·해 밝기만).
  - 잡동사니: 골목 5줄 가장자리 4~5.5m 마다 화분·대야·박스·화분 식물·풀을 번갈아(가운데 1.2m 비움, 축대 위면 건너뜀) · 전봇대 밑 쓰레기 봉투 60%.
  - 남은 회색 상자: 가로등 4.5m → `street_lamp_cobra` 0.62배 · 표지판 → 기둥 + 흰 판 + TMP 글자 · 전단 게시판 → 회색 금속 재질 + 포스터 4장 · 차단 펜스 → `M_Metal_Green`, 차단물 → `M_Metal_Grey`. 필로티 2블록 콜라이더는 **바꾸지 않음**(키트가 1층 앞을 철책 1.5m 로 막아 두어 열어도 창살을 통과하게 됨 — 키트에 문을 내는 결정이 먼저).
  - 밤 보안등 +1: 시우네 앞 골목(−162.3, 121.1) `street_lamp_trail` + Security 점광.
  - **식생**(힉스필드 GPT Image 2.5 참조 8장 + Hunyuan3D v3 8개 = 110cr, `tools/env_assets/specs/veg.json`): `pine_tree`·`cherry_tree`·`zelkova_tree`·`shrub_bush`·`ivy_patch`·`potted_plant_a/b`·`grass_tuft`. 회색 나무 30 → 종(성균관로 z 93~109 = 느티나무, 캠퍼스 = 벚나무, 그 밖 = 소나무, 높이에 맞춰 0.85~1.45배, 줄기 캡슐 콜라이더 그대로) · 풀숲 41 → 관목(상자 크기로 늘림) + 풀 50% · 담 40% 담쟁이 · 축대 위 풀 2.2m 마다 · 성곽 안쪽 1.6m 띠에 관목·풀 7m 마다. 셰이더 `Haengin/ToonLeaf`(1단 그림자 + 주변광 + Forward+ 점광 툰 램프 + 안개 + 바람 정점 흔들림(높이 비례, 그림자 캐스터도 같이)). 나무 LOD 35/90m, 작은 풀 8/20m·45m 컬링.
  - 색 보정: `LightingPreset` 에 채도·대비·Lift/Gamma/Gain·천정색 — `DayLight` 가 필름 볼륨 복제본의 `ColorAdjustments`·`LiftGammaGain` 에 넣는다(낮 채도 +8·대비 +5·따뜻한 Gain, 노을 +4, 새벽 −4 청색, 밤 −2 남색). 하늘 = 원경 카드 셰이더가 지평선 하늘색 → 천정색 그라데이션(호 메시 위로 2.4H), 카메라 단색은 그 뒤.
- **3차 패스(2026-10-24, `EnvSetup.Pass3.cs`)**: 길바닥 중앙선(노랑)·배수로 띠·주차선·바닥 얼룩(곱하기) · 가로수 줄(`VegetationRows`: 성균관로 인도 바깥 9m 마다 느티·플라타너스, 성곽길 소나무·관목, 와룡 오르막 관목·소나무, 담 위 화분·풀) · 잎 카드(`AddLeafCards`: 나무 프리팹 수관 둘레 14~16장, `Haengin/ToonLeafCard`, 텍스처 `Art/Env/Textures/Leaf_{pine,broad,cherry}.png` = `python tools/env_assets/leaf_cards.py` 가 참조 그림을 흰색 키잉) · `DecalQuad` 바랜 톤·거친 가장자리·곱하기 모드 · 밤 상가 점광 주황 9m·창문 55%·하늘 청회색 · 원경 반지름 480·지평선 +0.06H · 전봇대 LOD 앞당김 · **필로티 콜라이더 교체**(`PilotiColliders`: 키트가 manifest meta 에 적은 기둥·앞선·뒷선·통로로 위층·안쪽 벽·옆 벽·기둥·철책 콜라이더를 짬 — 키트 `_piloti` 는 철책 가운데 1.3m 를 비움). 식생 9종(플라타너스 추가, 13.75cr) + 잎 카드 참조 3장(8.25cr).
- **4차 패스(2026-10-24, `EnvSetup.Pass4.cs` + 키트 `tools/env/envkit/dense.py`)** — 결과·수치는 docs/09 6-5-3 '4차 패스'.
  - **벽면 밀도는 키트 생성기**: `dense.densify` 가 villa·shop·house 마다 면별 구멍(`emit_openings` 가 `Out.plog` 에 남김)·간판·소품 자리·외부 계단(`Out.stairs`)을 피해 가스관(세로관 1/4.6m + 땅에서 오는 L + 계량기 + 층 가지 U, 실외기 뒤 0.05m)·실외기·보일러 연통·빨래·방범창·출입구 둘레 화분·계량기·인입선 받는 자리를 더한다. 본체 난수와 따로(`rng(b.key, "dense")`). 소품은 기존 소켓 경로 그대로(338 → 532). 다시 만들기: `blender -b --factory-startup -P tools/env/build_env.py` → `concept/art/env/buildings/{Z1_*.fbx,zone1_buildings_manifest.json}` 를 `Art/Env/Buildings/` 로 복사 → `EnvSetup.Build`. 새 재질 `M_Fabric_White`·`M_Fabric_Red`(manifest 색).
  - **카드 전용 수관**(`BuildCardTree`, `PropPrefabs` 가 나무 4종 + `shrub_bush` 에 대해 Hunyuan 대신 부름): 가지 뼈대(관 메시, `M_V_bark_<종>` = ToonLeaf 평면색) + 잎 덩어리 카드(`M_V_crown_<종>` = ToonLeafCard `_Sphere 1`·`_Ink 1`·`_StencilRef 1`), 메시 `Art/Env/Props/CardTrees/<종>_{bark,crown}{0,1}.asset`, 텍스처 `Art/Env/Textures/Clump_{broad,pine,cherry}.png`(2×2 아틀라스, `concept/art/env/veg/clump_*.png` 에서 없을 때만 복사 — EnvImport: Clamp·알파·밉맵 coverage 보존). 크기 = 옛 Hunyuan GLB 높이·폭. LOD0 32m 까지, LOD1 = 덩어리당 큰 카드 3장(덤불 18/45m).
  - **하늘**: `SkyCard` = 아랫줄 집기(uv.y ≥ 0.012) · 붓 띠 7단 그라데이션(지평선 띠색 `_HorizonColor`) · 절차 붓 구름(층운·적운, 실루엣 뒤 — `_CloudLit`·`_CloudShade`·`_Cover`, `SkyCards.Palette` 의 `Horizon`·`CloudShade`·`Cover`) · 큐 AlphaTest+49(불투명 맨 끝). 호 메시 = 세로 0.62배(`SkySquash`)·0.2H 내림(`SkyDrop`)·아래 −0.9H·위 2.4H 는 반지름 0.55배(먼 면 600m 안). 구름 판은 안 만듦(달만).
  - **외곽선**: `M_Outline` 값은 `OutlineFeature` 가 매번 덮어씀 — 굵기 4m 2.4px(`_NearWidth`)·거리 지수 0.55·최소 0.55px(1px 아래는 진하기), 해상도 비례, 역깊이 모서리 `_CreaseInv` 0.0045, 실루엣 0.022·0.03, 근경 손그림 결 `_Grain` 0.035·`_Stain` 0.05(8~22m).
- **5차 패스(2026-10-25, `EnvSetup.Pass5.cs`·`Pass5b.cs`·`Pass5c.cs`·`Shots5.cs` + 키트 `dense.py`)** — 결과·프레임 대조 목록은 docs/09 6-5-3 '5차 패스'.
  - **간판**: 면 그림 `Art/Env/Signs5/Face_*.png` + `faces.json`(만들기: `tools/env_assets/gen_images.ps1 -Spec specs/signs5.json`(+`signs5b`·`signs5c`) → `python tools/env_assets/sign_faces.py` — 알파로 자르고 평균·빛 색을 적음; **새로 뽑으면 한국어 글자를 한 장씩 눈으로 확인**). `MakeSign5` 가 키트 `Sign_*` 자리마다 몸체(뒤 상자·테두리·볼트·배선관·받침 쇠 / 돌출 상자·뚜껑·보강대 / 명판)를 만들고 면 메시(`SignMeshes.asset` 서브 메시, 그림 비율 유지 + 가장자리 띠 늘이기)를 붙인다. 그림이 없으면 예전 TMP. 규칙 배치 `Signs5Rules`: 모서리 층별 3 · 창 시트지(키트 `win_sheet` 소켓) · 입간판 A형 4 · 원통 1 · LED 1 · 어닝 2 · 셔터 1 · 가게 안 8 · 혜화 MMA 입구(키트 `gym_door`) · 주소판(키트 `addr_plate`). 셰이더 `Haengin/SignFace`(낡음·밤 조명 상자·LED·유리 반사 띠·`_DayDim`) · `Haengin/SignGlow`(빛 번짐, 모드 1 = 가로등 날벌레) · `Haengin/WindowLit`(켜진 창 커튼·블라인드·실루엣 — WinLit 판 재질을 바꿈). `DayLight` 가 전역 `_HaenginSignsOn`(노을·밤)·`_HaenginNight` 를 넣음.
  - **거리·표면·움직임**: `Street5`(편의점 앞·세운 차·반사경·라바콘·볼라드·전봇대 안전띠·변압기·평상·벽화·현수막·가로등 배너·`Fills5`: 배기 덕트·필로티 기둥 띠·감나무·화분 상자) · `Surface5`(데칼 아틀라스 3×3 `Face_decals5.png`: 빗물·녹물(키트 `streak`)·아스팔트 균열·땜질·틈 풀·이끼·계단 닳음) · `Motion5`(빨래 천 = 키트 `cloth` 소켓 → `Haengin/ToonCloth`, 비둘기·고양이 = 런타임 `Scripts/Env/AmbientIdle.cs`, 날벌레). 감나무 = 카드 나무 `persimmon_tree`(열매 메시).
  - **키트(dense.py)**: 담장 `fence_clutter`·마당 대문 `gate_clutter`(가스관·계량기·우편함·주소판·초인종) + 소켓 `win_sheet`·`streak`·`cloth`·`gym_door`·`addr_plate`(manifest `Sock_*` info 에 `size`·`tenant`). `AttachKit` 은 이 소켓을 빛 자리로 만들지 않고 `socks5` 로 넘긴다.
- **6차 패스(2026-10-25, `EnvSetup.Pass6.cs` + `Scripts/Env/SignAudit.cs`·`GlyphCheck.cs` + 셰이더 `LightPool` · 시험 `SignTextTests`)** — 결과·수치는 docs/09 6-5-3 '6차 패스'.
  - **간판 점검**: `AttachKit` 은 키트 `Sign_*` 를 모았다가 `PlanSigns`(같은 면 겹친 띠는 큰 띠 위로, 창(`WinLit_*` 사각형)과 겹치는 띠는 창 사이로 줄이고 0.30m 도 안 남으면 뺌 — manifest info 의 `center`·`size_m` 을 고쳐서) 뒤에 짓는다. 창 시트지는 `Sheets6` — 창살(문설주) 사이 칸마다 낱말, 간판 몸(잠시 상자 콜라이더 층 30) 앞이면 안 붙임, 방범창 창은 키트(`dense.py`)가 소켓을 안 냄(`mull` 은 소켓 info 로). 점검 `SignAudit`(면 = 이름 `면`·`면 A`·`면 B` + `창 시트지 …` 묶음): 글자 빠짐(TMP `HasCharacter` 대체 글꼴 포함 + □ 칸) · 면끼리 겹침 · 키트 `*_LOD0` 가 면 앞 1.6m 에서 가리는 비율(입간판 빼고 15%) → 빌드 메모 + 시험 `S01_Sign_Glyphs`·`S02_Sign_Layout`. 빌드에선 `Env/글자 확인`(GlyphCheck)이 `[Env] 간판 글자 확인: TMP N개 · 빠진 글자 0` 한 줄.
  - **정류장 광고**: `BusAds6` 가 정류장 GLB 메시에서 세로 광고 상자(양면)와 뒤판 유리 두 칸을 정점으로 찾아 가상 광고 2장(`ad_concert`·`ad_academy`, `specs/signs6.json`)을 붙임 — 다시 뽑으면 글자 확인.
  - **초록 `Green6`**: 1m 격자로 위에서 쏜 빛이 `Ground/Terrain` 에 바로 닿는 칸 = 맨땅 → ① 지형 정점마다 알파를 둔 풀색 **곱하기 덮개**(`M_G6_GrassOverlay` = DecalQuad `_AtlasUV 1`, 텍스처 `Textures/Grass6.png` 은 매번 절차로 다시 씀, 큐 2550, 집·상가 둘레 6m 는 옅게) ② 잡초 카드(X 두 장, `Clump_weed`) — 맨땅 가장자리 진하게 ③ 담·축대·건물 밑 틈 풀(옆으로 쏜 빛) ④ 관목 덩이(`Clump_broad`)·흰 꽃 관목(`Clump_flower`, 산책길 가장자리) — 모두 `ToonLeafCard _SwayUV 1`(흔들림 무게 = uv2.y)로 48m 칸마다 한 메시.
  - **밤 빛 `Pools6`**: `Haengin/LightPool`(미리 곱한 알파 — 남색 바탕을 조금 덮고 주황을 더함, 젖은 반사 없음, `_HaenginNight`·가게는 `_HaenginSignsOn` 일 때만) — 가로등·보안등 아래 원, 1층 가게 앞 사각(유리에서 4.6m). 땅 높이에 맞춘 8×8 격자.
  - **그리기 호출 줄이기**: `Merge6`(Decor·Signs5·Street5·Surface5·Motion5·Rails6·BusAds6·Lot·키트 간판 몸·Roads·Ground·Stairs — 스크립트·LOD·TMP·간판 면·`Terrain` 은 그대로, 재질 × 48m 칸, 데칼은 아틀라스 칸·알파를 정점 uv2·색에 굽고 재질 하나 `M_D6_*`) · `PropMerge6`(작은 소품 LOD 3단을 재질 × 12m 칸으로, 칸 LOD 7/16/36m) · `BakeText6`(구역 TMP 를 그때 아틀라스 사본 `Art/Env/Pass6/TextAtlas6_*.asset` + 정적 메시로, TMP 컴포넌트는 꺼 두고 글자는 남김) · 키트 `blend.py` 먼 LOD 재질 줄이기(LOD1 4·LOD2 2, 기본색 가까운 쪽으로)·LOD0 에 디테일 합침 · 대각 1m 미만 그림자 끔. 메시는 `Art/Env/Pass6/Meshes6.asset` 한 곳(다시 지을 때 제자리 갱신, 안 쓴 것 지움). 어림 도구 `EnvSetup.DrawBreak5`(LOD 고른 뒤 서브메시 수), D08 `-tribreak 1` 은 묶음별 그리기·SetPass 도 적음(값에 고정 오프셋이 있어 합계로 판단).
- **7차 패스(2026-10-25, `EnvSetup.Pass7.cs` + 셰이더 `ScreenRim` · `DayLight` 전역 `_HaenginRim`·`_HaenginRimColor`)** — 결과는 docs/09 6-5-3 '7차 패스'. 역광 = 렌더러 기능 `Rim`(외곽선 다음, 스텐실 없음, 하늘 = 깊이 1,500m 넘음) · `PlanPlaza7`(초록·난간보다 먼저 광장 자리) → `BuildPlaza7` · `Ivy7`(담쟁이 카드 `Clump_ivy`, `specs/veg7.json`) · `WaryongWall7`(막돌 축대) · `Parking7`(상자·화분 줄) · 원경 회전 `SkyYaw7` 50° · 노을 해 70° · 밤 주변광·안개 중성 · 큰길 전봇대 웅덩이. 개발용 `EnvSetup.Shots` 에 `-skyYaw <도>`(원경 돌려 보기), `ProbeShot6`(재질 하나 근접 사진), `ProbeWaryong7`(길 양쪽 땅 높이).
  - **난간**: `Rails6` — Zone1 계단 5곳 손잡이(폭 2.4m 이상 양쪽) · 주차장 옹벽 머리 쇠 난간 · 성곽길 비탈 쪽 나무 난간(갈림길 입구는 비움). 성균관로 전봇대 2·3·4 는 간판 띠 바로 앞이라 `tools/zone1_gen.py` 에서 가게 사이로 옮김.
  - **함정**: 이름 있는 튜플을 삼항 연산자로 받으면 이름(.a·.b)이 사라짐(명시 타입으로) · 큐브 프리미티브는 면마다 UV 0~1 이라 줄무늬 방향은 텍스처로(셔터 = 가로 줄 텍스처 + 세로 Tiling) · 샷 배치(그래픽)는 끝날 때 가끔 Mono 정리 중 접근 위반(종료 코드 −1073741819) — 사진·로그는 다 저장됨 · 동적 글꼴 `KR_Bold_SDF.asset` 이 배치 뒤 바뀌면 `git checkout` 으로 되돌림.
- **조명 프리셋**: `Light_Dawn·Dusk·Night` 값을 docs/10 9장으로 덮어씀(EnvSetup.Presets 가 매번 — 손으로 고치려면 그 코드에서). 밤 = 해 0.22·주변광 (0.26,0.29,0.42)·하늘 #222A44·안개 #141829·가로등 10 → 빛 웅덩이 사이는 어둡다. D07 밤 기준을 '웅덩이 위 최대 L* ≥ 22 · 최소 ≥ 8'로 바꿈(M3LightTests). 그림자 캐스케이드 2단·60m(PC·Mobile RP 에셋).

### M1 플레이어·카메라 (`M1Setup` 이 Zone1 에 붙이는 리그, 2026-10-05)
- **2026-10-24 탐색 카메라 B**: 야외 탐색 = 거리 5.5(달리기 5.9)·피치 3°·FOV 53(57)·겨누는 점 1.15m(`CamTuning.exploreTargetOffset` −0.25 → `CamRig` 가 OrbitalFollow·RotationComposer `TargetOffset` 에 매 프레임, `CamRig.TrackPoint`·`Distance`·`CamClearance` 도 그 점 기준). CamTarget(1.40)·전투 카메라·실내(`SceneLoader.Indoors` 2.8·FOV 50·피치 8·오프셋 0)·체육관(3.0·FOV 50)은 그대로. 아래 4.0m·FOV 45 숫자는 예전 A. 테스트 기준은 docs/09 6-5-3 '탐색 카메라 B 확정'.
설계 = `docs/07_M1_조작_설계.md`. 숫자는 `Settings/MoveTuning.asset`·`CamTuning.asset`(없을 때만 만들어서 손으로 고친 값이 남음).
- **장면 루트**: `Player`(프리팹 `Prefabs/Player.prefab` 인스턴스, spawn 위치·yaw, 발 = 그 아래 Ground 면) · `Main Camera`(Zone1Builder 것을 그대로 쓰고 `CinemachineBrain`·`DebugHud` 를 붙임) · `CM_Explore` · `Zone1/Route` 에 `RouteData`(체크포인트 위치·반경·시작 지점 — 테스트와 나중의 체크포인트 진행 코드용).
- **Player**: 태그·레이어 Player. `CharacterController`(높이 1.74·반지름 0.25·skin 0.025·중심 0.895·턱 0.30·경사 40°·minMoveDistance 0) + `PlayerMotor` + `PInput` · 자식 `Visual`(`BodyLean`) › `Siwoo_Model`(**시우 리깅 FBX**, 회전 0·배율 1·바인드 자세 키 1.74, `M_SiwooAnim_Toon`, `Animator`(Siwoo.controller, 루트 모션 끔, 늘 계산) + `LocoAnim`) · `CamTarget`(높이 1.40).
- **애니메이션(3차, 2026-10-06, 07 문서 5-4·5-5)**: 블렌드 트리 대기(0, Idle_6)·걷기(1, Quick_Walk ×0.980)·달리기(2, Run_02 ×1.103) — `LocoAnim` 이 실제 수평 속도로 `Blend`·`Rate` 를 넣는다(0.5~1.4 m/s 는 걷기만 느리게, 1.4~4.5 는 걷기→달리기). 재생 배율 = 게임 속도 ÷ 클립 고유 속도(걷기 1.43·달리기 4.08 m/s, `CharSetup` 이 디딤발 속도를 재서 넣음, 걷기 분당 118걸음) → 디딤발 계통 미끄러짐 걷기 +0.006·달리기 +0.016 m/s(T25). 클립마다 발바닥이 바닥에 닿게 루트 높이 오프셋도 `CharSetup` 이 메시로 재서 넣는다(대기 −0.035·걷기 +0.050). 클립은 제자리(Bake Into Pose), 이동은 그대로 `PlayerMotor`.
- **이동**(`PlayerMotor`): 걷기 1.4(2026-10-06 1.6 → 1.4, 걷기 클립에 맞춤) / 달리기 4.5 m/s(버튼을 누르는 동안), 0→걷기 0.12초 · 걷기→달리기 0.4초 · 감속 18 m/s², 몸 회전 720°/s(달리기 540°/s), 중력 20 m/s², 땅 붙이기 0.35m, 경사에서도 수평 속도 유지, 40° 넘는 면은 미끄러져 내려옴, 맵 아래(`bounds.min.y − 10`)로 떨어지면 마지막 안전 지점으로. 점프 없음.
  공개 API: `SetMoveInput(Vector3 worldDir, float amount01, bool run)` / `SetMoveInput(Vector2 worldDirXZ, bool run = false)`(한 프레임만 유효 — 매 프레임 넣는다) · `ClearMoveInput()` · `Teleport(feet, yaw)` · `Position` `Grounded` `State`(Idle/Walk/Run/Fall) `PlanarSpeed` `Velocity` `LastSafePos` `Respawns`.
- **몸 기울기**(`BodyLean`, 3차에 애니메이션과 겹치지 않게 줄임): 속도 기울기 0(달리기 클립이 이미 숙임) · 가속 1 m/s² 당 0.15°(앞뒤 −4~+8°) · 도는 쪽으로 ±6°, 턱을 오를 때 비주얼 높이를 0.08초에 걸쳐 따라감, 카메라가 0.55m 안으로 오면 인물 숨김(그림자만).
- **카메라** `CM_Explore` = `CinemachineOrbitalFollow`(Sphere, 4.0m, 피치 8°·범위 −10~50°) + `CinemachineRotationComposer`(인물 화면 가로 −0.06) + `CinemachineDeoccluder`(반지름 0.10, 즉시 당김·0.5초 복귀, 얇은 PlayerOnly 는 통과) + 우리 `CamClearance`·`CamRig`·`CamInput`. FOV 45°(달리면 49°·4.3m, 실제 속도에 묶음), Q/L1 = 0.3초에 등 뒤로. 이동 기준 = 궤도 정면(카메라 정면 아님, 07 4-4).
  - **이동 기준 고정(3차, 07 4-10)**: 스틱을 누르는 동안 자동 정렬·Q/L1 정렬이 카메라를 돌린 만큼은 이동 기준에서 뺀다(`CamRig.StickToWorld`). 3차 검수의 'W+D 를 누르고 있으면 3초 뒤 45° → 119° 로 계속 돎'이 0° 로(T22·T23). 손으로 돌린 시점은 그대로 이동 방향을 돌리고, 스틱을 놓거나 방향을 바꾼 만큼 고정이 풀린다.
  - **자동 정렬(2차, 07 4-8)**: 걷기·달리기 모두. 마지막 수동 시점 조작(마우스·오른스틱) 뒤 걷기 1.8초·달리기 1.2초 기다리고, 움직인 지 0.3초 뒤부터 인물 등 뒤로 — `CamRig` 가 직접 최대 각속도 걷기 30°/s·달리기 60°/s, 각가속도 60/120°/s², 남은 각도 × 1.2/s 로 돌린다(Cinemachine 정렬은 골목에서 출렁여서 Q/L1 에만). 서 있을 때·카메라 쪽으로 걸어올 때(차 ≥ 150°)는 안 돌림. 세로는 3초 뒤 8° 로 최대 12°/s. 일시정지 메뉴에서 '걷기·달리기(기본) / 달리기만 / 끔' — 빌드에서는 `PlayerPrefs` `haengin.cam.autoAlign` 에 남는다. 수치는 `CamTuning`(새 필드 walkRecenterWait·walkAlignSpeed·runAlignSpeed·alignGain·alignAccel·alignBackAngle·pitchAlignSpeed).
  - **턱 보정(2차)**: `BodyLean` 이 비주얼과 함께 따라가는 점(`CamTarget`)도 같은 만큼 내렸다 올린다(실행 순서 −10, 브레인보다 먼저). 턱 문턱값 `MoveTuning.stepThreshold` 0.05 → 0.01(캡슐이 연석을 넘을 때 프레임당 0.01~0.04m 라 0.05 로는 한 번도 안 잡혔다). → 0.15m 턱에서 비주얼 0.024m·카메라 0.011m/프레임(T11).
  - **구현에서 더한 것 — `CamClearance`(Cinemachine 확장, Deoccluder 다음 Finalize 단계)**: Deoccluder 는 '머리와 카메라 사이를 막는 벽'만 처리해서, 골목 시험에서 카메라가 옆으로 스치는 벽에 0.05m 까지 붙었다(699프레임 중 84). 그래서 머리→카메라 선을 따라 0.05m 씩 당기며 ① 카메라 구(0.095m)가 Default·Ground·Wall·CamBlock 에 안 닿고 ② 무릎(0.5m)→카메라 선이 Wall·CamBlock 에 안 막히는(1.2m 까지만) 거리를 찾는다. 당길 땐 즉시, 풀릴 땐 0.5초. 처음엔 `Orbit.Radius` 를 줄이는 방식으로 했다가 Deoccluder 의 감쇠와 엇갈려 등 뒤 벽에서 8프레임 벽 안에 들어가서 확장 방식으로 바꿨다.
- **입력**: `Input/HInput.inputactions` 의 `Explore` 맵을 `PInput`·`CamInput` 이 같은 에셋에서 읽는다(`PlayerInput` 컴포넌트·생성 C# 클래스 안 씀). 템플릿 `InputSystem_Actions` 는 그대로 프로젝트 전역.
- **조작법**

  | 동작 | 키보드·마우스 | 패드 PS (Xbox) |
  |---|---|---|
  | 이동 | W A S D (방향키도 됨) | 왼스틱 (LS) — 기울기만큼 0.5~1.4 m/s |
  | 카메라 | 마우스 | 오른스틱 (RS) |
  | 달리기(누르는 동안) | 왼쪽 Shift | R2 (RT) |
  | 카메라 등 뒤로(락온 자리) | Q 또는 마우스 가운데 버튼 | L1 (LB) |
  | 상호작용(M1 은 자리만 — 알림만 뜸) | E | × (A) |
  | 일시정지 열기·닫기 | Esc | Options (Menu) |
  | 메뉴 고르기 | ↑ ↓ (W S), 마우스 올리기 | 십자키 · 왼스틱 위아래 |
  | 메뉴 선택 | Enter · Space · 마우스 왼쪽 | × (A) |
  | 메뉴 돌아가기(계속) | Esc · Backspace | ○ (B) |
  | 카메라 자동 정렬 바꾸기(메뉴 2번째 항목) | 선택 또는 ← → | × 또는 십자키 ← → |
  | 디버그 정보(속도·접지·카메라 거리·fps) | F1 | — |

  길잡이(3차): 다음 목표가 화면 밖·등 뒤면 화면 가장자리에 주황 방향 화살표(`RouteHud`, 화면 둘레 타원 위·카메라 기준 수평 각도), 목표 이름표는 위 가운데 길잡이 판과 겹치면 판 밑으로 내리고 화면 밖으로 삐져나가지 않게(`NameTags`). 프레임 제한: 품질 단계 모두 **vSync 1**(검수 408fps → 화면 주사율).
  화면 왼쪽 아래에 조작 안내 한 줄이 늘 뜬다(2차부터 `GameUi` — TMP 한글 글꼴 UGUI, 장면 루트 `UI 화면`). 일시정지 메뉴 = 계속 · 카메라 자동 정렬 ◀ 걷기·달리기 ▶ · 끝내기(07 문서 6-1). F1 디버그 정보(IMGUI)에 자동 정렬 상태·각속도·수동 조작 뒤 시간이 함께 나온다. 빌드는 마우스 커서를 창에 잡아 둔다(일시정지하면 풀림, 창을 다시 클릭하면 다시 잡음). 실행 인자 `-nocursorlock` 이면 잡지 않는다(자동 점검용). 시작 3초 뒤 Player.log 에 `[M1] 실행 확인: 장면 … 시우 위치 · 접지 … · 카메라 거리 … · fps(vSync · 품질)` · `[M1] 길잡이 확인: HUD …` · `[M1] 화면 UI 확인: 글꼴 … 없는 글자 0 · 메뉴 입력 Menu 맵 …` 세 줄.
- **빌드 목록**: `Zone1.unity`(첫 장면) → `Sandbox.unity`. `BatchTools.BuildWindows` 는 이제 빌드 목록의 켜진 장면을 순서대로 넣는다(비어 있으면 예전처럼 Sandbox 하나).
- 물리 충돌 행렬: Player × CamBlock 끔(07 3-5, `M1Setup.EnsureTuning`).

### 자동 테스트 (PlayMode, `Assets/_Project/Tests`, 어셈블리 `Haengin.Tests`)
시간을 1/60초로 고정(`Time.captureDeltaTime`)해 결과가 매번 같다. 입력은 `PlayerMotor.SetMoveInput` 주입(InputTests 만 가짜 패드·키보드). **테스트 실행기가 든 첫 장면(InitTestScene…)을 내리면 실행이 멈춘다**(첫 시도에서 10분 넘게 멈춤 확인) → 테스트는 장면을 Additive 로 열고 자기가 만든 장면(Lab_*, Zone1)만 내린다.

번호·합격 기준 = 07 문서 9-2 표(2차에서 코드 이름을 표 번호에 맞추고, 1차 구현이 말없이 늘린 허용치를 되돌림).

| 테스트 | 내용 |
|---|---|
| `MoveTests` T01~T13 · T19 | 코드로 만든 시험장: 발 높이 ≤0.01 · 걷기 1.60±0.02(95% ≤0.15초) · 달리기 4.50±0.05(≤0.60초, 정지 ≤0.30초·≤0.65m) · 180° 돌기 · 경사 30/35° 꼭대기·45/50° 막힘·**30° 내리막 달리기 접지 98%↑·뜬 프레임 0** · 턱 0.15/0.30 오름·0.45 막힘 · **T07 벽 45° 비스듬히: 벽 따라 ≥ 걷기 0.6배·관통 0** · 낙하→리스폰 · T09 폭 2.4m 골목 카메라 모든 프레임 벽 안·가림 0(+자동 정렬로 끝까지 걷기) · **T10 전봇대가 카메라–인물 사이를 지날 때 거리 변화 ≤0.05m** · T11 턱 오르내리기 카메라 y ≤0.02m/프레임·비주얼 ≤0.03m · T12 구도 (0.44, 0.50)±0.01·FOV 49/45±0.3 · T13 6초 달리기 방향 흐름 없음 · **T19 걷기 자동 정렬**(기다림 1.8초·≤30°/s·각가속도 ≤60°/s²·넘침 없음 / 달리기 1.2초·≤60°/s / 끔·서 있기·카메라 쪽으로 걷기 = 안 돌림) |
| `ZoneTests` T14 Walk / Run · T21 | **1구역 자동 걷기 / 달리기**: Zone1 을 열고 NavMesh 를 임시로 구워(반지름 0.35, 맨땅 비용 4, 저장 안 함) 체크포인트 1→10 의 아홉 구간을 경로 모서리를 따라 `SetMoveInput` 으로 간다(실제 게임 카메라·자동 정렬 켬). 구간마다 **1.3 × 경로 ÷ 속도** 안 도착 · 발이 지면 −1m 아래로 안 떨어짐 · 3초 동안 0.2m 미만 이동이면 '끼임' 실패 · 리스폰 0 · 10프레임마다 카메라 벽 안·가림 **0 (Assert)**. **T21 시작 구도**: 시작 지점에서 당김 ≤0.05m·거리 4.0±0.1·인물 50±4%·발 10±3%, 3초 달린 뒤 당김 없음·등 뒤 ≤15°·인물 38~48% |
| `ZoneTests` Z_GameShots | `-m1shots <폴더>` 를 줄 때만(그래픽 필요, `-nographics` 빼기): 실제 게임 카메라 화면 `m1_game_1_start / 2_alley / 3_reveal / 4_waryong / 5_run / 6_pause / 7_idle_front / 8_walk_side.png`(1920×1080, 2배 슈퍼샘플) + 발 미끄러짐 띠 `9_walk_strip / 10_run_strip.png`(구역 밖 체크무늬 바닥, 옆 고정 카메라 8컷). 화면 UI(길잡이 HUD·조작 안내·일시정지 메뉴)도 찍히게 찍는 동안만 오버레이 캔버스를 카메라 공간으로 바꾸고 이름표·방향 화살표를 촬영 해상도로 다시 계산한다(배치 실행의 화면은 640×480) |
| `InputTests` T16 · T20 | T16: 가짜 패드 왼스틱·R2·오른스틱, 키보드 W·Shift → HInput → PInput·CamInput → 모터·카메라. **T20 일시정지 메뉴**: 패드 Options 열기 → 십자키·왼스틱 고르기 → ×로 자동 정렬 바꾸기·끝내기 → ○로 닫기 / 키보드 Esc·↓↑·Enter·S·Space 로 같은 것 / 메뉴의 ×가 상호작용으로 새지 않음 |
| `MoveTests` T22 · `InputTests` T23 (3차) | **대각선 + 자동 정렬**: `CamRig.StickToWorld` 로 W+D 3초 걷기·달리기, D 만, W+D → W(T22) · 가짜 키보드 W+D 3초(T23) → 자동 정렬이 카메라를 20° 넘게 돌리는 중에도 이동 방향 변화 ≤ 15°. 결과 0.0°(카메라 42°), 비교로 넣은 예전 계산은 73.2° |
| `AnimTests` T24 · T25 (3차) | 실제 `Player.prefab`: **애니메이터 상태** 정지 → Idle · 걷기 1.4 → Walk · 달리기 4.5 → Run · 다시 정지 → Idle · 느린 걷기 0.8 → Walk(Rate 0.57) — 클립 무게 ≥ 0.9 / **발 미끄러짐**: 디딤발(발목 높이 최저 + 1.5cm) 진행 방향 성분 ≤ 속도의 5%, 절대값 ≤ 15%, 발목 최저 0.09~0.17m |
| `RouteTests` T26 (3차) | **방향 화살표·목표 이름표**: 목표가 보이면 화살표 없음 · 등 뒤 = 아래 가장자리 · 오른쪽 = 오른쪽 가장자리 / 목표 앞 다섯 자리에서 목표 이름표가 길잡이 판과 겹치지 않고 화면 안(옮긴 자리 ≥ 1) |
| `RouteTests` T17 / T18 | **길잡이**: 시우를 체크포인트마다 순간 이동 → 순서대로만 넘어감(건너뛴 4번에 가도 그대로), 지금 목표만 기둥·빛 기둥·깃발·이름표, 지난 것은 옅은 원판만, HUD "다음: … · NNm" → "도착: …" → 다음, 완주 이벤트 1번 / **이름표**: 지금 목표 이름표 보임, 먼 출입문 숨김, 체크포인트 다섯 곳에서 화면에 보이는 이름표끼리 겹침 0 |

- 07 문서 T15(상호작용 고르기)는 `Interactor` 가 아직 없어 미구현(07 11장 12).

### M2 전투 — 0~6단계 (2026-10-06, 설계 = `docs/08_M2_전투_설계.md`, 결과 = 08 문서 11-1)
- **코드** `Scripts/Combat/`: `Fighter`(HP·상태·피격·기술 실행 `AttackRun`) · `PlayerCombat`(입력 버퍼·약 4타·△ 마무리·회피/읽었다/반격·막기·전진 버팀·잡기 3갈래·기세) · `HitResolver`(부채꼴·구간 판정, 순수 함수) · `TimeFx`(timeScale 단독 소유 — 히트스톱·슬로·일시정지, 입력 버퍼 시계) · `ImpactFx`·`TraumaShake`(흔들림·줌 펀치 = 시안 식) · `HitReact`(젖힘·번쩍·셰이크, 클립 전 임시 공격 자세) · `LockOn` · `CombatCamRig`(CM_Combat) · `CombatMode`(탐색↔전투: 맵 전환·카메라 우선순위·HUD 숨김) · `MoveDef`/`MoveSet`/`MoveLib` · `CombatTuning` · `FighterBody`(적·허수아비 몸) · `HeatSurface`·`CrowdRing` · `CombatFactory`·`CombatLab`(장면 생성·테스트 공용). `Dev/InputScript`(프레임 입력 재생) · `Dev/CombatDebug`(F2 적 다시 · F3 기세 MAX · F4 시우 무적 · F7 판정 보기).
- **에셋**: `Settings/CombatTuning.asset` · `Settings/SiwooMoves.asset`(기술 14개 하위 에셋) · `Settings/CombatBlends.asset`(CM_Explore↔CM_Combat 0.6/0.8초) — 셋 다 없을 때만 만든다(손으로 고친 값 보존. 기본값을 바꿨으면 지우고 다시). 입력 `HInput` 에 `Combat` 맵. 레이어 12 `Fighter` · 13 `Crowd`.
- **전투 연습장** `Scenes/CombatLab.unity`: 평지 20×20 + 북쪽 벽(Wall + HeatSurface) + 구경꾼 원 반경 6(12명, Crowd) + 허수아비(태오 모델 회색 `Materials/Lab/M_Dummy_Toon`, HP 999) + 실제 Player 프리팹 + CM_Explore·CM_Combat, 장면이 뜨면 바로 전투. 빌드 목록에는 아직 안 넣음.
- **Player 프리팹**에 Fighter·HitReact(Visual)·LockOn·PlayerCombat 가 붙는다(탐색 중엔 꺼진 채 — `CombatMode.Begin` 이 켬). Zone1 에는 아직 CM_Combat·CombatMode 가 없다(13단계 인카운터).
- **테스트**: `CombatTests` C01~C10 · C09b · C17① · C20, `CombatInputTests` C21, `ZCombatShots`(`-c2shots <폴더>` 를 줄 때만, 그래픽 필요 — 시안과 같은 화각의 맞은 순간·연속 프레임·전환·읽었다·크러시·벽꽝). 이름이 Z 로 시작하는 이유: 장면의 PInput 이 실제 HInput 을 켜면 그 뒤 InputTestFixture 의 가짜 입력이 막혀서 InputTests 뒤에 돌게 함. 2026-10-06: **그래픽 켜고 42/42**(M1 27 + C 14 + 녹화 1, 로그 `c_18_all_gfx`).

### M2 전투 — 7·8·9단계 (2026-10-06, 결과 = 08 문서 11-2)
- **적**: `EnemyDef`(유형 표, 에셋 `Settings/Enemy_<유형>.asset` — 없을 때만 만듦) · `EnemyBrain`(접근·간보기·도발·공격·막기·피격·다운·탈락·도주) · `AttackDirector`(공격권 — 동시 1명, 어려움 2) · `TelegraphMark`('!'·'!!'). 깐족이(돌진·도주) · 석 달(막기·카운터 훅) · 냉장고(슈퍼아머 경직 게이지 30·막기 불가 껴안기).
- **클립**: `Editor/Game/ClipSetup`(`CombatSetup.Build` 가 부름) — 적 모델 4명 `Art/Characters/Enemy/*.fbx` Humanoid·툰 재질, 전투 클립 26개 `Art/Clips/*.fbx`(뼈만 있는 대리 메시) Humanoid(수평 이동은 버림), 시우 아바타로 타격 시각·뻗은 거리 측정 → `Anim/ClipTable.asset`·`CombatClips.json`(곡선은 `Logs/ClipProbe/*.csv`), 클립 나누기(잡기/밀기·쓰러짐/누움·일어서기·막는 프레임 등), `Anim/Siwoo.controller` 전투 상태 + 상체 막기 층, `Anim/Enemy.controller`(+ 적마다 대기만 바꾼 `Enemy_<이름>.overrideController`). 런타임 `FighterAnim`(코드가 상태 전이, 배율 ≤ 1.6 + 앞부분 건너뛰기로 판정 첫 프레임 = 클립 타격 시각) · `HandShape`(주먹·잡기 손 셰이프 키, 0.08초).
- **프리팹**: `Prefabs/Enemy_{Kkanjok,Seokdal,Naengjanggo,Scrum}.prefab`(Animator + FighterAnim + HandShape — 쓰는 쪽이 `CombatFactory.Enemy(def, …, model)` 로 몸·AI 를 붙임). Player 프리팹 모델에도 FighterAnim·HandShape.
- **연습장**: 허수아비 = 석 달 모델. **F5 = 깐족이·석 달·냉장고 1:3 소환**(AI + 공격권), F6 = 지움.
- **손 셰이프 키**: GLB → FBX 할 때 `blender -b --factory-startup --python tools/glb2fbx.py -- <glb> <fbx> <클립> 1 1`(넷째 1 = 메시 포함, 다섯째 1 = `tools/hand_keys.py` 로 `Fist_L/R`·`Grip_L/R` 추가). 동작 클립은 넷째를 `p`(대리 메시).
- **테스트**: `EnemyTests` C11(깐족이 3명 · 3유형 · 어려움 2명) · C12 · C13, `ClipTests` C14(손) · C14b(시우 클립 상태·타격 시각) · C14c(적 프리팹), `ZClipShots`(`-c9shots <폴더>` 를 줄 때만 — 기술 띠·손 확대·줄 세움·1:3 실전). 2026-10-06: **그래픽 켜고 51/51**(M1 27 + C 22 + 녹화 2, 로그 `e_15_gfx`).

### M2 전투 — 10·11·12단계 + 결정 3건 (2026-10-06, 결과 = 08 문서 11-3)
- **닿는 거리 자석**: 클립이 있는 몸은 발생 동안 '그 클립이 실제로 닿는 거리'(측정 뻗음 − 0.04m)까지 붙는다(1.2m 넘게 미끄러져야 하면 예전 자석). 판정 사거리는 그대로. `CombatTuning.ContactMax·ContactSink`, `Fighter.ReachOf`(FighterAnim 이 넣음).
- **2차 클립**: `Art/Clips/{FallDown,Smash,SideL,SideR}.fbx`(366 쓰러짐 · 128 내려찍기 · 525/526 옆걸음). 하체 밀기 = 260 `PushFwd`. 컨트롤러에 `FallBig/LieBig`(187), 위층 `SideUpper`(옆걸음 때 상체 전투 자세).
- **기세 액션**: `HeatAction`(조건·코드 타임라인, Player 에 붙음) · `HeatCam`(CM_Heat — `CombatFactory.BuildCombatCam` 이 같이 만듦).
- **이펙트·효과음**: `CombatFx` · `FxKit`(`Settings/FxKit.asset`) · 셰이더 `Shaders/FxSprite.shader`·`ShockCut.shader` · 쇼크 컷 = PC·Mobile 렌더러의 Full Screen Pass(전역 `_HaenginShock`). 그림 `Art/Fx/*.png` = `python tools/ink_fx_gen.py <Art/Fx> <Fonts/BlackHanSans-Regular.ttf>`, 소리 `Audio/SFX/*.wav` = `python tools/sfx_gen.py <Audio/SFX>`. 묶기는 `FxSetup.Ensure`(CombatSetup.Build 가 부름). 배치 모드에선 소리를 끈다.
- **HUD**: `CombatHud`(Player 밑 `CombatUi`, 전투 중에만). 조작 안내 줄은 `GameUi.HintOverride` 로 전투 조작.
- **덩치 손**: `tools/hand_keys.py` 의 `PROFILES`(냉장고·스크럼) — `glb2fbx.py` 가 원본 이름으로 고른다.
- **테스트**: `ClipTests` C05b(닿는 거리 자석), `HeatTests` C15, `HudTests` C10b(이펙트·효과음)·C16(HUD), `ZHeatShots`(`-c10shots <폴더>` — 기세 액션 녹화·이펙트·HUD 사진). 실제 Player 프리팹을 쓰는 테스트는 `PInput` 을 지운다(CombatMode 가 맵을 바꾸면 실제 HInput 이 켜져 InputTests 가 막힘). 2026-10-06: **그래픽 켜고 56/56**(M1 27 + C 26 + 녹화 3, 로그 `f_14_gfx`).

### M2 전투 — 13·14·15·16단계 (2026-10-06, 결과 = 08 문서 11-4)
- **무대** `Scripts/Stage/`: `EncounterDef`·`Encounter`(인카운터 Y4 — 대기 → 시작 트리거 → 시비 0.8초 → 전투 → 「정리.」 → 결과 카드 → 탐색, 패배 화면 다시/그만, 이긴 뒤 일시정지 메뉴 '전투 다시') · `YachaDef`·`Yacha`(야차 Y1 — 심판 형 대화 → 입장 6초 → 싸움 → 항복·심판 스톱·메뉴 항복 → 결과) · `StageHud`(자막·배너·큰 붓 글자·이름 카드·결과 카드·패배 화면·대화 판·상호작용 안내·야차 큰 바) · `CrowdFigure`(구경꾼 — 걷기·떠밂·흩어짐) · `Interactable`(× / E). **ScriptableObject 는 클래스 이름과 같은 파일에 둔다**(`EncounterDef.cs`·`YachaDef.cs` — `Encounter.cs` 안에 두었더니 에셋이 스크립트를 못 찾아 Zone1 결정적 저장 검사에서 '깨진 참조 2'로 멈춤).
- **에디터** `Editor/Game/StageSetup`: 구경꾼 프리팹 `Prefabs/Crowd_<적 이름>.prefab`(적 모델 + 회색 툰 재질 3가지 `Materials/Lab/M_Crowd_1~3`) · 심판 형 `Prefabs/Referee.prefab`(태오 모델 짙은 회색) · `Settings/Encounter_Y4.asset`·`Yacha_Y1.asset`(없을 때만 — 손으로 고친 값 보존, 프리팹 연결은 매번) · Zone1 에 CM_Combat·CM_Heat·CombatMode·`Stages/Encounter_Y4`·`Stages/Yacha_Y1`·주차장 벽 `HeatSurface`(`M1Setup.AttachRig` 끝에서 부름 — `Zone1Builder.Build` 만 돌려도 붙음) · 연습장 구경꾼 캡슐 → 구경꾼 프리팹. 순서 때문에 Zone1 까지 다시 만들 때는 `CombatSetup.BuildAll`(적·구경꾼 프리팹 → Zone1 → 연습장).
- **적 스크럼**: `EnemyLib.Scrum`(HP 260, 슈퍼아머 40 → 페이즈 2 50, 밀기 260 · 휘두르기 128 · 태클 512 · 페이즈 2 밀기 → 휘두르기), `EnemyBrain` 태클·헛방 비틀·물러나기·페이즈 2.
- **덩치 팔**: 냉장고·스크럼 적 프리팹의 `FighterAnim.ArmSpread = 12`(재생 뒤 위팔을 몸 바깥으로 벌림 — 시우 리그 클립의 팔이 굵은 몸통을 파고들지 않게). 근육 범위 줄이기는 효과가 없거나 거꾸로라(실측) 기본값으로 둔다. 측정: `-executeMethod Haengin.EditorGame.ArmProbe.Run`(후보별 '몸 안·닿음' 비율을 로그와 `Logs/arm_probe.csv` 에, `-armshots <폴더>` 를 주고 `-nographics` 를 빼면 자세 사진).
- **접근성**: 일시정지 메뉴 '흔들림 줄이기'(`Accessibility.Reduced`, PlayerPrefs `haengin.fx.reduced`) — 흔들림 ×0.3, 화면 번쩍·쇼크 컷·슬로 끔. 메뉴 항목은 상황에 따라: 계속 · 자동 정렬 · 끝내기 · 흔들림 줄이기 · (이긴 뒤 근처) 전투 다시 · (야차 중) 항복.
- **전투 카메라 옆 고르기**(6-5 보강): 락온 중 막히면 옆 각 18°·40°·65°·90° 중 열린 가장 작은 각, 지금 옆 우선, 반대 옆으로 넘어갈 길이 막혔으면 컷.
- **연타 난이도(08 12장 11, 2026-10-06 결정)**: `MoveDef.Committed`(몸을 던지는 공격 — 냉장고 전부·깐족이 달려들기·스크럼 태클: 시작~판정 끝 시우 □ 4타에 안 끊김, 피해는 받음) · `AttackDirector` 끊기면 옆·뒤 공격권(`CombatTuning` 의 FlankGap 0.3 · FlankSide 60 · FlankRadius 2.4 · FlankSpeed 2.2 · FlankTime 1.8 · FlankDelay 0.4 · FlankWarnLead 0.5) · `AttackRun.WarnOverride/WarnLead/NoFollowup`(옆·뒤 공격 '!'·한 방). `FightBot` Mash=false 는 '회피·막기' 봇(막을 수 없는 쪽은 판정 0.12초 전 회피, 0.6초 전부터 손 멈춤). 테스트 `EnemyTests` C12b, `StageTests` C22(연타 봇 + 회피·막기 봇 비교).
- **봇·스모크**: `Dev/FightBot`(연타 봇 / 녹화 봇 — `PlayerCombat` 에 사람 손과 같은 길로 입력) · `Dev/M2Smoke`(빌드에서 장면이 뜨고 3.5초 뒤 `[M2] 실행 확인` 한 줄, 실행 인자 `-m2smoke` 면 인카운터·야차를 봇으로 한 판씩 돌고 `[M2] 스모크` 줄을 남긴 뒤 스스로 종료).
- **테스트**: `StageTests` C18(인카운터 흐름) · C22(연타 봇) · C17②(벽 등지고 락온) · C19(야차 승패), `ZStageShots`(`-c13shots <폴더>` — 적 4명 정면·옆, 덩치 공격 자세, 인카운터·야차 한 판 녹화, 패배 화면, 일시정지 메뉴). M1 의 Zone1 테스트(ZoneTests·RouteTests)는 `Encounter.Suppress = true`(경로 걷기가 주차장을 지나감). 결과 수 = 08 문서 11-4.

### M3 이야기 — 0~5단계 (2026-10-06, 설계 = `docs/09_M3_버티컬슬라이스_설계.md`, 결과 = 09 문서 6-5-1)
- **코드**: `Scripts/Story/`(StoryRunner·SceneDef·EpisodeDef·StoryDef·StoryFlags·SceneLoader·InkWipe·GameClock·StoryHud·StagePlace·StoryBoot) · `Scripts/Talk/`(DialogueRunner·SubtitleUi·InnerVoiceUi·ChoiceUi·TalkInput·DlgLine·DlgBook) · `Scripts/Cut/`(Cutscene·CutCtx·CutCams·PropFollow·InkMode·CutLib + 타임라인 트랙/클립 8쌍) · `Scripts/Light/`(LightingPreset·DayLight·LightSpot) · `Scripts/Book/`(Ledger·LedgerUi·NameBook·NameBookUi·EpisodeCard·EndureFx) · `Scripts/Save/`(SaveData·SaveStore·TitleMenu) · `Scripts/UI/`(UiKit·UiFonts). 셰이더 `Shaders/InkMode.shader`(A′ 흑백·해칭, 렌더러 Full Screen Pass 'InkMode' — 전역 `_HaenginInk`).
- **데이터**: `Data/Story/m3_story.json`(장면 39 = 장면 32 + 회차 카드 7, 시험 장면 3, 회차·도감 칸·가계부 공개 표·하늘 메모) · `Data/Story/dlg/ep*.tsv`(확정 대사 — 열 id·화자·표정·표시·대사·대기·카메라·메모, `\n` 두 글자 = 줄바꿈, 한 줄 32자) · `dlg/draft/`(원고 초안, `tools/script2dlg.py --all`) · `Data/Story/cuts/Cut_*.playable`(시험 컷신, 없을 때만 만듦 — `-recut` 이면 다시).
- **에셋**(StorySetup): `Settings/Story.asset` · `Dialogue.asset` · `CutLib.asset` · `UiFonts.asset`(본문 KR_Bold_SDF · 붓 KR_Brush_SDF = Black Han Sans · 손글씨 KR_Hand_SDF = **Gaegu OFL** — 붓·손글씨는 본문을 대체 글꼴로) · `Light_{Dawn,Day,Dusk,Night}.asset`(없을 때만 — 낮 = 그때 Zone1 값) · 카메라 프리셋 프리팹 `Prefabs/Cam/CP_*.prefab`.
- **장면**: `Scenes/Title.unity`(빌드 첫 장면) · 자리 무대 `Scenes/St_{Gukbap,Home,Conv,School,Site,Flash}.unity`(9단계에 키트로 바꿈, Zone1 과 안 겹치게 x·z 2000 근처) · Zone1 에 루트 `Story`(StoryRunner·SceneLoader·TalkInput·DialogueRunner·Cutscene·DayLight + 화면 9개) · `Zone1/Lights`(점광 13 + 창문 판 260, 시드 고정) — `M1Setup.AttachRig` 끝에서 `StorySetup.AddStoryToZone1` 이 붙임(Zone1Builder 만 돌려도 붙음, 결정적 저장 그대로 — 두 번 돌려 Zone1.unity 같음 확인).
- **흐름**: 타이틀 → `StoryBoot.Request` → Zone1 의 StoryRunner 가 시작(Zone1 을 그냥 열면 아무것도 안 함 — M1·M2 테스트·연습장 그대로). 이야기 중 M2 무대(`Stages`)·M1 체크포인트 숨김. 무대 = Additive(Zone1 루트만 끔), 전환 = 먹 닦기 0.5초. 장면 시작마다 자동 저장(`persistentDataPath/save_auto.json`, 이전 것은 `.prev.json`). 일시정지 메뉴에 '도감'·'저장하고 타이틀로'(이야기 중만, 메뉴 캔버스 정렬 70 = 맨 위). 입력 `Talk` 맵(`tools/input_maps.py` 가 HInput 에 넣음 — 다음 × · Enter · Space · E · 마우스 왼쪽, 길게 0.8초 또는 Select/Tab = 건너뛰기).
- **빌드 목록**: Title → Zone1 → St_* 6 → Sandbox → CombatLab(타이틀 '연습장'). 빌드를 `-m2smoke`·`-practice` 로 띄우면 타이틀이 바로 연습장 Zone1 로(M2 스모크 그대로).
- **테스트**: `M3StoryTests` D01·D02 · `M3ATalkInputTests`·`M3TalkTests` D03·D04 · `M3CutTests` D05·D06 · `M3LightTests` D07(그래픽이 있으면 걷는 면 L* 도) · `M3BookTests` D09·D10 · `M3SaveTests` D11·D12 · `ZStoryShots`(그래픽이면 늘 D08 성능 첫 측정 → `Logs/perf_first.csv`, `-m3shots <폴더>` 를 주면 0~5단계 녹화·사진 + `notes_s*.txt`). 공용 `M3Lab`(Zone1 열기·정리, 저장 폴더 = 임시 한글 폴더) · `Shots`(촬영·L*). 2026-10-06: **그래픽 켜고 80개 중 75 통과 · 실패 0 · 건너뜀 5**(M1·M2 녹화 — 각자 인자 필요), 347초.

2026-10-06 4차-2(대기 Idle_6·걷기 Quick_Walk 로 교체, 걷기 1.4 m/s): **27/27 통과**(게임 화면 11장, 로그 `a_26_tests`). T14 걷기 9/9 **140.0초**(경로 221.3m, 시간 한도는 1.3 × 경로 ÷ 1.4 로 같이 늘어남) · 달리기 9/9 46.3초 · 리스폰 0.

2026-10-06 4차(리깅 모델·애니메이터·대각선 이동·HUD): **27/27 통과**(그래픽 켜고 게임 화면 10장 포함, 로그 `a_17_tests`, 실제 실행 50초). T14 걷기 9/9 122.5초 · 달리기 9/9 46.3초 · 리스폰 0(모델을 바꿔도 이동은 같음).

2026-10-05 3차(카메라·조작 다듬기): **22/22 통과**(그래픽 켜고 게임 화면 6장 포함, 로그 `p_27_tests`). 걷기 9/9 게임 시간 **122.5초**(경로 221.2m), 달리기 9/9 **46.3초**(220.2m), 리스폰 0, 카메라 벽 안·가림 0(걷기 703·달리기 247 검사) — 자동 정렬로 카메라가 등 뒤를 따라와 걷기 최소 카메라 거리 1.22 → **3.17m**. 시작 구도 거리 4.00m·당김 0·인물 50%·발 10%, 3초 달리기 4.58m·당김 0·인물 40%. 실제 실행 약 1분(Unity 띄우기·컴파일 포함).

2차(골목 벽·길잡이 반영 뒤): 17/17 통과. 1구역 걷기 9/9 구간 게임 시간 122.6초(경로 221.0m), 달리기 9/9 46.1초(220.5m), 리스폰 0, 카메라 벽 안·가림 0(이때는 세기만 하고 Assert 안 함, 시간 한도 1.5×+4초).

1차 결과(2026-10-05): 15/15 통과(게임 화면 촬영 포함). 1구역 걷기 9/9 구간 게임 시간 122.5초(경로 221.1m), 달리기 9/9 46.0초(220.5m).

### M3 6·7·8단계 + 1화 채우기 (2026-10-25, 결과 = 09 문서 6-5-4)
- **정식 모델(6·7단계)**: `tools/formal_model.py` → `tools/face_tex.py`(윗눈꺼풀 `LID_MM` 7.6 — 카메라 B FOV 53 기준, `<이름>FaceMask.png` 도 뽑음) → `Art/Characters/{Siwoo,Taeo,SiwooDelivery}/` · `CharSetup`(손가락 30 매핑·`isReadable`·몸 UTS Shade/OutW·얼굴 UTS 알파 클리핑 + 외곽선·그림자 끔·`Paint`) · `FormalSetup.Build`(HandPoseSet 관통 맞춤 = 바인드 자세·대기 자세 둘 다 3.6mm 목표, FaceRig·HandPoser·FormalModel·소켓 5·LODGroup 0.25/0.08/0.01, `Prefabs/Char/Taeo.prefab`) · 플레이어 = 교복 + `SiwooDelivery_Model`(꺼 둠) + `Outfit`(장면 데이터 `outfit`, 새벽 배달 = 배달복). `HandCheck` = 손 꼭짓점 직접 스키닝 + 손가락 캡슐(붙은 손가락·물갈퀴·꺾인 사슬 제외).
- **걷기(대표 "여성스러움")**: 남자 걷기 = 동작 566(`tools/pipeline/walk_clips.ps1` 후보 4개 32cr, `walk_metrics.py` 엉덩이·팔 흔들림 비교, `docs/anim_library.md` '걷기 교체') — 시우·태오·남자 NPC 공통, 루트 XZ 빼고(`Human(..., rootXZ)`) ×1.401 = 1.4m/s, 여자는 그대로.
- **스토리보드(대표 "막 지나감")**: 자리 장면은 `StoryBoardUi` 카드(회차·날짜·시각·곳·제목·흐름 3~5줄·핵심 대사 0~3·'제작 중 장면')가 **누를 때까지** 그대로, 새 게임 = 1막 소개 카드, 조작 장면 앞 소개 카드. 데이터 = `tools/storyboard.py` + `storyboard_curated.py` → `m3_story.json`(board·quotes·outfit·intro).
- **배달 자전거(8단계)**: `Scripts/Bike/`(BikeMotor·SoupGauge·BikeInput·BikeCam·BikeVisual·BikeRider·PathRunner·BikeCourse·BikeHud) · `BikeSetup.Build`(Zone1 에 코스·CM_Bike·자전거·태오 달리기·꼭대기 집 꾸밈 — `M1Setup`·`StorySetup` 이 Zone1 을 다시 만들 때도 붙임) · 모델 `tools/bike_split.py`(바퀴 원 맞춤·바큇살만 돎·크랭크·앵커 json) → `Art/Bike/DeliveryBike.fbx/.json` · 입력 맵 `Bike`(`tools/input_maps.py`) · 맵 `tools/zone1_gen.py` '와룡 오르막 자전거길(게임 전용)'·'꼭대기 집'·'운동기구 마당 축대'. 1-2 → 1-3 → 1-4 는 한 번 탄 채 이어 감(내리기 없음).
- **1화 「출석부에 없는 이름」**: `CastSetup.Build`(배역 8 = 엄마·하늘·도경민·민재·건우·남·여학생·곽반장, 이미 뽑아 둔 `concept/art/3d/m3/anim/*_idle243.glb` → 0크레딧, `Anim/Cast.controller` 앉기·말하기·박수 + 기본 층 IK) → `StorySetup.Build` 가 무대를 다시 만들며 `CastSetup.Dress`(국밥집 엄마 · 시우네 하늘·의자·아빠 사진 `Art/Story/DadPhoto.png`(0.5cr) · 교실 칠판·교탁·책상 12·배역·뒷문 스폰 `BackDoor`). 대사 `Data/Story/dlg/ep01.tsv` 1-5·1-6(원고 문장 그대로) — 카메라 칸 `프리셋[:배우]`(근접·낮은 정면·와이드·로우앵글·등 뒤 트래킹·측면 트래킹·**미디엄**), 이벤트 `braidgame:N`(양갈래 땋기 `BraidGame`, 결과 양파·다시·합격 고정) · `cut:<장면>`(대사 사이 컷신) · `hand:배우:자세` · `pose:배우:상태` · `look:배우|반:대상|해제` · `face:배우|반:대상|해제` · `raise:배우:대상|해제`(`CastLook` 고개·돌아앉기·팔 IK). 대사 카메라 사이는 컷(블렌드 없음).
- **테스트**: `M3CharTests` D13~D15b · `M3BikeTests` D16a~c·D17 · `M3BoardTests` B01~B03 · 녹화 `ZCharShots`(`-s6shots`) · `ZBikeShots`(`-s8shots`) · `ZBoardShots`(`-boardshots`) · `ZEp1Shots`(`-ep1shots`).
- **함정**: ① 앉은 배우는 스킨 경계가 선 자세 그대로 → 카메라는 머리 뼈 기준(`CutCamDriver.Measure`) ② 무대가 켜질 때 애니메이터가 기본 상태로 되돌려 `Play` 가 씹힘 → `CastPose` 가 LateUpdate 에서 다시 ③ 대사 카메라 블렌드 중간 자리가 배우 머리를 지남 → 대사 동안 브레인 기본 블렌드 = 컷 ④ 벽 검사를 허리 높이에서 쏘면 교탁·책상에 걸려 카메라가 코앞으로 → 머리에서 ⑤ 사진 시험에서 Player 프리팹은 `PlayerMotor`·`CharacterController` 를 꺼야(연습장 바닥 밑으로 떨어짐) ⑥ 애니메이터를 끄고 뼈만 바꾸면 화면 밖 스킨이 옛 모양 → `updateWhenOffscreen`.

### 무대 4곳 실제 공간(09 9단계, 2026-10-25 — 결과 = 09 문서 6-5-5)
- **원본**: `tools/env/stages/` — `specs/frames.json`(목표 그림 8) · `specs/props.json`+`props_meta.json`+`props_final.json`(Hunyuan 소품 4) · `specs/textures.json`(GPT 결 3) · `gen_images.ps1`·`gen_3d.ps1`(체육관 것과 같고 경로만 stages) · `process_props.py` · `make_textures.py`(절차 결 + GPT 결 다듬기) · `build_stages.py`(키트 4 + manifest) · `compose_compare.py`(비교 시트). 결과 `concept/art/env/stages/`.
- **순서**: `python tools/env/stages/make_textures.py` → `blender -b --factory-startup -P tools/env/stages/build_stages.py` → `python tools/env/stages/process_props.py` → `blender -b --factory-startup -P tools/env/prop_lods.py -- --src concept/art/env/stages/props --out unity/HaenginMainEvent/Assets/_Project/Art/Env/Stages/Props` → Unity `StageKitSetup.Build`(**처음은 `-nographics` 빼기** — 소품 텍스처 추출) → `StorySetup.Build`.
- **Unity**: `Editor/Game/StageKitSetup.cs`(복사 · 재질 `Materials/Env/Stages/M_*` · 소품 `Prefabs/Stage/P_*` · `Place`(키트·충돌·소품·불·문·표시 자리·카메라 소켓) · `AddDoorsToZone1`(M1Setup.AttachRig 이 부름)) · `Scripts/Stage/StageDoor.cs`(× · E 로 들어가기/나가기, Interactable) · `PlaceMarker.cs`(알바·잠·저장·가계부·자리 표시 — 기능은 새 설계 뒤) · `StageLights.cs`(실내 주변광·볼륨·안개 끔).
- **시험**: `M3StageTests` K01 · 사진 `ZStageShots`(`-stageshots <폴더>`) · 빌드 스모크 `-stagesmoke`.
- **바른 앉기**: `tools/pipeline/sit_clips.ps1`(후보 리깅) · `sit_metrics.py`(숙임 비교) → `Art/Clips/M3/{SitUp,SitTalk,SitFloor}.fbx`(glb2fbx 대리 메시), `CastSetup` 표에 처음·끝 프레임(364·363 은 끝 자세만 반복).
- **함정**: ① 키트 무대는 해·안개를 `StageLights` 가 다시 덮는다(DayLight 가 무대를 연 뒤 다시 칠함 — LateUpdate) ② 키트 FBX 안 빈 오브젝트(Sock_/Prop_)는 `Place` 가 지우고 다시 만든다 ③ 실제 방은 카메라 벽 당김이 정상(D02 키트 무대 예외) — 스폰은 등 뒤 2.8m 가 비는 자리로 ④ `formal_model.py` 로 손가락만 다시 뽑을 땐 `FORMAL_KEEP_FACE=<예전 FBX>`(얼굴 데칼을 다시 만들면 표정 칸이 어긋남).

### 체육관 — 혜화 MMA(2026-10-24, 설계 = `docs/12_체육관_설계.md`, 결과 = 12 문서 12장)
- **입력** `Art/Env/Gym/`: 키트 `Gym_Hyehwa.fbx` + manifest(소켓 115) + `Textures/M_*.png` 13 + `poster_atlas.json` + `Props/*.glb` 25(LOD 3단, `blender -b --factory-startup -P tools/env/prop_lods.py -- --src concept/art/env/gym/props --out Assets/_Project/Art/Env/Gym/Props`) + `T_DrillRing.png`(절차). 캐릭터 `Art/Characters/M3/{Yunjun,Gojun}.fbx`(`tools/glb2fbx.py` 로 `concept/art/3d/m3/*_idle243.glb` 에서, 텍스처는 GLB 안 JPEG 를 그대로 뽑음) · 앉기 클립 `Art/Clips/M3/ChairSit.fbx`(대리 메시).
- **에셋**(`GymSetup.Assets`): 재질 `Materials/Env/Gym/M_*.mat` 28(UTS; 유리·철망 URP Lit, 거울 Unlit, 포스터 칸 8) · 소품 프리팹 `Prefabs/Gym/P_*.prefab` 25 · NPC 프리팹 `Npc_Yunjun/Gojun`, `Npc_Member_01~08`(적 모델 + 옷 색 `M_Member_##`), 고준 전투 몸 `Gojun_Fighter` · 애니메이터 `Anim/Gym.controller` + Override 5 + `Enemy_Gojun.overrideController` · 적 정의 `Settings/Enemy_Gojun.asset` · 드릴 `Settings/DrillBook.asset`(`Data/Drill/drills.json` 이 원본, 손으로 고치지 말 것).
- **장면** `Scenes/Gym.unity`(`GymSetup.Scene`): 루트 `Stage_Gym`(StagePlace `Gym` + GymManager + DrillRunner) 아래 `Kit`(FBX 풀어서) · `Colliders`(평면도 상자 39) · `Sockets`(114) · `Props`(43, 샌드백은 `_Pivot`(BagSwing) › 홀더 › `_Body`(Fighter)) · `Lights`(GymLights) · `거울 반사판`(MirrorPlane) · `Markers`(DrillMarker 19) · `NPC`(10) · `CM_Drill` · 스폰 `Spawn`(계단 위)·`Floor`·`Mat`·`Cage`.
- **런타임** `Scripts/Gym/`: DrillTypes·DrillBook·DrillGrader(산식·보상·시뮬)·DrillMetrics·ComboJudge·BeatTrack·DrillRunner·Stations{Rhythm,Strike,Grapple}·SparStation·DrillHud·DrillResultCard·DrillMenu·DrillMarker·GymManager·GymLights·GymNpc·BagSwing·MirrorPlane, 빌드 스모크 `Scripts/Dev/GymSmoke.cs`(`-gymsmoke`). 타이틀 '연습장 › 체육관' → `StoryBoot.Mode.Gym`.
- **2차 패스(2026-10-24)**: 실내 전용 볼륨(채도 −38·스플릿 톤 중립·노출 +)·해 0·안개 끔(`GymLights`), 바닥 스티커 라벨, 장식 30(배관·테이프·사무실 집기·포스터), 전용 동작 3종(`Art/Clips/Gym/{JumpRope,Kettlebell,Burpee}.fbx` ← `concept/art/3d/anim/gym/`, `tools/pipeline/gym_clips.ps1` 24cr) → `Settings/GymClips.asset`, 드릴 카메라 ±15°·사무실 2.2·미트 프리셋. 시간대 사진 `-gymshots` 의 `tod/`, 묶기 `scratchpad/gym_compose2.py`. 결과 = docs/12 12-4.
- **함정**: ① MonoBehaviour·ScriptableObject 는 **파일 이름 = 클래스 이름**이어야 장면·에셋 참조가 산다(BagSwing·DrillBook 을 같은 파일에 뒀다가 참조가 `fileID: 0` 으로 깨짐) ② 체육관 시험 클래스는 `M4…`(입력 테스트 뒤) ③ `M3Lab.UnloadAll` 이 `Gym` 무대도 내린다 ④ 봇이 돌 때는 DrillRunner 가 실제 입력을 읽지 않는다(주입 스틱·가드가 덮이지 않게).

### 1장 오픈월드 데모(2026-10-25, 설계 = `docs/13_오픈월드_커리어_설계.md`, 결과 = 13 문서 14장)
- **흐름**: 타이틀 '새 게임' → `StoryBoot.Mode.World` → Zone1 의 `WorldRunner.Begin`(장 카드 → 하루 루프 안내 3장 → C1-01 오프닝 체인) → 자유 이동. 예전 '스토리보드 카드 자동 진행'은 타이틀 › 연습장 › '이야기 장면 보기'(StoryRunner 장면 목록 — M3 시험 그대로). '이어하기' = v2 저장이면 오픈월드, v1(M3 장면 저장)은 그 장면이 든 카드부터로 이주.
- **데이터**(손으로 고치는 원본): `Data/World/chapters.json`(장 7 + 1장 데모 목표) · `activities.json`(알바·학교·잠·저장·가계부·하늘이·청소 관원 — 결정 12 시각) · `balance.json`(시계·공휴일·청소년 근로·피로·돈·평판·소문·날씨·지점) · `events/ch1.json`(1장 49 + N01~N10) · `events/ch2_6.json`(2~6장·막간 자리 60). → `WorldSetup.Build`/`DataOnly` → `Settings/World.asset`(`WorldJson.Fill` 결정적 변환, 시험 W01 이 두 번 돌려 비교). 금칙어 목록 = 레포 `tools/banned_names.txt`(W00).
- **코드** `Scripts/World/`: `WorldDef`(데이터) · `WorldSave`(저장 v2 칸 — `SaveData.World`) · `WorldModel`(순수 규칙: 시계·돈·말일 이체·피로·스탯 소수·평판·소문·알바 정산·7시간·잠·목표·v1 이주) · `EventDirector`(카드 고르기: 트리거·우선순위·하루 상한·결정적 확률·놓침) · `WeekPlanner`(주간 계획 맡기기) · `WorldRunner`(+`.Acts`·`.Ui` — 장면 위 진행: 평가·카드·활동·잠·체육관·시비·계획·데모 끝·HUD·휴대폰·저장·문) · `WorldHud` · `PhoneUi` · `PlannerUi` · `ChapterUi` · `JobGame`(알바 미니게임 5) · `WorldMarker`(표시 자리 = 활동) · `StageLife`(실내 시간대 무리·창빛·스탠드·김) · `RumorNpc`. 봇 `Scripts/Dev/WorldBot.cs`, 빌드 스모크 `Scripts/Dev/Ch1Smoke.cs`(`-ch1smoke`). 고친 기존 코드: `GameClock`(해·`Advance` 자정 넘김·날짜 열쇠) · `StoryRunner`(`BeginWorld`·`RunChain` — 장면 체인을 카드 본편으로, 시각은 앞으로만) · `SaveData` v2 · `SaveStore`(v1 읽기 허용) · `TitleMenu` · `DayLight.ApplyClock`(연속 보간) · `GymManager.Gate/Finished` · `StageDoor.Gate` · `StoryBoardUi.ShowCard` · `GameUi.BookLabel`.
- **장면**: Zone1 루트 `World`(WorldRunner + 화면 5 — `WorldSetup.AddToZone1`, `M1Setup.AttachRig` 끝). 무대 4곳 2차 = `WorldSetup.DressLife`(StorySetup.Stages 가 무대를 다시 만들 때): 국밥집 손님 무리 4(새벽 2·점심 3·저녁 6·밤 2, 배역 모델 재사용 + 옷 색) · 가마솥·육수통 김 3 · 새벽 창빛 / 시우네 이불(밤 펼침·낮 갬)·하늘 밤엔 잠·책상·스탠드(19:00~01:00)·창빛 / 편의점 알바생(18:00~01:00)·손님 낮·밤. 실행 중에만 붙는 것: 마을버스 정류장 문(→ 체육관, 15분)·명륜1가 방면 표지 문(→ 현장)·체육관/현장 나가는 문·표시 자리 `job_delivery`(국밥집 주방)·`job_site`(현장)·`gym_clean`(체육관 사무실 앞).
- **조작**: 표시 원·문 앞 × / E(직접 · 맡기기(10번 또는 S 뒤) · 그만) · 휴대폰 Tab / Select · 일시정지 메뉴 '휴대폰(Tab)'·'저장하고 타이틀로'. 미니게임 = Menu 맵(방향 · × / Enter / Space).
- **시험**: `M5WorldTests` W00~W04·W06·W07·W09~W13(순수) · `M5WorldPlayTests` W05·W08·W12b·W14·W15(Zone1, 봇) · `ZWorldShots` Z_W16_Perf(그래픽이면 늘) · Z_World_Shots(`-worldshots <폴더>` — 녹화 JPG 프레임 + 사진).
- **돈 쓸 곳(2026-10-26, 13 문서 14-6)**: 개인레슨(이윤준·고준 + 출장 김승연·손진수·서진수 — 실존 인물 본인 허락) · 영양제·보양식. 원본 `Data/World/shop.json` + `lesson_lines.tsv`(대사, 대표가 다듬는 칸) → `WorldJson.FillShop`(DataOnly 가 같이). 규칙 `WorldModel.Shop.cs`(예약·노쇼·매주 고정·레슨 보상·효과·배부름·체감·영양제), 장면 `WorldRunner.Shop.cs`(체육관 레슨 진행·휴대폰 '레슨'·'가방' 탭·가게 차림표·HUD 효과 아이콘·싸움 체력 효과), 고르기 화면 `PickUi`, 판정 보정 `DrillGrader.Assist`(오픈월드만), 출장 코치 NPC `GymManager.Guests`(`GymSetup.Npcs` 5 — FBX `Art/Characters/M3/{Seungyeon,JinsooSon,JinsooSeo}.fbx`), Zone1 판매창 3 `WorldSetup.AddShopFronts`(명륜3가 원룸 2 1층, 인도 높이는 레이로). 주간 계획은 그 날 고정 레슨을 아침에 예약하고 대신 감. 시험 `M5ShopTests` W17~W22 · `M5WorldPlayTests` W23~W25 · W15 4월 말 지갑 10~25만 · 사진 `ZWorldShots.Z_World_Shop_Shots`. **빌드 인자 `-mute`**(`Scripts/Dev/MuteArg.cs`) = 처음부터 끝까지 소리 0 — 그래픽 켜고 플레이 QA 할 때. **함정**: 시험 중간 저장(Mid)은 그 날까지 지난 '놓칠 수 없는'(miss never) 카드도 끝낸 것으로 넣어야 따라잡기 카드가 시계를 밤으로 돌리지 않음.
- **함정**: ① 감독의 '시각(time)' 트리거는 다른 트리거가 있는 카드면 To 에 데려간다(C1-07 22:30) ② 시각 박힌 핵심 카드가 그 활동 시간 안이면 알바를 시작하지 않는다(5/1 국밥집 — 17:30 케이크) ③ 그룹(group)은 '둘 중 하나'(C1-03/03v) 전용 — 차례 사연(N09→N09b)엔 쓰지 않음 ④ M2 무대(주차장 인카운터)는 시비 카드·시비 반복(주차장 45m 안)일 때만 켬 — 늘 켜면 그리기 몫 1.9M ⑤ 녹화에서 오버레이 UI 를 카메라 공간으로 찍으면 화면 공간 외곽선이 UI 위로 번진다 → `ZWorldShots.CleanGrab`(기능 끈 UI 화소만 갈아 끼움) ⑥ 봇 루프의 안전 횟수는 '걸음'만 셀 것(바쁜 프레임까지 세면 3/17 에 멈춤) ⑦ 시험이 같은 정적 시계(GameClock)를 쓰므로 모델 둘을 번갈아 돌릴 땐 날짜를 다시 맞춤.

## 4. 배치 명령 (GUI 없이)

공통 준비(PowerShell). **`$env:TMPDIR` 줄은 대표 PC 에서 필수**(주의 1):
```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe"
$proj  = "C:\클로드\haengin1-repo\haengin1\unity\HaenginMainEvent"
$env:TMPDIR = $env:TEMP
function Run-Unity([string]$log, [string[]]$more) {
  $a = @('-batchmode','-quit','-projectPath',"`"$proj`"",'-logFile',"`"$log`"") + $more
  $p = Start-Process $unity -ArgumentList $a -PassThru -WindowStyle Hidden
  $p.WaitForExit()
  "종료 코드 $($p.ExitCode) — 로그 $log"
}
```
(`& $unity ...` 로 부르면 PowerShell 이 끝날 때까지 기다리지 않는다. `Start-Process -Wait` 는 Unity 가 남긴 자식 프로세스까지 기다려 로그가 끝나고도 10분쯤 더 멈춰 있었다 → Unity 프로세스만 `WaitForExit()` 로 기다린다.)

| 하는 일 | 명령 | 비고 |
|---|---|---|
| 임포트·컴파일 점검 | `Run-Unity "$env:TEMP\u_check.log" @('-nographics','-executeMethod','Haengin.EditorTools.BatchTools.ImportCheck')` | 로그에 `[BatchTools] 임포트 점검 결과 | 컴파일 에러(CS) N건 | 임포트 에러 M건`. 하나라도 있으면 exit 1. `-reimport Assets/_Project/Art/Characters/Siwoo` 를 붙이면 그 폴더를 강제 재임포트한 뒤 센다 |
| 캐릭터 임포트·재질·애니메이터 | `Run-Unity "$env:TEMP\u_char.log" @('-nographics','-executeMethod','Haengin.EditorTools.CharSetup.Build')` | FBX 5개 Humanoid 설정(뼈 매핑·바인드 포즈 기준 자세·클립 루프·제자리) → 클립마다 발바닥 높이 맞춤(로그 `발바닥 맞춤`) → 툰 재질 2개 → 걸음 측정 → `Anim/Siwoo.controller`·`Taeo.controller`. 로그 `[CharSetup] 걸음 측정 …` · `컨트롤러 … Walk ×0.980 …`. MoveTuning 의 걷기·달리기 속도를 바꾸면 다시 돌린다(재생 배율이 따라 바뀜). GLB 를 새로 받았으면 먼저 `blender -b --factory-startup --python tools/glb2fbx.py -- <glb> <fbx> <클립 이름>`(파일 머리 주석) |
| Sandbox 다시 만들기 | `Run-Unity "$env:TEMP\u_setup.log" @('-nographics','-executeMethod','Haengin.EditorTools.SandboxSetup.Build')` | 재질·볼륨·장면을 같은 경로에 덮어씀(이제 스스로 종료). `-modelYaw 90` 은 예전 정적 GLB 방향 |
| 스크린샷(정면) | `Run-Unity "$env:TEMP\u_shot.log" @('-executeMethod','Haengin.EditorTools.BatchTools.Screenshot','-scene','Assets/_Project/Scenes/Sandbox.unity','-out','C:\temp\front.png','-width','1920','-height','1080')` | **`-nographics` 를 빼야** GPU 로 렌더된다(창은 안 뜸). 가장 우선순위 높은 CM 카메라 자세를 메인 카메라에 옮겨 2배 슈퍼샘플로 찍음 |
| 스크린샷(3/4) | 위와 같고 `'-yaw','35'` 추가 | `-yaw` = `-pivot`(기본 `Siwoo`) 둘레로 카메라를 돈 각도. 그 밖에 `-camera <CM 이름>`, `-supersample 1~4`, `-show Siwoo_Rigged`(비활성 루트를 찍을 때만 켬), `-hide 이름`, `-animTime 초`(장면의 Humanoid Animator 를 대기 클립 그 시각 자세로 — 기본 1초. 편집 모드에선 Animator 가 돌지 않아 안 하면 A포즈) |
| Zone1 다시 만들기 | `Run-Unity "$env:TEMP\u_zone1.log" @('-nographics','-executeMethod','Haengin.EditorTools.Zone1Builder.Build')` | `Data/zone1.json` → `Scenes/Zone1.unity` + `Zone1_Mesh.asset` 를 덮어씀(재질은 같은 경로에서 값만 갱신, 결과는 결정적). `-nographics` 를 빼고 `'-shots','<폴더>'` 를 붙이면 같은 실행에서 점검 컷 7장(`z1_1_top` 평면 · `2_alley` 골목 눈높이 · `3_reveal` 계단참 성곽 · `4_waryong` · `5_target` 체크포인트·이름표·HUD · `6_startcam` · `7_uphill`)을 찍는다. 로그 `[Zone1Builder] 완료: … 길 가운데선 막힘 N곳 · NavMesh 길만 완주 9/9 …` 와 점검 메모. 실패하면 exit 1. 에디터 메뉴 **Haengin › Zone1 장면 다시 만들기** 도 같다. 데이터를 바꾸려면 먼저 `python tools/zone1_gen.py && python tools/zone1_check.py` |
| Zone1 평면도(위에서) | `Run-Unity "$env:TEMP\u_z1top.log" @('-executeMethod','Haengin.EditorTools.BatchTools.Screenshot','-scene','Assets/_Project/Scenes/Zone1.unity','-out','C:\temp\z1_top.png','-width','1800','-height','1300','-topdown','-area','-274,70,-130,174')` | 직교 투영, 화면 위 = 북. 찍는 동안만 안개·그림자 끔(`-shadows` 면 그림자 넣음). `-area` 없으면 모든 렌더러 범위 |
| Zone1 장면 컷 | 위와 같고 `-topdown -area …` 대신 `'-view','Zone1/Shots/Shot_Start'`(또는 Shot_StartCam · Shot_Uphill · Shot_Waryong · Shot_Reveal), `-width 1920 -height 1080` | `-view` = 그 오브젝트의 위치·방향·화각(꺼진 Camera)으로 찍음. `-fov 도` 로 화각 덮어쓰기, `-nofog` 로 안개 끄기 |
| M1 다시 만들기 | `Run-Unity "$env:TEMP\u_m1.log" @('-nographics','-executeMethod','Haengin.EditorGame.M1Setup.Build')` | 캐릭터(CharSetup) + 조정값(없을 때만) + `Prefabs/Player.prefab` + Zone1 다시 만들기(리그 포함) + 빌드 목록(Zone1, Sandbox). Sandbox 까지 한 번에: `M1Setup.BuildAll`(2026-10-06 실행 14초). `Zone1Builder.Build` 만 돌려도 리그가 다시 붙는다(BeforeSave 확장 지점). 리그만: `M1Setup.AddRigToZone1`. 로그 `[M1Setup]` |
| 맵 아트 패스(2026-10-24) | `Run-Unity "$env:TEMP\u_env.log" @('-nographics','-executeMethod','Haengin.EditorGame.EnvSetup.Build')` | `Art/Env` 들여오기 설정(EnvImport) → UTS 재질 33 + FBX 재질 바꿔 꿰기 → 소품 프리팹 40(LOD 3단) → 원경 호 메시·재질 → 렌더러 기능 Outline 붙이고 SSAO 끔 → 그림자 캐스케이드 2단·60m → 조명 프리셋 새벽·노을·밤 값 → 캐릭터 툰 재질 스텐실 1 → **Zone1 다시 만들기**(BeforeSave 끝에 `EnvSetup.AttachKit`: 구역 FBX 7 세우기·LOD·소켓 338 에 소품·창문 불 39묶음·간판 18 + TMP 글자·빛 자리 13·회색 상자 숨김·주차장 배치·바닥 재질·원경 카드). 약 25초. 에셋만: `EnvSetup.AssetsOnly`. `Zone1Builder.Build` 만 돌려도 키트는 다시 붙는다(에셋이 있으면). 소품 GLB 를 새로 받았으면 먼저 `blender -b --factory-startup -P tools/env/prop_lods.py -- --src concept/art/env/props --out unity/HaenginMainEvent/Assets/_Project/Art/Env/Props`, 건물 FBX·텍스처·원경은 `concept/art/env/{buildings,textures,sky,signs}` 에서 복사. 로그 `[EnvSetup]` |
| 맵 비교 사진 | `Run-Unity "$env:TEMP\u_envshots.log" @('-executeMethod','Haengin.EditorGame.EnvSetup.Shots','-out','<폴더>')` → `python tools/env/compose_compare.py <폴더> <결과 폴더>` | **`-nographics` 빼기**. 키트 미리 보기 8곳(`tools/env/preview_env.py shots()` 와 같은 자리) + 밤 2곳 + 시간대 4종 × 3곳 = 22장(1920×1080, 게임 카메라 근사, 시우 대기 자세) → `compare.jpg`(스타일 프레임과 나란히)·`tod.jpg`. `-only 1,7` 로 일부만. 촬영 중 이름표·HUD·체크포인트·무대 표시를 끄고, 저장하지 않는다 |
| 5차 근경 사진 | `Run-Unity "$env:TEMP\u_close5.log" @('-executeMethod','Haengin.EditorGame.EnvSetup.Closeups5','-out','<폴더>')` | **`-nographics` 빼기**. 간판 4곳 × 낮·밤(`sign_*_{day,night}.png`) + 근경 디테일 6(`detail_*.png`) + `closeups.txt`. `EnvSetup.Shots` 는 5차부터 탐색 카메라 B(5.5·3°·53·1.15)로 찍음(`-camA` 면 예전 자리값) |
| 탐색 카메라 비교(4차 — 2026-10-24 대표가 B 확정, `Settings/CamTuning.asset` = 5.5·5.9·피치 3·FOV 53/57·exploreTargetOffset −0.25) | `Run-Unity "$env:TEMP\u_camab.log" @('-executeMethod','Haengin.EditorGame.EnvSetup.CameraAB','-out','<폴더>')` | **`-nographics` 빼기**. 8곳 × A 지금(4.0·8°·FOV 45·표적 1.40) / B 넓게(5.5·3°·53·1.15) `ab_<n>_{A,B}.png` + `camera_ab.txt`(인물 화면 높이·발 위치). 기본값은 바꾸지 않음 |
| 체육관(2026-10-24) | `Run-Unity "$env:TEMP\u_gym.log" @('-executeMethod','Haengin.EditorGame.GymSetup.Build')` | **처음은 `-nographics` 를 빼고**(소품 텍스처 추출에 GPU) 들여오기 → 재질 28 → 소품 프리팹 25 → 이윤준·고준 Humanoid+툰·Gym.controller·관원 8·고준 전투 몸·스파링 정의 → DrillBook → `Scenes/Gym.unity` → 빌드 목록. 약 20초. 에셋만 `GymSetup.AssetsOnly`, 장면만 `GymSetup.SceneOnly`. 소품 GLB 를 새로 받았으면 먼저 `prop_lods.py`(위 '체육관'). 로그 `[GymSetup]` |
| 체육관 테스트·녹화 | `'-testFilter','Haengin.Tests.M4GymTests'`(G01~G16) · 사진·카드·녹화·성능은 `-nographics` 빼고 `'-testFilter','Haengin.Tests.ZGymShots','-gymshots','<폴더>'` | `<폴더>/compare/g01~g06.png`(카메라 소켓 6앵글) · `cards/D##.png`(결과 카드 10) · `seq_mitt/`·`seq_spar/`(960×540 30fps 240장 → `ffmpeg -framerate 30 -i f_%03d.png -an -c:v libx264 -pix_fmt yuv420p`) · `perf_gym.csv` · `summary.txt`. 묶기 `scratchpad/gym_compose.py <폴더> <출력>` |
| 체육관 스모크(빌드) | `Start-Process <빌드>\HaenginMainEvent.exe -ArgumentList '-batchmode','-nographics','-gymsmoke','-logFile',"$env:TEMP\gym_smoke.log"` | 타이틀 → 연습장 → 체육관 → D01 봇 S → 시뮬 1회 → `[Gym] 스모크: 끝` 뒤 스스로 종료 |
| 정식 모델(6·7단계) | `Run-Unity "$env:TEMP\u_formal.log" @('-nographics','-executeMethod','Haengin.EditorGame.FormalSetup.Build')` | FBX 3 들여오기(CharSetup) → HandPoseSet 관통 맞춤(`Settings/HandPoses_<이름>.asset`) → 태오 프리팹. 플레이어 프리팹은 `M1Setup.Build` 가 정식 모델로 |
| 배달 자전거(8단계) | `Run-Unity "$env:TEMP\u_bike.log" @('-nographics','-executeMethod','Haengin.EditorGame.BikeSetup.Build')` | 자전거 모델·Ride 상태·플레이어 부품·Zone1 코스(꼭대기 집 꾸밈 포함). 맵을 바꿨으면 먼저 `python tools/zone1_gen.py` → `zone1_check.py` → `M1Setup.Build` |
| 1화 배역 | `Run-Unity "$env:TEMP\u_cast.log" @('-nographics','-executeMethod','Haengin.EditorGame.CastSetup.Build')` → `StorySetup.Build` | 배역 FBX·컨트롤러·프리팹 → 무대 꾸밈은 `StorySetup.Build` 가 무대를 다시 만들 때 |
| 자전거 스모크(빌드) | `Start-Process <빌드>\HaenginMainEvent.exe -ArgumentList '-batchmode','-nographics','-bikesmoke','-logFile',"$env:TEMP\bike_smoke.log"` | 타이틀 → 1-2~1-4 봇 주행 → `[Bike] 스모크:` 줄(정차·지각·국물·태오 추월) |
| 무대 4곳 키트 | `Run-Unity "$env:TEMP\u_stagekit.log" @('-executeMethod','Haengin.EditorGame.StageKitSetup.Build')` → `StorySetup.Build` | 원본 복사·재질·소품 프리팹 → 무대를 다시 만들 때 키트·충돌·소품·불·문·표시 자리 |
| 장소 스모크(빌드) | `Start-Process <빌드>\HaenginMainEvent.exe -ArgumentList '-batchmode','-nographics','-stagesmoke','-logFile',"$env:TEMP\stage_smoke.log"` | 연습장 Zone1 → 출입문 4곳 들어가고 나오기 → `[Stage] 스모크:` 줄 |
| 1장 오픈월드 설정 | `Run-Unity "$env:TEMP\u_world.log" @('-nographics','-executeMethod','Haengin.EditorGame.WorldSetup.Build')` | `Settings/World.asset`(Data/World/*.json) → 이야기 데이터(StorySetup.Data) → 무대 다시 만들기(실내 2차 DressLife) → Zone1 다시 만들기(세계 루트) → 빌드 목록. 약 36초(+컴파일). 데이터만: `WorldSetup.DataOnly`(카드·밸런스 JSON 을 고친 뒤 — W01 이 에셋 = JSON 인지 봄). 로그 `[WorldSetup]` |
| 1장 시험 | `'-testFilter','Haengin.Tests.M5WorldTests\|Haengin.Tests.M5WorldPlayTests'` (W00~W15) · 성능 W16 은 그래픽 켜고 `'-testFilter','Haengin.Tests.ZWorldShots'` · 녹화·사진은 `'-worldshots','<폴더>'` 를 더함 | 녹화 = `<폴더>/{a_day,b_jobs_site,b_jobs_counter,c_card_C1-05,c_card_N01,c_card_C1-24,d_goal}/f_*.jpg` → `ffmpeg -framerate 20 -i f_%05d.jpg -an -c:v libx264 -pix_fmt yuv420p <이름>.mp4`(a_day 는 15fps 로 찍음) · 사진 `st_*.png`(실내 시간대)·`ui_*.png` · `notes_world.txt` · `perf_world.csv` |
| 1장 스모크(빌드) | `Start-Process <빌드>\HaenginMainEvent.exe -ArgumentList '-batchmode','-nographics','-ch1smoke','-logFile',"$env:TEMP\ch1_smoke.log"` | 타이틀 → 새 게임 → 봇(전부: 체험·청소 관원·드릴·사연·시비·알바)이 3/3 → 5/8 데모 끝 카드까지 → `[World] 스모크:` 줄(목표 판정·카드 수·맡기기 수) 뒤 종료. `-ch1straight` 를 더하면 직진 |
| M3 이야기 설정 | `Run-Unity "$env:TEMP\u_story.log" @('-nographics','-executeMethod','Haengin.EditorGame.StorySetup.Build')` | 글꼴 3종 · Story/Dialogue 에셋(json·TSV) · 자리 무대 6 · 컷신(없을 때만, `'-recut'` 이면 다시) · 카메라 프리팹 · InkMode 렌더러 기능 · Title.unity · Zone1 다시 만들기(이야기 루트·빛 자리) · 빌드 목록. 2026-10-06 약 5초(+컴파일). 데이터만: `StorySetup.DataOnly`. 입력 맵을 바꿨으면 먼저 `python tools/input_maps.py`, 대사 초안은 `python tools/script2dlg.py --all`. 로그 `[StorySetup]` |
| M2 전투 설정·연습장 | `Run-Unity "$env:TEMP\u_c2.log" @('-nographics','-executeMethod','Haengin.EditorGame.CombatSetup.Build')` | 레이어 12·13 + 충돌 행렬 + 전투 에셋(없을 때만) + 시우 FBX(CharSetup) + **적 모델·전투 클립·측정표·애니메이터(ClipSetup)** + Player 프리팹(전투 부품·FighterAnim·HandShape) + 적 정의·프리팹 + `Scenes/CombatLab.unity`. 2026-10-06 약 2분 40초. Zone1 까지 다시: `CombatSetup.BuildAll`. 로그 `[CombatSetup]` · `[ClipSetup]`(클립마다 타격 시각·뻗은 거리, `재생 배율 …` 한 줄 — 1.6 넘는 것은 '앞 N초 건너뜀') |
| PlayMode 테스트 | 아래 '테스트 실행' | `-runTests` 에는 **`-quit` 를 붙이지 않는다**(끝나면 스스로 닫힘). 종료 코드 0 = 전부 통과, 2 = 실패 있음 |
| Windows 빌드 | `Run-Unity "$env:TEMP\u_build.log" @('-nographics','-executeMethod','Haengin.EditorTools.BatchTools.BuildWindows','-out','C:\클로드\haengin1-builds\M1')` | 빌드 목록 장면(**Zone1 첫 장면** → Sandbox), Windows 64비트, **Mono**, 개발 빌드 아님. M1 결과물 위치 = 레포 밖 `C:\클로드\haengin1-builds\M1\HaenginMainEvent.exe`(약 167MB). 빌드가 끝나면 `Fonts/OFL.txt` 를 `<out>\licenses\NotoSansKR-OFL.txt` 로 복사하고(OFL 1.1 은 글꼴과 함께 배포할 때 라이선스 동봉 의무), **배포 금지** 디버그 폴더 `…_BurstDebugInformation_DoNotShip` 을 지운다(2026-10-06부터 자동, 로그 `글꼴 라이선스 동봉` · `배포 금지 폴더 삭제`). 결과 `<out>\HaenginMainEvent.exe`. 로그에 `[BatchTools] 빌드 결과: Succeeded | 크기 | 시간 | 에러 | 경고`. 실패하면 exit 1. 임시 폴더를 못 쓰면 시작 전에 바로 실패 처리 |
| M2 빌드 · 실행 확인 | 위 빌드 명령에서 `-out` 만 `C:\클로드\haengin1-builds\M2`(M1 폴더는 그대로 둠). 확인: `$p = Start-Process "C:\클로드\haengin1-builds\M2\HaenginMainEvent.exe" -ArgumentList '-batchmode','-nographics','-m2smoke','-logFile',"$env:TEMP\m2_smoke.log" -PassThru -WindowStyle Hidden; $p.WaitForExit(300000)` | 창·소리 없이 돈다(배치 모드 플레이어는 오디오를 끔). 로그에 `[M1] 실행 확인` · `[M2] 실행 확인: 장면 Zone1 · 인카운터 … · 야차 …` · `[M2] 스모크: …`(인카운터·야차 한 판씩) 줄, 끝나면 스스로 종료. 안 끝나면 그 PID 만 끈다 |

실제로 돌려 본 결과(2026-10-05): ImportCheck 컴파일 에러 0 · 임포트 에러 0, 스크린샷 정면·3/4 정상, 빌드 Succeeded(138MB, 52초, 에러 0·경고 0), 빌드한 exe 를 창 모드로 10초 띄워 살아 있음 확인.
체육관 2차 패스(2026-10-24): 빌드 Succeeded(**525.5MB**, 에러 0·경고 407) → `M3map`, `-gymsmoke` 통과, 테스트 97개 중 87 통과·0 실패·10 건너뜀, 실내 1080p 5.7~6.1ms · 삼각형 1.39M(해 끔). 전용 동작 3종 24cr.
체육관 통합(2026-10-24): 빌드 Succeeded(**524.5MB**, 100초, 에러 0·경고 409) → `M3map` 덮어씀(장면 목록에 Gym.unity 추가), `-gymsmoke` 로 타이틀 → 체육관 → D01 봇 S → 시뮬 1회 확인(5.2초). 테스트 **97개 중 통과 87 · 실패 0 · 건너뜀 10**(178초). 체육관 저녁 피크 1080p 6.4ms · 삼각형 최대 2.16M.
M3 맵 아트 패스 7차(2026-10-25, 역광 테두리·담쟁이·둥근 광장·밤 톤·주차장·와룡 축대 — 맵 아트 패스 마감): 빌드 Succeeded(**653.1MB**, 에러 0) → `M3map`, `-m2smoke`·`-gymsmoke` 둘 다 통과. 테스트 **99개 중 통과 88 · 실패 0 · 건너뜀 11**. D08 이야기 모드 밤 상가거리 95% 9.01ms · 0.91M · 그리기 978 · SetPass 139. 렌더러 기능 `Rim`(ScreenRim) 추가 — 렌더러 자산은 체육관과 같이 씀(실내는 하늘이 없어 영향 없음).
M3 맵 아트 패스 6차(2026-10-25, 간판 결함·글자 검사·정류장 광고·맨땅 초록·밤 빛 웅덩이·그리기 호출 줄이기·난간): 빌드 Succeeded(**647.9MB**, 에러 0) → `M3map`, `-m2smoke`(로그 `[Env] 간판 글자 확인: TMP 150개 · 빠진 글자 0`)·`-gymsmoke` 둘 다 통과. 테스트 **99개 중 통과 88 · 실패 0 · 건너뜀 11**(S01·S02 새로). D08 이야기 모드 밤 상가거리 95% 9.16ms · 0.92M · **그리기 968 · SetPass 136**(5차 1,727 · 283). 작은 소품 칸 합치기를 켜면 빌드가 1.73GB(인스턴스마다 메시 복제)라 끔.
M3 맵 아트 패스 5차(2026-10-25, 간판 몸체·면 그림·가게 안·창 시트지·거리 데코·표면 데칼·움직임): 빌드 Succeeded(**614.1MB**, 에러 0) → `M3map`, `-m2smoke`·`-gymsmoke` 둘 다 통과. 테스트 **97개 중 통과 86 · 실패 0 · 건너뜀 11**. D08 이야기 모드 밤 상가거리 95% 9.38ms · 0.87M · 그리기 1,727 · SetPass 283.
M3 맵 아트 패스 4차(2026-10-24, 벽면 밀도·카드 수관·붓 구름 하늘·거리별 선 굵기·카메라 비교): 빌드 Succeeded(**518.7MB**, 에러 0·경고 410) → `M3map`, `-m2smoke`·`-gymsmoke` 둘 다 통과. 테스트 **97개 중 통과 86 · 실패 0 · 건너뜀 11**. D08 이야기 모드 밤 상가거리 95% 8.37ms · 0.63M · 이야기 모드 낮 명륜3가 골목 95% 6.36ms · 0.64M(새 경로). 샷 촬영 배치는 끝날 때 가끔 종료 코드 −1073741819(Mono 정리 중, 사진은 저장됨).
M3 맵 아트 패스 3차(2026-10-24, 길바닥·가로수 줄·잎 카드·밤 주황·필로티 콜라이더): 빌드 Succeeded(**536.0MB**, 체육관 포함, 에러 0) → `M3map`, `-m2smoke`·`-gymsmoke` 둘 다 통과. 테스트 **97개 중 통과 86 · 실패 0 · 건너뜀 11**. D08 이야기 모드 밤 상가거리 95% 8.00ms · 0.70M(무대 켠 연습장 모드 2.54M 중 1.95M 은 M2 적·구경꾼 — `ZStoryShots` 에 `-tribreak` 를 주면 묶음별 내역).
M3 맵 아트 패스 2차(2026-10-24, 식생·바닥·데칼·색 보정): 빌드 Succeeded(**401.9MB**, 에러 0·경고 379) → `M3map` 덮어씀, `-m2smoke` 로 M1·M2 실행 확인 + 인카운터·야차 승. 테스트 **80개 중 통과 70 · 실패 0 · 건너뜀 10**. D08(밤 상가거리) 95% 7.96ms · 삼각형 최대 2.53M(그림자 거리 45m).
M3 맵 아트 패스 1차(2026-10-24): 빌드 Succeeded(**363.5MB**, 에러 0·경고 378(UTS·글꼴 경고)) → `C:\클로드\haengin1-builds\M3map`(M1·M2 폴더 그대로) · OFL 3종 동봉 · DoNotShip 삭제. `-batchmode -nographics -m2smoke` 로 `[M1] 실행 확인`·`[M2] 실행 확인`·스모크 인카운터 승(탈락 3/3, 34.7초)·야차 승(30.2초) 뒤 스스로 종료. 처음 빌드는 773.7MB 였다 — ① 소품 프리팹 338개가 `BatchingStatic` 이라 정적 배칭이 장면 메시 280MB 를 만들었고(→ 소품은 Occludee 만, GPU 인스턴싱), ② glTFast 가 GLB 안 텍스처를 압축 없이 들여 300MB(→ `Art/Env/Props/Tex/*.png` 로 뽑아 BC7). 테스트 전체(그래픽 켠 배치) **80개 중 통과 70 · 실패 0 · 건너뜀 10**(녹화), 95초.
M1 4차-2(2026-10-06, 대기·걷기 클립 교체 + 걷기 1.4): 빌드 Succeeded(167.4MB, 13초, 에러 0·경고 0) → 덮어씀 · OFL 동봉 · DoNotShip 자동 삭제. 최소화·`-nocursorlock` 으로 10초 안에 실행 확인 세 줄(시우 접지 True 상태 Idle · 카메라 4.00m · vSync 1), 예외 0, 그 PID 만 종료.
M1 4차(2026-10-06, 리깅 모델·애니메이터): 빌드 Succeeded(167.0MB, 14초, 에러 0·경고 0) → `C:\클로드\haengin1-builds\M1` 덮어씀 · OFL 동봉 · DoNotShip 자동 삭제. exe 를 창 모드 1280×720·최소화·`-nocursorlock` 으로 띄워 15초 안에 `[M1] 실행 확인: 장면 Zone1 · 시우 (-197.5, 30.68, 97.4) 접지 True 상태 Idle · 카메라 거리 4.00m · 6 fps(vSync 1 · 품질 PC) · 화면 1280x720`(최소화라 6fps — `runInBackground` 꺼짐) · 길잡이 · 화면 UI 세 줄, 예외 0, 그 PID 만 종료.
M1 3차(2026-10-05, 카메라·조작 다듬기): 빌드 Succeeded(148.3MB, 13초, 에러 0·경고 0) → `C:\클로드\haengin1-builds\M1` 덮어씀, DoNotShip 폴더 지움. exe 를 창 모드 1280×720·최소화·`-nocursorlock` 으로 띄워 8초 만에 `[M1] 실행 확인: 장면 Zone1 · 시우 (-197.5, 30.68, 97.4) 접지 True 상태 Idle · 카메라 거리 4.00m` · `[M1] 길잡이 확인: HUD "다음: 후문 상가거리 · 19m" · 글꼴에 없는 글자 0` · `[M1] 화면 UI 확인: 글꼴 KR_Bold_SDF · 글꼴에 없는 글자 0 · 메뉴 입력 Menu 맵 · 카메라 자동 정렬 걷기·달리기` 확인, 예외 0, 그 PID 만 종료.
M1(2026-10-05): 빌드 Succeeded(Zone1 + Sandbox, 139.6MB, 에러 0·경고 0). exe 를 창 모드(1280×720)·최소화·`-nocursorlock` 으로 15초 띄워 응답 중 확인, Player.log 예외 0, `[M1] 실행 확인: 장면 Zone1 · 시우 (-197.4, 30.85, 94.7) 접지 True 상태 Idle · 카메라 거리 2.16m` 확인 후 그 PID 만 종료.

**테스트 실행**(결과 XML + 로그. 게임 화면도 찍으려면 `-nographics` 를 빼고 `-m1shots <폴더>` 추가):
```powershell
$a = @('-batchmode','-projectPath',"`"$proj`"",'-runTests','-testPlatform','PlayMode',
       '-testResults',"`"$env:TEMP\m1_tests.xml`"",'-logFile',"`"$env:TEMP\m1_tests.log`"")   # -quit 없음
$p = Start-Process $unity -ArgumentList $a -PassThru -WindowStyle Hidden; $p.WaitForExit(); "exit $($p.ExitCode)"
[xml]$x = Get-Content "$env:TEMP\m1_tests.xml" -Encoding UTF8; $x.'test-run' | Select-Object total, passed, failed, skipped
```
일부만: `'-testFilter','Haengin.Tests.ZoneTests'`. M3 만: `'-testFilter','Haengin.Tests.M3StoryTests|Haengin.Tests.M3ATalkInputTests|Haengin.Tests.M3TalkTests|Haengin.Tests.M3CutTests|Haengin.Tests.M3LightTests|Haengin.Tests.M3BookTests|Haengin.Tests.M3SaveTests'`, 녹화 `-nographics` 빼고 `'-testFilter','Haengin.Tests.ZStoryShots','-m3shots','<폴더>'`(PNG·프레임 — mp4 는 `ffmpeg -framerate 20 -i f_%04d.png -an -c:v libx264 -pix_fmt yuv420p`). 전투만: `'-testFilter','Haengin.Tests.CombatTests|Haengin.Tests.CombatInputTests|Haengin.Tests.EnemyTests|Haengin.Tests.ClipTests|Haengin.Tests.HeatTests|Haengin.Tests.HudTests'`. 전투 연습장 녹화: `-nographics` 빼고 `'-testFilter','Haengin.Tests.ZCombatShots','-c2shots','<폴더>'`. 9단계 띠 사진·손 확대·1:3: `-nographics` 빼고 `'-testFilter','Haengin.Tests.ZClipShots','-c9shots','<폴더>'`(PNG 와 `notes9.txt`, 띠마다 `meta.csv` — 묶음·mp4 는 밖에서). 10~12단계 기세 액션·이펙트·HUD: `'-testFilter','Haengin.Tests.ZHeatShots','-c10shots','<폴더>'`(`notes10.txt`). 13~16단계 무대: `'-testFilter','Haengin.Tests.StageTests'`(C17②·C18·C19·C22), 녹화 `'-testFilter','Haengin.Tests.ZStageShots','-c13shots','<폴더>'`(`notes13.txt`, 프레임 PNG 는 3프레임마다 = 20fps 로 묶음).

## 5. 주의

1. **TMPDIR (대표 PC 전용 문제, 2026-10-05 해결)** — 대표 지시로 이스트소프트 프로그램 9개를 지우고 사용자 환경변수 TMPDIR 도 지웠다. 새로 켠 프로그램(Hub 재시작 후 연 에디터 포함)에서는 더 이상 문제가 없다. 다만 그 전에 켜진 프로세스(이미 떠 있던 Hub, Claude 앱이 띄운 셸)는 옛 값을 물려받으므로 배치 실행에서는 계속 `$env:TMPDIR=$env:TEMP` 줄을 둔다. 이전 기록: 사용자 환경변수 `TMPDIR = C:\Users\Public\Documents\ESTsoft\CreatorTemp`(ESTsoft 프로그램이 넣은 것)인데 이 폴더에는 새 파일을 만들 수 없다. Unity 의 Mono 는 `TMPDIR` 을 `TEMP` 보다 먼저 보고 첫 값을 계속 쓰기 때문에 `Path.GetTempFileName()` 을 쓰는 단계가 깨진다 → **빌드의 Burst AOT 단계가 실패**(`AsyncPluginsFromLinker failed ... FileNotFoundException ... CreatorTemp`), 첫 임포트 때 API Updater 예외도 났다. 배치 실행은 띄우기 전에 `$env:TMPDIR=$env:TEMP` 로 해결. **Hub 로 연 에디터에서 빌드할 때도 같은 문제가 생긴다** → 근본 해결은 사용자 환경변수 TMPDIR 을 지우거나 `%TEMP%` 로 바꾸는 것(대표 결정, ESTsoft 프로그램 쪽 영향 확인 필요). 에디터 안에서는 `TempPathGuard` 가 문제를 찾아 경고만 한다(프로세스 안에서 바꿔도 소용없음을 확인함).
2. **한글 경로** — 프로젝트를 `C:\클로드\...` 에 둔 채로 패키지 해결(git 패키지 포함)·컴파일·GLB 임포트·셰이더 컴파일·GPU 스크린샷·Windows(Mono) 빌드가 모두 정상이었다. 그래서 `C:\dev\...` 로 옮기지 않았다. 다만 **IL2CPP 빌드는 시험하지 않았다** — IL2CPP 로 바꿀 때 경로 문제가 나면 프로젝트를 `C:\dev\haengin1-unity\HaenginMainEvent` 로 옮기고 이 문서에 위치를 적는다. 빌드 출력 폴더는 영문 경로를 권장.
   **진짜 제약은 경로 길이다.** 프로젝트 경로가 53자이고 Library 안 가장 긴 상대 경로가 202자라 합쳐 255자 — Windows 한계 259자까지 4자 남는다. 검수에서 같은 프로젝트를 120자 깊이에 복제해 열자 DirectoryNotFoundException 57건·임포트 에러 21건이 났다(53자 한글 경로에서는 0건). → **이 프로젝트 폴더의 전체 경로는 53자 이하로 둔다. 레포를 GitHub Desktop 기본 위치(Documents\GitHub\...)나 worktree 같은 더 깊은 폴더에 클론하지 말 것.** 꼭 필요하면 `C:\dev\h1` 같은 짧은 경로로 옮긴다. Hub 에서 Add 할 때도 같다.
3. **Git LFS** — `HaenginMainEvent/.gitattributes` 가 glb·fbx·png·jpg·psd·tga·exr·wav·mp3·ogg·vrm·blend 등을 LFS 로, `*.unity *.prefab *.asset *.mat *.meta` 등 Unity YAML 은 LF 텍스트(+unityyamlmerge 병합 드라이버 이름)로 지정한다. 커밋 전에 한 번 `git lfs install` 필요. 이 `.gitattributes` 는 이 폴더 아래에만 적용되며, 레포의 `concept/art/3d/*.glb` 원본은 LFS 가 아니다. 시우 GLB 2개(2.4MB·3.4MB)는 Assets 쪽에 복사본이 있다.
4. `.gitignore` 로 `Library/ Temp/ Obj/ Logs/ UserSettings/ Builds/ *.csproj *.sln` 등을 뺀다. 빈 폴더 `Scripts/` 는 `.gitkeep` 로 유지(Unity 는 점으로 시작하는 파일을 무시).
   빌드할 때 `com.unity.collections` 가 끌어온 `test-framework.performance` 가 `Assets/Resources/` 에 json 을 잠깐 만들고 지워서 **빈 `Assets/Resources`(+.meta)** 가 남을 수 있다. `BuildWindows` 는 원래 없던 빈 폴더면 지운다. 에디터에서 빌드한 뒤 빈 폴더가 남았으면 커밋하지 말고 지울 것.
5. **다른 플랫폼을 추가하면** UniGLTF 기본 임포터를 끄는 define 두 개(`UNIGLTF_DISABLE_DEFAULT_GLB_IMPORTER`, `UNIGLTF_DISABLE_DEFAULT_GLTF_IMPORTER`)를 그 플랫폼의 Scripting Define Symbols 에도 넣는다. 지금은 Standalone 에만 있어서, 빠지면 glTFast 와 .glb 임포터가 충돌한다.
6. 첫 실행한 플레이어는 어셈블리 로드에 8초 걸렸다(백신 검사로 보임). 10초 안에 장면 표시까지 확인하지는 못했고, 죽지 않고 응답 중인 것까지 확인.
7. 배치 실행마다 스크립트 컴파일 단계가 100~170초 걸린다(대부분 `IL Post Processor runner` 연결 대기). 백신(ESTsoft) 실시간 검사 영향으로 보인다 — Unity 에디터 폴더와 프로젝트 `Library` 를 검사 예외에 넣을지는 대표 결정.
8. **Unity Hub 가 꺼져 있으면 배치 실행이 라이선스 오류로 바로 끝난다**(2026-10-05 2차 작업에서 확인: 종료 코드 198, 로그 `No valid Unity Editor license found` · `Found 0 entitlement groups`). MSIX Hub 의 라이선스 클라이언트(`...\WindowsApps\UnityTechnologies.UnityHub_*\app\UnityLicensingClient_V1`)가 떠 있어야 대표 계정 라이선스를 쓴다 — 에디터에 들어 있는 라이선스 클라이언트로는 안 된다(MSIX 가상화 폴더라 보이지 않는 것으로 보임). 배치 실행 전에 Hub 를 켜 둔다(시작 메뉴 Unity Hub, Hub CLI 는 쓰지 않음).
