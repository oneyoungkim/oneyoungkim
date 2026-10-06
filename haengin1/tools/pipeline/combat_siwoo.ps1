# Siwoo combat clips (Meshy library through Higgsfield 3D Rigging, 8 credits each).
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$out = 'C:\클로드\haengin1-repo\haengin1\concept\art\3d\anim\combat'
New-Item -ItemType Directory -Force $out | Out-Null
$url = 'https://raw.githubusercontent.com/oneyoungkim/oneyoungkim/claude/festive-davinci-97qbrd/haengin1/concept/art/3d/siwoo_tripo_v2_front.glb'
$clips = [ordered]@{ 89='stance'; 21='fwalk_fwd'; 20='fwalk_back'; 191='jab'; 192='cross'; 193='hook'; 194='uppercut'; 209='kick'; 211='knee'; 138='block'; 156='dodge'; 174='hit_face'; 178='hit_body'; 187='knockdown'; 344='standup'; 403='victory' }
foreach ($kv in $clips.GetEnumerator()) {
  $id = $kv.Key; $name = $kv.Value; $file = "$out\siwoo_${name}.glb"
  if (Test-Path $file) { "SKIP $name"; continue }
  $r = & node $cli generate create 3d_rigging --model_url $url --height_meters 1.74 --enable_animation true --animation_action_id $id --wait --wait-timeout 25m --json 2>&1 | Out-String
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE $id $name $((Get-Item $file).Length)" }
  else { "FAIL $id $name :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(300, $r.Length)) }
}
"BALANCE " + (& node $cli account status 2>&1 | Out-String).Trim()
"ALL_DONE"
