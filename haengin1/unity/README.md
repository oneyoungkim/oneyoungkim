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
│  │  ├─ Anim/                      Siwoo.controller(대기·걷기·달리기 블렌드 트리) · Taeo.controller(대기) — CharSetup 이 만듦
│  │  ├─ Data/                      zone1.json (1구역 배치도, tools/zone1_gen.py 가 만듦 — 손으로 고치지 말 것)
│  │  ├─ Materials/                 M_SiwooAnim_Toon · M_Taeo_Toon(리깅 모델, UTS) · M_Siwoo_Toon(예전 정적) · M_SiwooRigged_Toon · M_Floor_Sand(URP Lit)
│  │  │  └─ Zone1/                  M_Z1_<종류>.mat — 그레이박스 단색 재질 + T_Z1_Stone·Paving·Beam.png 생성 텍스처(Zone1Builder 가 만듦)
│  │  ├─ Fonts/                     NotoSansKR-Bold.ttf + OFL.txt(OFL 1.1, 게임 동봉 가능) · KR_Bold_SDF.asset(TMP 동적 한글 글꼴) · KR_Label.mat(이름표, 오버레이)
│  │  ├─ Scenes/                    Sandbox.unity · Zone1.unity + Zone1_Mesh.asset(생성 메시 73개)
│  │  ├─ Input/                     HInput.inputactions — M1 입력(Explore 맵 + Menu 맵(일시정지 메뉴), 키보드·마우스·패드)
│  │  ├─ Prefabs/                   Player.prefab — 시우 리깅 FBX(툰, Animator + LocoAnim) + CharacterController·PlayerMotor·PInput + Visual(BodyLean) + CamTarget (M1Setup 이 만듦)
│  │  ├─ Scripts/                   런타임 코드(어셈블리 Haengin.Game): Player/ PlayerMotor·PInput·BodyLean·LocoAnim·MoveTuning · Cam/ CamRig·CamInput·CamTuning·CamClearance · Core/ RigFactory·Layers·GameState·ModelFit·RouteData · Route/ RouteGuide·RouteHud·NameTags · UI/ GameUi(조작 안내·알림·일시정지 메뉴) · Dev/ DebugHud(F1)
│  │  ├─ Editor/                    BatchTools.cs · CharSetup.cs(리깅 캐릭터 임포트·재질·애니메이터) · SandboxSetup.cs · TempPathGuard.cs · Zone1Builder.cs · Zone1Data.cs · MiniJson.cs · StableIds.cs(결정적 fileID) · KoreanFont.cs (어셈블리 Haengin.EditorTools)
│  │  │  └─ Game/                   M1Setup.cs · RouteSetup.cs (어셈블리 Haengin.EditorGame — 게임 코드를 참조하는 장면 설정)
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

### M1 플레이어·카메라 (`M1Setup` 이 Zone1 에 붙이는 리그, 2026-10-05)
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
| M3 이야기 설정 | `Run-Unity "$env:TEMP\u_story.log" @('-nographics','-executeMethod','Haengin.EditorGame.StorySetup.Build')` | 글꼴 3종 · Story/Dialogue 에셋(json·TSV) · 자리 무대 6 · 컷신(없을 때만, `'-recut'` 이면 다시) · 카메라 프리팹 · InkMode 렌더러 기능 · Title.unity · Zone1 다시 만들기(이야기 루트·빛 자리) · 빌드 목록. 2026-10-06 약 5초(+컴파일). 데이터만: `StorySetup.DataOnly`. 입력 맵을 바꿨으면 먼저 `python tools/input_maps.py`, 대사 초안은 `python tools/script2dlg.py --all`. 로그 `[StorySetup]` |
| M2 전투 설정·연습장 | `Run-Unity "$env:TEMP\u_c2.log" @('-nographics','-executeMethod','Haengin.EditorGame.CombatSetup.Build')` | 레이어 12·13 + 충돌 행렬 + 전투 에셋(없을 때만) + 시우 FBX(CharSetup) + **적 모델·전투 클립·측정표·애니메이터(ClipSetup)** + Player 프리팹(전투 부품·FighterAnim·HandShape) + 적 정의·프리팹 + `Scenes/CombatLab.unity`. 2026-10-06 약 2분 40초. Zone1 까지 다시: `CombatSetup.BuildAll`. 로그 `[CombatSetup]` · `[ClipSetup]`(클립마다 타격 시각·뻗은 거리, `재생 배율 …` 한 줄 — 1.6 넘는 것은 '앞 N초 건너뜀') |
| PlayMode 테스트 | 아래 '테스트 실행' | `-runTests` 에는 **`-quit` 를 붙이지 않는다**(끝나면 스스로 닫힘). 종료 코드 0 = 전부 통과, 2 = 실패 있음 |
| Windows 빌드 | `Run-Unity "$env:TEMP\u_build.log" @('-nographics','-executeMethod','Haengin.EditorTools.BatchTools.BuildWindows','-out','C:\클로드\haengin1-builds\M1')` | 빌드 목록 장면(**Zone1 첫 장면** → Sandbox), Windows 64비트, **Mono**, 개발 빌드 아님. M1 결과물 위치 = 레포 밖 `C:\클로드\haengin1-builds\M1\HaenginMainEvent.exe`(약 167MB). 빌드가 끝나면 `Fonts/OFL.txt` 를 `<out>\licenses\NotoSansKR-OFL.txt` 로 복사하고(OFL 1.1 은 글꼴과 함께 배포할 때 라이선스 동봉 의무), **배포 금지** 디버그 폴더 `…_BurstDebugInformation_DoNotShip` 을 지운다(2026-10-06부터 자동, 로그 `글꼴 라이선스 동봉` · `배포 금지 폴더 삭제`). 결과 `<out>\HaenginMainEvent.exe`. 로그에 `[BatchTools] 빌드 결과: Succeeded | 크기 | 시간 | 에러 | 경고`. 실패하면 exit 1. 임시 폴더를 못 쓰면 시작 전에 바로 실패 처리 |
| M2 빌드 · 실행 확인 | 위 빌드 명령에서 `-out` 만 `C:\클로드\haengin1-builds\M2`(M1 폴더는 그대로 둠). 확인: `$p = Start-Process "C:\클로드\haengin1-builds\M2\HaenginMainEvent.exe" -ArgumentList '-batchmode','-nographics','-m2smoke','-logFile',"$env:TEMP\m2_smoke.log" -PassThru -WindowStyle Hidden; $p.WaitForExit(300000)` | 창·소리 없이 돈다(배치 모드 플레이어는 오디오를 끔). 로그에 `[M1] 실행 확인` · `[M2] 실행 확인: 장면 Zone1 · 인카운터 … · 야차 …` · `[M2] 스모크: …`(인카운터·야차 한 판씩) 줄, 끝나면 스스로 종료. 안 끝나면 그 PID 만 끈다 |

실제로 돌려 본 결과(2026-10-05): ImportCheck 컴파일 에러 0 · 임포트 에러 0, 스크린샷 정면·3/4 정상, 빌드 Succeeded(138MB, 52초, 에러 0·경고 0), 빌드한 exe 를 창 모드로 10초 띄워 살아 있음 확인.
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
