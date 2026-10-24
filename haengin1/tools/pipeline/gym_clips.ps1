# 체육관 드릴 전용 동작(docs/12 4-8 동작 주문서, 2026-10-24 2차 패스 승인 24cr): 시우 리그에 Meshy 라이브러리 동작 1개씩(리깅 포함 8cr)
# 사용: powershell -ExecutionPolicy Bypass -File tools/pipeline/gym_clips.ps1 [-Ids "46,327,396"]
# raw 주소는 커밋 SHA 고정(9c96853 — siwoo_tripo_v2_front.glb 가 들어 있는 커밋). 결과 concept/art/3d/anim/gym/siwoo_<이름>.glb
param([string]$Ids = "46,327,396")
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$out = 'C:\클로드\haengin1-repo\haengin1\concept\art\3d\anim\gym'
New-Item -ItemType Directory -Force $out | Out-Null
$url = 'https://raw.githubusercontent.com/oneyoungkim/oneyoungkim/9c96853/haengin1/concept/art/3d/siwoo_tripo_v2_front.glb'
$names = @{ 46 = 'jumprope'; 327 = 'kettlebell'; 396 = 'burpee' }
foreach ($s in $Ids.Split(',')) {
  $id = [int]$s.Trim(); $name = $names[$id]; if (-not $name) { $name = "a$id" }
  $file = "$out\siwoo_${name}.glb"
  if (Test-Path $file) { "SKIP $id $name"; continue }
  $r = & node $cli generate create 3d_rigging --model_url $url --height_meters 1.74 --enable_animation true --animation_action_id $id --wait --wait-timeout 25m --json 2>&1 | Out-String
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE $id $name $((Get-Item $file).Length)" }
  else { "FAIL $id $name :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(400, $r.Length)) }
}
"BALANCE " + (& node $cli account status 2>&1 | Out-String).Trim()
"ALL_DONE"
