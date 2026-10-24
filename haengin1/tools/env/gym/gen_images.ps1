param(
  [Parameter(Mandatory = $true)][string]$Spec,   # specs/<file>.json  {prefix, common, defaults, items:[{name,out,prompt,aspect,resolution,quality,background,refs}]}
  [string[]]$Only,
  [switch]$DryRun
)
# 체육관용 GPT Image 2.5 묶음 생성(tools/env_assets/gen_images.ps1 와 같고 뿌리·로그 경로만 다름). 인자 배열로 호출(셸 없음).
# 이미 있는 결과 파일은 건너뜀. CLI JSON 은 logs/<name>.json. 503 이어도 서버에서 돌고 과금되니 `generate list` 로 확인 후 재호출.
$ErrorActionPreference = 'Continue'
if ($Only) { $Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path          # haengin1/
$specPath = if (Test-Path $Spec) { $Spec } else { Join-Path $PSScriptRoot $Spec }
$s = Get-Content $specPath -Raw -Encoding UTF8 | ConvertFrom-Json
$logDir = Join-Path $PSScriptRoot 'logs'; New-Item -ItemType Directory -Force $logDir | Out-Null
$d = $s.defaults
foreach ($it in $s.items) {
  if ($Only -and ($Only -notcontains $it.name)) { continue }
  $out = Join-Path $root $it.out
  if (Test-Path $out) { "SKIP $($it.name)"; continue }
  New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
  $body = if ($it.prompt -is [array]) { $it.prompt -join ' ' } else { $it.prompt }
  $p = (@($s.prefix, $body, $s.common) | Where-Object { $_ }) -join ' '
  $q = if ($it.quality) { $it.quality } elseif ($d.quality) { $d.quality } else { 'high' }
  $res = if ($it.resolution) { $it.resolution } elseif ($d.resolution) { $d.resolution } else { '2k' }
  $ar = if ($it.aspect) { $it.aspect } elseif ($d.aspect) { $d.aspect } else { '1:1' }
  $a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--quality', $q, '--resolution', $res, '--aspect_ratio', $ar)
  $bg = if ($it.background) { $it.background } else { $d.background }
  if ($bg) { $a += @('--background', $bg) }
  $refs = @(); if ($d.refs) { $refs += $d.refs }; if ($it.refs) { $refs += $it.refs }
  foreach ($r in $refs) { $a += @('--image-references', (Join-Path $root $r)) }
  $a += @('--wait', '--wait-timeout', '20m', '--json')
  if ($DryRun) { "DRY $($it.name) :: $($p.Length) chars, $q $res $ar bg=$bg refs=$($refs -join ',')"; continue }
  $r = & node $cli @a 2>&1 | Out-String
  $r | Set-Content -Encoding utf8 (Join-Path $logDir "$($it.name).json")
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) {
    Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $out
    "DONE $($it.name) $((Get-Item $out).Length)"
  } else {
    $flat = ($r -replace "`r?`n", ' ')
    "FAIL $($it.name) :: " + $flat.Substring(0, [Math]::Min(400, $flat.Length))
  }
}
"ALL_DONE"
