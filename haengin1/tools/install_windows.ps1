# 행인1의 메인이벤트 — Windows 개발 도구 설치 (docs/05_개발환경_설치가이드.md 2장 기준)
#
# 실행 (PowerShell, 레포의 haengin1 폴더에서):
#   powershell -ExecutionPolicy Bypass -File tools\install_windows.ps1            # 필수만
#   powershell -ExecutionPolicy Bypass -File tools\install_windows.ps1 -Optional  # 추천 도구까지
#   powershell -ExecutionPolicy Bypass -File tools\install_windows.ps1 -DryRun    # 설치 없이 할 일만 출력
# 옵션: -SkipUnity (엔진 확정 전이면 Unity Hub·에디터 건너뜀)
#       -UnityVersion 6000.3.xf1 (에디터 버전 직접 지정. 생략하면 Hub 목록에서 6000.3 LTS 최신을 고름)
# 이미 설치된 프로그램은 건너뛴다. 여러 번 실행해도 안전하다.

param(
  [switch]$Optional,
  [switch]$SkipUnity,
  [switch]$DryRun,
  [string]$UnityVersion = ''
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

function Say($msg, $color = 'Gray') { Write-Host $msg -ForegroundColor $color }
function Step($msg) { Say "`n== $msg" 'Cyan' }

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
  Say 'winget이 없습니다. Microsoft Store에서 "앱 설치 관리자(App Installer)"를 설치한 뒤 다시 실행하세요.' 'Red'
  exit 1
}

$results = [ordered]@{}

function Install-Winget($id, $name, $source = 'winget') {
  $listed = winget list --id $id --exact --accept-source-agreements --disable-interactivity 2>$null | Out-String
  if ($listed -match [regex]::Escape($id)) { Say "  [있음] $name"; $results[$name] = '이미 설치됨'; return }
  if ($DryRun) { Say "  [설치 예정] $name ($id)" 'Yellow'; $results[$name] = '설치 예정(DryRun)'; return }
  Say "  [설치] $name ($id) ..." 'Yellow'
  winget install --id $id --exact --source $source --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
  if ($LASTEXITCODE -eq 0) { $results[$name] = '설치 완료' } else { $results[$name] = "실패 (코드 $LASTEXITCODE)"; Say "  $name 설치 실패 — 나중에 수동 설치" 'Red' }
}

function Refresh-Path {
  $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}

# ---------- 필수 ----------
Step '필수 도구'
Install-Winget 'Git.Git'               'Git'
Install-Winget 'GitHub.GitLFS'         'Git LFS'
Install-Winget 'GitHub.GitHubDesktop'  'GitHub Desktop'
Install-Winget 'BlenderFoundation.Blender' 'Blender'
Install-Winget 'pixivInc.VRoidStudio'  'VRoid Studio'
if (-not $SkipUnity) { Install-Winget 'Unity.UnityHub' 'Unity Hub' }

if (-not $DryRun) {
  Refresh-Path
  if (Get-Command git -ErrorAction SilentlyContinue) {
    git lfs install | Out-Null
    if ($LASTEXITCODE -eq 0) { Say '  Git LFS 초기화 완료' } else { Say '  git lfs install 실패 — 새 창에서 다시 실행해 보세요' 'Red' }
  }
}

# ---------- Unity 에디터 6.3 LTS ----------
if (-not $SkipUnity) {
  Step 'Unity 6.3 LTS 에디터'
  $hub = @("$env:ProgramFiles\Unity Hub\Unity Hub.exe", "${env:ProgramFiles(x86)}\Unity Hub\Unity Hub.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
  $modules = @('windows-il2cpp', 'visualstudio', 'language-ko')
  if (-not $hub) {
    if ($DryRun) { Say ('  [설치 예정] Unity Hub 설치 후 6000.3 LTS 에디터 + 모듈: ' + ($modules -join ', ')) 'Yellow'; $results['Unity 에디터'] = '설치 예정(DryRun)' }
    else { Say '  Unity Hub 실행 파일을 찾지 못했습니다. Hub를 한 번 켠 뒤 이 스크립트를 다시 실행하세요.' 'Red'; $results['Unity 에디터'] = '건너뜀 (Hub 없음)' }
  } else {
    $installed = & $hub -- --headless editors --installed 2>$null | Out-String
    if ($installed -match '6000\.3\.') {
      Say '  [있음] Unity 6000.3.x'; $results['Unity 에디터'] = '이미 설치됨'
    } else {
      $ver = $UnityVersion
      if (-not $ver) {
        $releases = & $hub -- --headless editors --releases 2>$null | Out-String
        $ver = ([regex]::Matches($releases, '6000\.3\.\d+f\d+') | ForEach-Object Value | Sort-Object { [int]($_ -replace '6000\.3\.(\d+)f\d+', '$1') } | Select-Object -Last 1)
      }
      if (-not $ver) {
        Say '  Hub 목록에서 6000.3 LTS를 찾지 못했습니다. Unity Hub > 설치 > 에디터 설치에서 "6000.3 LTS"를 직접 고르고 모듈(Windows Build Support IL2CPP, Visual Studio, 한국어)을 체크하세요.' 'Red'
        $results['Unity 에디터'] = '수동 설치 필요'
      } elseif ($DryRun) {
        Say "  [설치 예정] Unity $ver + 모듈: $($modules -join ', ')" 'Yellow'; $results['Unity 에디터'] = "설치 예정 $ver (DryRun)"
      } else {
        Say "  [설치] Unity $ver (수 GB, 오래 걸립니다) ..." 'Yellow'
        $hubArgs = @('--', '--headless', 'install', '--version', $ver) + ($modules | ForEach-Object { @('--module', $_) })
        & $hub @hubArgs
        if ($LASTEXITCODE -eq 0) { $results['Unity 에디터'] = "설치 완료 $ver" }
        else { $results['Unity 에디터'] = "실패 (코드 $LASTEXITCODE) — Hub에서 수동 설치"; Say '  Unity 에디터 설치 실패 — Unity Hub 화면에서 설치하세요.' 'Red' }
      }
    }
  }
}

# ---------- 추천 (필요해지면) ----------
if ($Optional) {
  Step '추천 도구'
  Install-Winget 'KDE.Krita'          'Krita'
  Install-Winget 'Audacity.Audacity'  'Audacity'
  Install-Winget 'XPFMG5VK7FJPXL'     'Cascadeur (Microsoft Store)' 'msstore'
  Say '  PureRef는 winget에 없습니다 → https://www.pureref.com/download.php 에서 직접 받으세요.'
  $results['PureRef'] = '수동 (웹)'
}

# ---------- 웹 계정·다음 단계 ----------
Step '계정만 만들 곳 (웹)'
Say '  Unity 계정: https://id.unity.com   (Hub 로그인, 에셋스토어)'
Say '  Mixamo:     https://www.mixamo.com (Adobe 계정, 리깅·격투 애니메이션)'

Step '결과'
foreach ($k in $results.Keys) { Say ("  {0,-28} {1}" -f $k, $results[$k]) }
Say "`n다음: Unity Hub에서 새 프로젝트(Universal 3D) → Package Manager에서 Cinemachine, Input System, Timeline, ProBuilder, UniVRM, 툰 셰이더 추가 (설치가이드 2장 마지막 표)." 'Green'
