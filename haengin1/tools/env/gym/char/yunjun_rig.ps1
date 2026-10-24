param([Parameter(Mandatory = $true)][string]$ModelUrl, [double]$Height = 1.75, [int]$Action = 243)
# 이윤준 리깅 + 동작 1개(기본 243 Idle_3) — 8cr. ModelUrl 은 커밋 SHA 고정 raw 주소(브랜치 주소는 CDN 이 옛 파일을 줄 수 있음).
# 결과 concept/art/3d/m3/yunjun_idle243.glb
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$out = Join-Path $root ("concept\art\3d\m3\yunjun_idle{0}.glb" -f $Action)
if (Test-Path $out) { "SKIP yunjun_rig"; exit }
$a = @('generate','create','3d_rigging','--model_url',$ModelUrl,'--height_meters',"$Height",'--enable_animation','true','--animation_action_id',"$Action",'--wait','--wait-timeout','25m','--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot ("..\logs\yunjun_rig{0}.json" -f $Action))
$m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
$flat = ($r -replace "`r?`n", ' ')
if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $out; "DONE yunjun_rig $((Get-Item $out).Length)" }
else { "FAIL yunjun_rig :: " + $flat.Substring(0, [Math]::Min(600, $flat.Length)) }
