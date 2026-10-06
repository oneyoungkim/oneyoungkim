# M2 extra clips: Siwoo-only moves on the Siwoo rig, heavy-body enemy moves on the Taeo rig, generic enemy moves on Siwoo.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$out = 'C:\클로드\haengin1-repo\haengin1\concept\art\3d\anim\combat'
$base = 'https://raw.githubusercontent.com/oneyoungkim/oneyoungkim/claude/festive-davinci-97qbrd/haengin1/concept/art/3d'
$S = @("$base/siwoo_tripo_v2_front.glb", 1.74, 'siwoo'); $T = @("$base/taeo_tripo_v1_front.glb", 1.83, 'taeo')
$jobs = @(
  @($S, 195, 'bighook'), @($S, 259, 'grabpush'), @($S, 31, 'breath'),
  @($T, 88, 'taunt'), @($T, 206, 'frontkick'), @($T, 512, 'tackle'), @($T, 260, 'push'),
  @($S, 510, 'charge'), @($S, 365, 'kneel'), @($S, 562, 'stumble')
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
