# 행인1의 메인이벤트 — Unity 프로젝트

- 프로젝트 위치: `haengin1/unity/HaenginMainEvent` (레포 안, 한글 경로 `C:\클로드\...` 그대로 사용 — 아래 '주의 2' 참고)
- 에디터: **Unity 6000.3.25f1 (Unity 6.3 LTS)**, 렌더 파이프라인 **URP 17.3** (Hub의 "Universal 3D" 템플릿 17.0.14에서 시작)
- 회사명 / 제품명: `makethis1` / `행인1의 메인이벤트` (빌드 실행 파일 이름은 영문 `HaenginMainEvent.exe`)
- 입력: Input System 전용(Player Settings › Active Input Handling = Input System Package (New))
- 에셋 직렬화: Force Text, 메타 파일: Visible Meta Files (기본값 확인함)

## 1. 여는 법

1. Unity Hub › Projects › **Add** › `...\haengin1\unity\HaenginMainEvent` 폴더 선택 → 에디터 6000.3.25f1 로 연다.
2. 처음 열면 `Library/` 를 새로 만드느라 몇 분 걸린다(패키지 내려받기 포함, git 패키지는 Git 이 PATH 에 있어야 함).
3. 시험 장면: `Assets/_Project/Scenes/Sandbox.unity` (빌드 목록에 이것 하나만 들어 있음).
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
│  │  ├─ Materials/                 M_Siwoo_Toon(UTS) · M_SiwooRigged_Toon · M_Floor_Sand(URP Lit)
│  │  ├─ Scenes/                    Sandbox.unity
│  │  ├─ Scripts/                   (런타임 코드 자리, 비어 있음)
│  │  ├─ Editor/                    BatchTools.cs · SandboxSetup.cs · TempPathGuard.cs (어셈블리 Haengin.EditorTools)
│  │  └─ Settings/                  Sandbox_FilmVolume.asset (필름 후처리)
│  ├─ _Template/                    URP 템플릿 샘플(Readme, TutorialInfo, SampleScene) — 지우지 않고 격리
│  ├─ Settings/                     URP 파이프라인 에셋(PC/Mobile_RPAsset·Renderer, 전역 설정) — 템플릿 것이지만 실제 사용 중
│  └─ InputSystem_Actions.inputactions   프로젝트 전역 입력 액션(템플릿 기본)
├─ Packages/  ProjectSettings/      커밋 대상
└─ Library/ Temp/ Logs/ UserSettings/ Builds/   커밋 안 함(.gitignore)
```

`Haengin.EditorTools` 어셈블리는 게임 코드(Assembly-CSharp)에 기대지 않게 따로 떼어 두었다. 다만 배치 실행에서 스크립트 컴파일 에러가 있으면 Unity 가 `Scripts have compiler errors.` 를 남기고 `-executeMethod` 전에 종료 코드 1로 끝난다(검수에서 확인) → **컴파일 에러는 종료 코드 1과 로그의 `error CS` 줄로 판단**한다. ImportCheck 는 컴파일이 통과한 뒤의 임포트 에러를 세는 용도.

### Sandbox 장면 구성 (`SandboxSetup.Build` 가 만든 것)
- 배경 = 종이색 **#F4EFE6**(카메라 단색), 바닥 = 모래 베이지 **#E9E0CC** 300m 평면, 선형 안개 #E9E0CC 30~140m
- Directional Light: 따뜻한 흰빛, 앞-왼쪽 위(48°, 150°), 부드러운 그림자(강도 .7)
- **Siwoo**(원점): Tripo GLB 는 정면이 glTF +X → glTFast 가 X 를 뒤집어 Unity 에선 -X → **Y +90°** 로 돌려 +Z(Unity 표준 앞)를 보게 함. 키 **1.74m** 로 스케일(2026-10-05 대표 결정: 시우 174cm 61kg), 발 y=0. 모델 자체는 162cm 기준 설정화로 만든 것이라 비율이 조금 작은 사람 체형 — A포즈 재생성 때 174cm로 다시 만든다
- **Siwoo_Rigged**: x=1.6 에 **비활성**(재생성 전까지 참고용)
- Main Camera(+CinemachineBrain, SMAA, 후처리 켬) + **CM_FullBody**(FOV 30°, 거리 3.93m, 높이 0.97m, 전신이 들어옴)
- Global Volume: 채도 -15(=×.85), 대비 -6, 스플릿 톤(그림자 #2F4A5A 쪽 / 밝은 쪽 #F3D9B0 쪽), 톤매핑 없음, 블룸 끔
- 툰 재질(M_Siwoo_Toon): 기본 텍스처 그대로, 1단 그림자 = 텍스처 × (.78,.71,.74) 모브, 2단 = 1단 × (.84,.80,.80), 경계 feather .02, 스펙큘러·림 0, 외곽선 #1A1417 폭 2.6(오브젝트 공간 ×0.001, 1080p 전신 컷에서 실루엣 2~3px). 텍스처 한 장에 피부·옷·머리가 섞여 있어 부위별 곱셈값은 아직 못 나눔(부위 마스크 또는 재질 분리 필요).

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
| Windows 빌드 | `Run-Unity "$env:TEMP\u_build.log" @('-nographics','-executeMethod','Haengin.EditorTools.BatchTools.BuildWindows','-out','C:\temp\HaenginWin')` | Sandbox 하나, Windows 64비트, **Mono**, 개발 빌드 아님. 결과 `<out>\HaenginMainEvent.exe`. 로그에 `[BatchTools] 빌드 결과: Succeeded | 크기 | 시간 | 에러 | 경고`. 실패하면 exit 1. 임시 폴더를 못 쓰면 시작 전에 바로 실패 처리 |

실제로 돌려 본 결과(2026-10-05): ImportCheck 컴파일 에러 0 · 임포트 에러 0, 스크린샷 정면·3/4 정상, 빌드 Succeeded(138MB, 52초, 에러 0·경고 0), 빌드한 exe 를 창 모드로 10초 띄워 살아 있음 확인.

## 5. 주의

1. **TMPDIR (대표 PC 전용 문제)** — 사용자 환경변수 `TMPDIR = C:\Users\Public\Documents\ESTsoft\CreatorTemp`(ESTsoft 프로그램이 넣은 것)인데 이 폴더에는 새 파일을 만들 수 없다. Unity 의 Mono 는 `TMPDIR` 을 `TEMP` 보다 먼저 보고 첫 값을 계속 쓰기 때문에 `Path.GetTempFileName()` 을 쓰는 단계가 깨진다 → **빌드의 Burst AOT 단계가 실패**(`AsyncPluginsFromLinker failed ... FileNotFoundException ... CreatorTemp`), 첫 임포트 때 API Updater 예외도 났다. 배치 실행은 띄우기 전에 `$env:TMPDIR=$env:TEMP` 로 해결. **Hub 로 연 에디터에서 빌드할 때도 같은 문제가 생긴다** → 근본 해결은 사용자 환경변수 TMPDIR 을 지우거나 `%TEMP%` 로 바꾸는 것(대표 결정, ESTsoft 프로그램 쪽 영향 확인 필요). 에디터 안에서는 `TempPathGuard` 가 문제를 찾아 경고만 한다(프로세스 안에서 바꿔도 소용없음을 확인함).
2. **한글 경로** — 프로젝트를 `C:\클로드\...` 에 둔 채로 패키지 해결(git 패키지 포함)·컴파일·GLB 임포트·셰이더 컴파일·GPU 스크린샷·Windows(Mono) 빌드가 모두 정상이었다. 그래서 `C:\dev\...` 로 옮기지 않았다. 다만 **IL2CPP 빌드는 시험하지 않았다** — IL2CPP 로 바꿀 때 경로 문제가 나면 프로젝트를 `C:\dev\haengin1-unity\HaenginMainEvent` 로 옮기고 이 문서에 위치를 적는다. 빌드 출력 폴더는 영문 경로를 권장.
   **진짜 제약은 경로 길이다.** 프로젝트 경로가 53자이고 Library 안 가장 긴 상대 경로가 202자라 합쳐 255자 — Windows 한계 259자까지 4자 남는다. 검수에서 같은 프로젝트를 120자 깊이에 복제해 열자 DirectoryNotFoundException 57건·임포트 에러 21건이 났다(53자 한글 경로에서는 0건). → **이 프로젝트 폴더의 전체 경로는 53자 이하로 둔다. 레포를 GitHub Desktop 기본 위치(Documents\GitHub\...)나 worktree 같은 더 깊은 폴더에 클론하지 말 것.** 꼭 필요하면 `C:\dev\h1` 같은 짧은 경로로 옮긴다. Hub 에서 Add 할 때도 같다.
3. **Git LFS** — `HaenginMainEvent/.gitattributes` 가 glb·fbx·png·jpg·psd·tga·exr·wav·mp3·ogg·vrm·blend 등을 LFS 로, `*.unity *.prefab *.asset *.mat *.meta` 등 Unity YAML 은 LF 텍스트(+unityyamlmerge 병합 드라이버 이름)로 지정한다. 커밋 전에 한 번 `git lfs install` 필요. 이 `.gitattributes` 는 이 폴더 아래에만 적용되며, 레포의 `concept/art/3d/*.glb` 원본은 LFS 가 아니다. 시우 GLB 2개(2.4MB·3.4MB)는 Assets 쪽에 복사본이 있다.
4. `.gitignore` 로 `Library/ Temp/ Obj/ Logs/ UserSettings/ Builds/ *.csproj *.sln` 등을 뺀다. 빈 폴더 `Scripts/` 는 `.gitkeep` 로 유지(Unity 는 점으로 시작하는 파일을 무시).
   빌드할 때 `com.unity.collections` 가 끌어온 `test-framework.performance` 가 `Assets/Resources/` 에 json 을 잠깐 만들고 지워서 **빈 `Assets/Resources`(+.meta)** 가 남을 수 있다. `BuildWindows` 는 원래 없던 빈 폴더면 지운다. 에디터에서 빌드한 뒤 빈 폴더가 남았으면 커밋하지 말고 지울 것.
5. **다른 플랫폼을 추가하면** UniGLTF 기본 임포터를 끄는 define 두 개(`UNIGLTF_DISABLE_DEFAULT_GLB_IMPORTER`, `UNIGLTF_DISABLE_DEFAULT_GLTF_IMPORTER`)를 그 플랫폼의 Scripting Define Symbols 에도 넣는다. 지금은 Standalone 에만 있어서, 빠지면 glTFast 와 .glb 임포터가 충돌한다.
6. 첫 실행한 플레이어는 어셈블리 로드에 8초 걸렸다(백신 검사로 보임). 10초 안에 장면 표시까지 확인하지는 못했고, 죽지 않고 응답 중인 것까지 확인.
7. 배치 실행마다 스크립트 컴파일 단계가 100~170초 걸린다(대부분 `IL Post Processor runner` 연결 대기). 백신(ESTsoft) 실시간 검사 영향으로 보인다 — Unity 에디터 폴더와 프로젝트 `Library` 를 검사 예외에 넣을지는 대표 결정.
