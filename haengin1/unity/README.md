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
│  │  ├─ Art/Characters/Siwoo/      siwoo_tripo_v1.glb (리깅 없음) · siwoo_tripo_v1_rigged.glb (뼈 24개, 팔 스키닝 깨짐 — 재생성 예정)
│  │  ├─ Data/                      zone1.json (1구역 배치도, tools/zone1_gen.py 가 만듦 — 손으로 고치지 말 것)
│  │  ├─ Materials/                 M_Siwoo_Toon(UTS) · M_SiwooRigged_Toon · M_Floor_Sand(URP Lit)
│  │  │  └─ Zone1/                  M_Z1_<종류>.mat — 그레이박스 단색 재질(Zone1Builder 가 만듦)
│  │  ├─ Scenes/                    Sandbox.unity · Zone1.unity + Zone1_Mesh.asset(생성 메시 64개)
│  │  ├─ Input/                     HInput.inputactions — M1 입력(Explore 맵, 키보드·마우스·패드)
│  │  ├─ Prefabs/                   Player.prefab — 시우 정적 GLB(툰) + CharacterController·PlayerMotor·PInput + Visual(BodyLean) + CamTarget (M1Setup 이 만듦)
│  │  ├─ Scripts/                   런타임 코드(어셈블리 Haengin.Game): Player/ PlayerMotor·PInput·BodyLean·MoveTuning · Cam/ CamRig·CamInput·CamTuning · Core/ RigFactory·Layers·GameState·ModelFit·RouteData · Dev/ DebugHud
│  │  ├─ Editor/                    BatchTools.cs · SandboxSetup.cs · TempPathGuard.cs · Zone1Builder.cs · Zone1Data.cs · MiniJson.cs (어셈블리 Haengin.EditorTools)
│  │  │  └─ Game/                   M1Setup.cs (어셈블리 Haengin.EditorGame — 게임 코드를 참조하는 장면 설정)
│  │  ├─ Tests/                     PlayMode 테스트(어셈블리 Haengin.Tests): MoveTests · ZoneTests · InputTests · Lab
│  │  └─ Settings/                  Sandbox_FilmVolume.asset · Zone1_FilmVolume.asset (필름 후처리, 같은 레시피) · MoveTuning.asset · CamTuning.asset (조정값, 없을 때만 만듦)
│  ├─ _Template/                    URP 템플릿 샘플(Readme, TutorialInfo, SampleScene) — 지우지 않고 격리
│  ├─ Settings/                     URP 파이프라인 에셋(PC/Mobile_RPAsset·Renderer, 전역 설정) — 템플릿 것이지만 실제 사용 중
│  └─ InputSystem_Actions.inputactions   프로젝트 전역 입력 액션(템플릿 기본)
├─ Packages/  ProjectSettings/      커밋 대상
└─ Library/ Temp/ Logs/ UserSettings/ Builds/   커밋 안 함(.gitignore)
```

`Haengin.EditorTools` 어셈블리는 게임 코드(`Haengin.Game`)에 기대지 않게 따로 떼어 두었다. 게임 코드가 필요한 장면 설정은 `Haengin.EditorGame`(Editor/Game) 이 맡고, `Zone1Builder.BeforeSave` 확장 지점에 등록해서 Zone1 을 다시 만들 때 리그를 붙인다(한 방향 참조만). 다만 배치 실행에서 스크립트 컴파일 에러가 있으면 Unity 가 `Scripts have compiler errors.` 를 남기고 `-executeMethod` 전에 종료 코드 1로 끝난다(검수에서 확인) → **컴파일 에러는 종료 코드 1과 로그의 `error CS` 줄로 판단**한다. ImportCheck 는 컴파일이 통과한 뒤의 임포트 에러를 세는 용도.

### Sandbox 장면 구성 (`SandboxSetup.Build` 가 만든 것)
- 배경 = 종이색 **#F4EFE6**(카메라 단색), 바닥 = 모래 베이지 **#E9E0CC** 300m 평면, 선형 안개 #E9E0CC 30~140m
- Directional Light: 따뜻한 흰빛, 앞-왼쪽 위(48°, 150°), 부드러운 그림자(강도 .7)
- **Siwoo**(원점): Tripo GLB 는 정면이 glTF +X → glTFast 가 X 를 뒤집어 Unity 에선 -X → **Y +90°** 로 돌려 +Z(Unity 표준 앞)를 보게 함. 키 **1.74m** 로 스케일(2026-10-05 대표 결정: 시우 174cm 61kg), 발 y=0. 모델 자체는 162cm 기준 설정화로 만든 것이라 비율이 조금 작은 사람 체형 — A포즈 재생성 때 174cm로 다시 만든다
- **Siwoo_Rigged**: x=1.6 에 **비활성**(재생성 전까지 참고용)
- Main Camera(+CinemachineBrain, SMAA, 후처리 켬) + **CM_FullBody**(FOV 30°, 거리 3.93m, 높이 0.97m, 전신이 들어옴)
- Global Volume: 채도 -15(=×.85), 대비 -6, 스플릿 톤(그림자 #2F4A5A 쪽 / 밝은 쪽 #F3D9B0 쪽), 톤매핑 없음, 블룸 끔
- 툰 재질(M_Siwoo_Toon): 기본 텍스처 그대로, 1단 그림자 = 텍스처 × (.78,.71,.74) 모브, 2단 = 1단 × (.84,.80,.80), 경계 feather .02, 스펙큘러·림 0, 외곽선 #1A1417 폭 2.6(오브젝트 공간 ×0.001, 1080p 전신 컷에서 실루엣 2~3px). 텍스처 한 장에 피부·옷·머리가 섞여 있어 부위별 곱셈값은 아직 못 나눔(부위 마스크 또는 재질 분리 필요).

### Zone1 장면 구성 (`Zone1Builder.Build` 가 만든 것, 2026-10-05)
혜화동 1구역(성대 후문 ~ 와룡공원) M1 그레이박스. 배치도 `Data/zone1.json` → `Scenes/Zone1.unity`. 데이터 규칙은 `docs/06_M1_그레이박스_설계.md` 14장, 레이어·계단 충돌 규칙은 `docs/07_M1_조작_설계.md` 3-5·3-6.
- **좌표**: zone1.json 그대로(원점 = 혜화동 로터리, +X 동, +Z 북, 1 = 1m). 장면을 옮기지 않는다(다음 구역과 같은 좌표로 이어 붙이기 위해). 1구역은 x −274 ~ −130, z 70 ~ 174.
- **장면 루트** `Zone1/`
  - `Ground` — `Terrain`(73×53 높이 격자 메시 + MeshCollider) · `Apron`(구역 밖으로 내려가는 지형, 보이기만) · 패드 6개(윗면 = 패드 높이, 아래로 3m)
  - `Roads` — 길 16줄. 폴리라인을 폭만큼 넓힌 띠(위 = 걷는 면, 아래로 3m) + MeshCollider. 겹치는 곳 깜빡임 방지로 종류별 1~5cm 차등
  - `Blocks/<종류>` — 상자 126개(BoxCollider). 소나무 = 줄기 + 잎 상자(콜라이더는 지름 0.6m 캡슐), 풀숲 = 콜라이더 없음
  - `Walls` — 한양도성 성곽: 폴리라인을 두께 2.5m 로 세운 벽(기초 −1.5 ~ +4.5m), 충돌은 위로 10m 더
  - `Stairs` — 보이는 단(충돌 없음, 단 윗면이 경사선 위아래로 반 단씩) + 자식 `경사판 충돌`(보이지 않는 BoxCollider, Ground)
  - `Landmarks/<종류>` — 전봇대(+전선 두 가닥)·가로등·표지·정류장·전단 보드·출입문(가장 가까운 건물 면에 붙임)·암문(성벽 안쪽 면)·전망 표지·전투 무대 원(반투명)·원경(말바위·북악산 = 둥근 산, 성북동 지붕·남쪽 도심 = 상자 무리, 충돌 없음). 배드민턴 네트(새벽 전용)는 꺼 둠
  - `Route/CP01~CP10` — 걷는 면에 붙인 얇은 반투명 원판 + 테두리 + 3m 표지 기둥(시작 지점과 겹치는 1번은 기둥 없음) + `트리거`(반경 × 3m, Ignore Raycast 레이어). 순서대로 켜는 런타임 코드는 아직 없음
  - `Spawn` — 시작 자리(위치·yaw = zone1.json spawn) + 바닥 화살표. (`ScaleRef_Siwoo (EditorOnly)` 키 기준 인형은 M1 리그가 붙을 때 `M1Setup` 이 지운다)
  - `Bounds` — 경계 사각형 네 변의 보이지 않는 벽(PlayerOnly)
  - `Shots` — 배치 스크린샷 자리(꺼진 Camera 로 화각 포함): `Shot_Start`(시작 지점 눈높이 1.6m) · `Shot_StartCam`(시작 지점 게임 카메라 4m·8°·FOV 45) · `Shot_Uphill`(시우네 앞 → 꼭대기 계단) · `Shot_Waryong`(공터·성벽·성곽길) · `Shot_Reveal`(계단참에서 성곽)
- 장면 루트에 `Directional Light`(Sandbox 와 같은 색·세기·부드러운 그림자, 방향만 남남동 위 48°) · `Global Volume (Film)`(Sandbox 와 같은 필름 레시피) · `Main Camera`(SMAA, 후처리, 처음 자리 = Shot_StartCam). 하늘·안개 = #E9E0CC, 선형 안개 40~380m.
- **재질**: 06 문서 10장 색표(zone1.json `colors`) 그대로 종류별 URP Lit 단색(스무스니스 0, 스펙큘러·반사 끔). 체크포인트·전투 무대 표시만 URP Unlit 반투명.
- **레이어**(07 문서 3-5, 빌더가 비어 있으면 이름을 넣음): 6 Ground(지형·길·패드·계단 경사판) / 7 Wall(건물·담·성곽·옹벽·차단물·후문 기둥) / 8 Player / 9 PlayerOnly(전봇대·가로등·표지·정류장·난간·나무 줄기·소품·경계벽) / 10 Interact / 11 CamBlock. 물리 충돌 행렬(Player×CamBlock 끔)은 아직 안 건드림.
- **지형 깎기**: 2m 격자 삼각형이 좁은 길(2.2~3m) 위로 최대 0.5m(남쪽 인도 1.2m) 솟아서, 걷는 면 둘레 2m 안 격자점을 그 면 높이 −0.12m 아래로 내린다(980점, 최대 1.59m). zone1.json 은 그대로 두고 장면에서만 깎는다.
- **자기 점검**(로그 `[Zone1Builder]`): 블록·기둥 바닥이 땅보다 뜨면 아래로 늘리고 기록 / 체크포인트·시작 지점 발밑 높이 / 길·계단을 시우 캡슐(반지름 0.25·키 1.74)로 0.5m마다 훑어 건물·담에 걸리는지 / 시우 크기 NavMesh 를 임시로 구워(저장 안 함) 체크포인트 1→10 이 이어지는지 — '길만'(지형은 걸을 수 없음) · '맨땅 포함' 두 판.
  2026-10-05 결과: 떠 있던 블록 보정 7개(최대 1.05m, 주차장 난간·동쪽 옹벽 등 낮은 땅 쪽) · 묻힌 블록 0 · 기둥 바닥 보정 6개(최대 0.87m, 깎은 길가에 선 골목 전봇대 등) · 체크포인트 높이차 최대 0.07m · 길 가운데선 막힘 **0** · 가장자리만 닿음 11곳(인도 연석 쪽 전봇대·정류장, 주차장 난간 0.1m) · NavMesh 9구간 **모두 완주**(길만 207.9m / 맨땅 포함 200.1m; 06 문서 그래프 거리 229.7m 보다 짧은 건 넓은 면을 대각선으로 가로질러서).
- **이름표(TextMeshPro) 는 지금 없다**: 한글이 든 TMP 글꼴 에셋이 프로젝트에 없어서다(시스템 글꼴로 TMP 글꼴을 만들지 않기로 함). 한글 TMP 글꼴 에셋을 Assets 아래에 넣고 다시 실행하면 랜드마크·체크포인트에 이름표가 자동으로 붙는다.
- **알려진 문제(정적 촬영 자리 한정)**: 시작 지점이 닫힌 후문 차단 펜스(높이 2.5m) 바로 앞 2m 라서, 빌더가 계산한 정적 자리 `Shot_StartCam`(뒤 4m·피치 8°)은 화면 아래 절반이 펜스에 가린다. **실제 게임 카메라는 이 문제가 없다** — 실행 중에는 Deoccluder 가 카메라를 펜스 앞(인물에서 약 2.2m)으로 당기고, 그래도 남는 경우를 `CamClearance`(아래 M1 절)가 잡는다(게임 화면 촬영 `m1_game_1_start.png`). spawn 위치를 옮길지는 여전히 06·07 결정 사항.

### M1 플레이어·카메라 (`M1Setup` 이 Zone1 에 붙이는 리그, 2026-10-05)
설계 = `docs/07_M1_조작_설계.md`. 숫자는 `Settings/MoveTuning.asset`·`CamTuning.asset`(없을 때만 만들어서 손으로 고친 값이 남음).
- **장면 루트**: `Player`(프리팹 `Prefabs/Player.prefab` 인스턴스, spawn 위치·yaw, 발 = 그 아래 Ground 면) · `Main Camera`(Zone1Builder 것을 그대로 쓰고 `CinemachineBrain`·`DebugHud` 를 붙임) · `CM_Explore` · `Zone1/Route` 에 `RouteData`(체크포인트 위치·반경·시작 지점 — 테스트와 나중의 체크포인트 진행 코드용).
- **Player**: 태그·레이어 Player. `CharacterController`(높이 1.74·반지름 0.25·skin 0.025·중심 0.895·턱 0.30·경사 40°·minMoveDistance 0) + `PlayerMotor` + `PInput` · 자식 `Visual`(`BodyLean`) › `Siwoo_Model`(시우 정적 GLB, Y +90°, 키 1.74, `M_Siwoo_Toon`) · `CamTarget`(높이 1.40).
- **이동**(`PlayerMotor`): 걷기 1.6 / 달리기 4.5 m/s(버튼을 누르는 동안), 0→걷기 0.12초 · 걷기→달리기 0.4초 · 감속 18 m/s², 몸 회전 720°/s(달리기 540°/s), 중력 20 m/s², 땅 붙이기 0.35m, 경사에서도 수평 속도 유지, 40° 넘는 면은 미끄러져 내려옴, 맵 아래(`bounds.min.y − 10`)로 떨어지면 마지막 안전 지점으로. 점프 없음.
  공개 API: `SetMoveInput(Vector3 worldDir, float amount01, bool run)` / `SetMoveInput(Vector2 worldDirXZ, bool run = false)`(한 프레임만 유효 — 매 프레임 넣는다) · `ClearMoveInput()` · `Teleport(feet, yaw)` · `Position` `Grounded` `State`(Idle/Walk/Run/Fall) `PlanarSpeed` `Velocity` `LastSafePos` `Respawns`.
- **몸 기울기**(`BodyLean`, 애니메이션 없음): 속도·가속으로 앞뒤 −4~+8°, 도는 쪽으로 ±8°, 턱을 오를 때 비주얼 높이를 0.08초에 걸쳐 따라감, 카메라가 0.55m 안으로 오면 인물 숨김(그림자만).
- **카메라** `CM_Explore` = `CinemachineOrbitalFollow`(Sphere, 4.0m, 피치 8°·범위 −10~50°) + `CinemachineRotationComposer`(인물 화면 가로 −0.06) + `CinemachineDeoccluder`(반지름 0.10, 즉시 당김·0.5초 복귀, 얇은 PlayerOnly 는 통과) + 우리 `CamClearance`·`CamRig`·`CamInput`. FOV 45°(달리면 49°·4.3m, 실제 속도에 묶음), 자동 등 뒤 정렬은 달릴 때만(1.2초 대기·2초), Q/L1 = 0.3초에 등 뒤로. 이동 기준 = 궤도 정면(카메라 정면 아님, 07 4-4).
  - **구현에서 더한 것 — `CamClearance`(Cinemachine 확장, Deoccluder 다음 Finalize 단계)**: Deoccluder 는 '머리와 카메라 사이를 막는 벽'만 처리해서, 골목 시험에서 카메라가 옆으로 스치는 벽에 0.05m 까지 붙었다(699프레임 중 84). 그래서 머리→카메라 선을 따라 0.05m 씩 당기며 ① 카메라 구(0.095m)가 Default·Ground·Wall·CamBlock 에 안 닿고 ② 무릎(0.5m)→카메라 선이 Wall·CamBlock 에 안 막히는(1.2m 까지만) 거리를 찾는다. 당길 땐 즉시, 풀릴 땐 0.5초. 처음엔 `Orbit.Radius` 를 줄이는 방식으로 했다가 Deoccluder 의 감쇠와 엇갈려 등 뒤 벽에서 8프레임 벽 안에 들어가서 확장 방식으로 바꿨다.
- **입력**: `Input/HInput.inputactions` 의 `Explore` 맵을 `PInput`·`CamInput` 이 같은 에셋에서 읽는다(`PlayerInput` 컴포넌트·생성 C# 클래스 안 씀). 템플릿 `InputSystem_Actions` 는 그대로 프로젝트 전역.
- **조작법**

  | 동작 | 키보드·마우스 | 패드 PS (Xbox) |
  |---|---|---|
  | 이동 | W A S D (방향키도 됨) | 왼스틱 (LS) — 기울기만큼 0.5~1.6 m/s |
  | 카메라 | 마우스 | 오른스틱 (RS) |
  | 달리기(누르는 동안) | 왼쪽 Shift | R2 (RT) |
  | 카메라 등 뒤로(락온 자리) | Q 또는 마우스 가운데 버튼 | L1 (LB) |
  | 상호작용(M1 은 자리만 — 알림만 뜸) | E | × (A) |
  | 일시정지(계속·끝내기) | Esc | Options (Menu) |
  | 디버그 정보(속도·접지·카메라 거리·fps) | F1 | — |

  화면 왼쪽 아래에 조작 안내 한 줄이 늘 뜬다. 빌드는 마우스 커서를 창에 잡아 둔다(일시정지하면 풀림, 창을 다시 클릭하면 다시 잡음). 실행 인자 `-nocursorlock` 이면 잡지 않는다(자동 점검용). 시작 3초 뒤 Player.log 에 `[M1] 실행 확인: 장면 … 시우 위치 · 접지 …` 한 줄.
- **빌드 목록**: `Zone1.unity`(첫 장면) → `Sandbox.unity`. `BatchTools.BuildWindows` 는 이제 빌드 목록의 켜진 장면을 순서대로 넣는다(비어 있으면 예전처럼 Sandbox 하나).
- 물리 충돌 행렬: Player × CamBlock 끔(07 3-5, `M1Setup.EnsureTuning`).

### 자동 테스트 (PlayMode, `Assets/_Project/Tests`, 어셈블리 `Haengin.Tests`)
시간을 1/60초로 고정(`Time.captureDeltaTime`)해 결과가 매번 같다. 입력은 `PlayerMotor.SetMoveInput` 주입(InputTests 만 가짜 패드·키보드). **테스트 실행기가 든 첫 장면(InitTestScene…)을 내리면 실행이 멈춘다**(첫 시도에서 10분 넘게 멈춤 확인) → 테스트는 장면을 Additive 로 열고 자기가 만든 장면(Lab_*, Zone1)만 내린다.

| 테스트 | 내용 |
|---|---|
| `MoveTests` T01~T11 | 코드로 만든 시험장: 발 높이 · 걷기 1.6 · 달리기 4.5와 정지 시간·거리 · 180° 돌기 · 경사 30/35° 오름·45/50° 막힘 · 턱 0.15/0.30 오름·0.45 막힘 · 폭 2.4m 골목에서 카메라를 두 바퀴 돌리고 등을 벽에 붙여도 벽 안·가림 0 · 구도(뷰포트 0.44, 0.50)와 FOV 45↔49 · 6초 달리기 방향 흐름 없음 · 낭떠러지 낙하→리스폰 · 턱 오를 때 비주얼 튐 ≤ 0.04m |
| `ZoneTests` T14 / T15 | **1구역 자동 걷기 / 달리기**: Zone1 을 열고 NavMesh 를 임시로 구워(반지름 0.35, 맨땅 비용 4, 저장 안 함) 체크포인트 1→10 의 아홉 구간을 경로 모서리를 따라 `SetMoveInput` 으로 간다. 구간마다 제한 시간(1.5 × 경로 ÷ 속도 + 4초) 안 도착(반경 안, 최대 2.5m) · 발이 지면 −1m 아래로 안 떨어짐 · 3초 동안 0.2m 미만 이동이면 '끼임' 실패 · 리스폰 0. 10프레임마다 카메라가 벽·땅 안에 들어갔는지·인물이 가려졌는지 센다. 로그 `[M1Test] Zone1 걷기 완주 …` |
| `ZoneTests` Z_GameShots | `-m1shots <폴더>` 를 줄 때만(그래픽 필요, `-nographics` 빼기): 실제 게임 카메라 화면 4장 `m1_game_1_start / 2_uphill / 3_waryong / 4_run.png`(1920×1080, 2배 슈퍼샘플) |
| `InputTests` T16 | 가짜 패드 왼스틱·R2·오른스틱, 키보드 W·Shift → HInput → PInput·CamInput → 모터·카메라 |

2026-10-05 결과: **15/15 통과**(게임 화면 촬영 포함). 1구역 걷기 9/9 구간 게임 시간 **122.5초**(경로 221.1m), 달리기 9/9 **46.0초**(220.5m), 리스폰 0, 끼임 0, 카메라 벽 안·가림 0(걷기 703·달리기 245 검사). 실제 실행 시간은 걷기 약 2.5초·달리기 1초(프레임 시간 고정이라 빠름), Unity 띄우기·컴파일 포함 한 번에 약 35초.

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
| Sandbox 다시 만들기 | `Run-Unity "$env:TEMP\u_setup.log" @('-nographics','-executeMethod','Haengin.EditorTools.SandboxSetup.Build')` | 재질·볼륨·장면을 같은 경로에 덮어씀. `-modelYaw 90` 으로 모델 방향 조정 |
| 스크린샷(정면) | `Run-Unity "$env:TEMP\u_shot.log" @('-executeMethod','Haengin.EditorTools.BatchTools.Screenshot','-scene','Assets/_Project/Scenes/Sandbox.unity','-out','C:\temp\front.png','-width','1920','-height','1080')` | **`-nographics` 를 빼야** GPU 로 렌더된다(창은 안 뜸). 가장 우선순위 높은 CM 카메라 자세를 메인 카메라에 옮겨 2배 슈퍼샘플로 찍음 |
| 스크린샷(3/4) | 위와 같고 `'-yaw','35'` 추가 | `-yaw` = `-pivot`(기본 `Siwoo`) 둘레로 카메라를 돈 각도. 그 밖에 `-camera <CM 이름>`, `-supersample 1~4`, `-show Siwoo_Rigged`(비활성 루트를 찍을 때만 켬), `-hide 이름` |
| Zone1 다시 만들기 | `Run-Unity "$env:TEMP\u_zone1.log" @('-nographics','-executeMethod','Haengin.EditorTools.Zone1Builder.Build')` | `Data/zone1.json` → `Scenes/Zone1.unity` + `Zone1_Mesh.asset` 를 덮어씀(재질은 같은 경로에서 값만 갱신). 로그 `[Zone1Builder] 완료: … 길 가운데선 막힘 N곳 · NavMesh 길만 완주 9/9 …` 와 점검 메모. 실패하면 exit 1. 에디터 메뉴 **Haengin › Zone1 장면 다시 만들기** 도 같다. 데이터를 바꾸려면 먼저 `python tools/zone1_gen.py && python tools/zone1_check.py` |
| Zone1 평면도(위에서) | `Run-Unity "$env:TEMP\u_z1top.log" @('-executeMethod','Haengin.EditorTools.BatchTools.Screenshot','-scene','Assets/_Project/Scenes/Zone1.unity','-out','C:\temp\z1_top.png','-width','1800','-height','1300','-topdown','-area','-274,70,-130,174')` | 직교 투영, 화면 위 = 북. 찍는 동안만 안개·그림자 끔(`-shadows` 면 그림자 넣음). `-area` 없으면 모든 렌더러 범위 |
| Zone1 장면 컷 | 위와 같고 `-topdown -area …` 대신 `'-view','Zone1/Shots/Shot_Start'`(또는 Shot_StartCam · Shot_Uphill · Shot_Waryong · Shot_Reveal), `-width 1920 -height 1080` | `-view` = 그 오브젝트의 위치·방향·화각(꺼진 Camera)으로 찍음. `-fov 도` 로 화각 덮어쓰기, `-nofog` 로 안개 끄기 |
| M1 다시 만들기 | `Run-Unity "$env:TEMP\u_m1.log" @('-nographics','-executeMethod','Haengin.EditorGame.M1Setup.Build')` | 조정값(없을 때만) + `Prefabs/Player.prefab` + Zone1 다시 만들기(리그 포함) + 빌드 목록(Zone1, Sandbox). `Zone1Builder.Build` 만 돌려도 리그가 다시 붙는다(BeforeSave 확장 지점). 리그만: `M1Setup.AddRigToZone1`. 로그 `[M1Setup]` |
| PlayMode 테스트 | 아래 '테스트 실행' | `-runTests` 에는 **`-quit` 를 붙이지 않는다**(끝나면 스스로 닫힘). 종료 코드 0 = 전부 통과, 2 = 실패 있음 |
| Windows 빌드 | `Run-Unity "$env:TEMP\u_build.log" @('-nographics','-executeMethod','Haengin.EditorTools.BatchTools.BuildWindows','-out','C:\클로드\haengin1-builds\M1')` | 빌드 목록 장면(**Zone1 첫 장면** → Sandbox), Windows 64비트, **Mono**, 개발 빌드 아님. M1 결과물 위치 = 레포 밖 `C:\클로드\haengin1-builds\M1\HaenginMainEvent.exe`(약 141MB). 결과 `<out>\HaenginMainEvent.exe`. 로그에 `[BatchTools] 빌드 결과: Succeeded | 크기 | 시간 | 에러 | 경고`. 실패하면 exit 1. 임시 폴더를 못 쓰면 시작 전에 바로 실패 처리 |

실제로 돌려 본 결과(2026-10-05): ImportCheck 컴파일 에러 0 · 임포트 에러 0, 스크린샷 정면·3/4 정상, 빌드 Succeeded(138MB, 52초, 에러 0·경고 0), 빌드한 exe 를 창 모드로 10초 띄워 살아 있음 확인.
M1(2026-10-05): 빌드 Succeeded(Zone1 + Sandbox, 139.6MB, 에러 0·경고 0). exe 를 창 모드(1280×720)·최소화·`-nocursorlock` 으로 15초 띄워 응답 중 확인, Player.log 예외 0, `[M1] 실행 확인: 장면 Zone1 · 시우 (-197.4, 30.85, 94.7) 접지 True 상태 Idle · 카메라 거리 2.16m` 확인 후 그 PID 만 종료.

**테스트 실행**(결과 XML + 로그. 게임 화면도 찍으려면 `-nographics` 를 빼고 `-m1shots <폴더>` 추가):
```powershell
$a = @('-batchmode','-projectPath',"`"$proj`"",'-runTests','-testPlatform','PlayMode',
       '-testResults',"`"$env:TEMP\m1_tests.xml`"",'-logFile',"`"$env:TEMP\m1_tests.log`"")   # -quit 없음
$p = Start-Process $unity -ArgumentList $a -PassThru -WindowStyle Hidden; $p.WaitForExit(); "exit $($p.ExitCode)"
[xml]$x = Get-Content "$env:TEMP\m1_tests.xml" -Encoding UTF8; $x.'test-run' | Select-Object total, passed, failed, skipped
```
일부만: `'-testFilter','Haengin.Tests.ZoneTests'`.

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
