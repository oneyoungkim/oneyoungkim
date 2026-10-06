# Enemy 3D pipeline: crop A-pose sheet -> Tripo multiview (detailed) -> Blender reorient to face +Z
# -> commit+push the front-facing GLB (public raw URL for the rigger) -> Higgsfield 3D Rigging with a calm idle.
param([string[]]$Names = @('kkanjok','seokdal','naengjanggo','scrum'))
$ErrorActionPreference = 'Continue'
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$repo = 'C:\클로드\haengin1-repo'
$art = "$repo\haengin1\concept\art"
$sp = 'C:\Users\incbc\AppData\Local\Temp\claude\C-----\bd7f26b1-414c-4215-8ba8-632473d060dc\scratchpad'
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$heights = @{ kkanjok = 1.72; seokdal = 1.77; naengjanggo = 1.84; scrum = 1.88 }
$raw = 'https://raw.githubusercontent.com/oneyoungkim/oneyoungkim/claude/festive-davinci-97qbrd/haengin1/concept/art/3d'
foreach ($n in $Names) {
  $h = $heights[$n]
  $sheet = "$art\fujimoto\enemy\${n}_apose.png"
  if (-not (Test-Path $sheet)) { "NOSHEET $n"; continue }
  $tri = "$art\3d\${n}_tripo.glb"; $front = "$art\3d\${n}_front.glb"; $rig = "$art\3d\anim\${n}_idle243.glb"
  if (-not (Test-Path "$art\3d_input\${n}_front.png")) {
    & python "$sp\hf\crop4.py" $sheet "$art\3d_input\$n" 2>&1 | Select-Object -Last 1 | ForEach-Object { "CROP $n $_" }
  }
  if (-not (Test-Path $tri)) {
    $imgs = @(); foreach ($v in 'front','left','back','right') { $imgs += @('--image-references', "$art\3d_input\${n}_$v.png") }
    $a = @('generate','create','tripo_h3_1_multiview_to_3d') + $imgs + @('--geometry_quality','detailed','--texture_quality','detailed','--pbr','false','--face_limit','80000','--wait','--wait-timeout','25m','--json')
    $r = & node $cli @a 2>&1 | Out-String
    $m = [regex]::Match($r, '"result_url":\s*"([^"]+\.glb)"')
    if (-not $m.Success) { "FAIL tripo $n :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(300, $r.Length)); continue }
    Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $tri; "TRIPO $n $((Get-Item $tri).Length)"
  }
  if (-not (Test-Path $front)) {
    & $blender -b --python "$sp\reorient.py" -- $tri $front $h 2>&1 | Select-String 'DIMS|EXPORTED|Error' | ForEach-Object { "REORIENT $n $($_.Line)" }
  }
  Push-Location $repo
  git add "haengin1/concept/art/fujimoto/enemy/${n}_color.png" "haengin1/concept/art/fujimoto/enemy/${n}_apose.png" "haengin1/concept/art/3d_input/${n}_*.png" "haengin1/concept/art/3d/${n}_tripo.glb" "haengin1/concept/art/3d/${n}_front.glb" 2>&1 | Out-Null
  git commit -q -m "Enemy ${n}: concept art, A-pose sheet and front-facing Tripo model`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" 2>&1 | Out-Null
  git push -q origin claude/festive-davinci-97qbrd 2>&1 | Out-Null
  Pop-Location
  "PUSHED $n"
  if (-not (Test-Path $rig)) {
    $r = & node $cli generate create 3d_rigging --model_url "$raw/${n}_front.glb" --height_meters $h --enable_animation true --animation_action_id 243 --wait --wait-timeout 25m --json 2>&1 | Out-String
    $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
    if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $rig; "RIG $n $((Get-Item $rig).Length)" }
    else { "FAIL rig $n :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(300, $r.Length)) }
  }
}
"BALANCE " + (& node $cli account status 2>&1 | Out-String).Trim()
"ALL_DONE"
