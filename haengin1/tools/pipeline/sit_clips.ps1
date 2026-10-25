# 바른 자세 앉기 후보(2026-10-25 — 반 친구가 책상에 엎드린 듯 보임, 33 Chair_Sit_Idle_M 대신): 시우 리그에 Meshy 라이브러리 앉기 1개씩(리깅 포함 8cr)
# 사용: powershell -ExecutionPolicy Bypass -File tools/pipeline/sit_clips.ps1 -Ids "32"   (여러 개는 프로세스를 나란히 띄움)
# 후보: 32 Chair_Sit_Idle_F · 307 Sitting_Answering_Questions · 364 Sit_on_Chair_Arms_Crossed · 363 Sit_Cross_Legged_on_Floor(시우네 좌식 밥상)
# raw 주소는 커밋 SHA 고정(9c96853 — siwoo_tripo_v2_front.glb 가 들어 있는 커밋). 결과 concept/art/3d/anim/m3/sit/siwoo_sit<번호>.glb
param([string]$Ids = "32")
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$out = 'C:\클로드\haengin1-repo\haengin1\concept\art\3d\anim\m3\sit'
New-Item -ItemType Directory -Force $out | Out-Null
$url = 'https://raw.githubusercontent.com/oneyoungkim/oneyoungkim/9c96853/haengin1/concept/art/3d/siwoo_tripo_v2_front.glb'
foreach ($s in $Ids.Split(',')) {
  $id = [int]$s.Trim()
  $file = "$out\siwoo_sit$id.glb"
  if (Test-Path $file) { "SKIP $id"; continue }
  $r = & node $cli generate create 3d_rigging --model_url $url --height_meters 1.74 --enable_animation true --animation_action_id $id --wait --wait-timeout 25m --json 2>&1 | Out-String
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE $id $((Get-Item $file).Length)" }
  else { "FAIL $id :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(600, $r.Length)) }
}
"ALL_DONE"
