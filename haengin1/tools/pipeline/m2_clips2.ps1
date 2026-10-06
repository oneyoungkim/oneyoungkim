# M2 candidates round 2: knockdown variants, heavy overhead smash (Taeo rig), crouch side-walks as strafe.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$out = 'C:\Users\incbc\AppData\Local\Temp\claude\C-----\bd7f26b1-414c-4215-8ba8-632473d060dc\scratchpad\cand_clips'
New-Item -ItemType Directory -Force $out | Out-Null
$base = 'https://raw.githubusercontent.com/oneyoungkim/oneyoungkim/claude/festive-davinci-97qbrd/haengin1/concept/art/3d'
$S = @("$base/siwoo_tripo_v2_front.glb", 1.74, 'siwoo'); $T = @("$base/taeo_tripo_v1_front.glb", 1.83, 'taeo')
$jobs = @(
  @($S, 190, 'kd190'), @($S, 366, 'kd366'), @($S, 502, 'kd502'), @($S, 503, 'kd503'),
  @($T, 128, 'smash128'), @($S, 525, 'side525'), @($S, 526, 'side526')
)
foreach ($j in $jobs) {
  $who = $j[0]; $id = $j[1]; $name = $j[2]
  $file = "$out\$($who[2])_$name.glb"
  if (Test-Path $file) { "SKIP $name"; continue }
  $r = & node $cli generate create 3d_rigging --model_url $who[0] --height_meters $who[1] --enable_animation true --animation_action_id $id --wait --wait-timeout 25m --json 2>&1 | Out-String
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE $id $($who[2])_$name $((Get-Item $file).Length)" }
  else { "FAIL $id $name :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(300, $r.Length)) }
}
"BALANCE " + (& node $cli account status 2>&1 | Out-String).Trim()
"ALL_DONE"
