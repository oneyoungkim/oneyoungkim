# 냉장고 redo: fixed crops -> Tripo detailed -> reorient -> commit/push -> rig from the commit-pinned raw URL (no CDN staleness).
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$repo = 'C:\클로드\haengin1-repo'; $art = "$repo\haengin1\concept\art"
$sp = 'C:\Users\incbc\AppData\Local\Temp\claude\C-----\bd7f26b1-414c-4215-8ba8-632473d060dc\scratchpad'
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$n = 'naengjanggo'; $h = 1.84
$tri = "$art\3d\${n}_tripo.glb"; $front = "$art\3d\${n}_front.glb"; $rig = "$art\3d\anim\${n}_idle243.glb"
if (-not (Test-Path $tri)) {
  $imgs = @(); foreach ($v in 'front','left','back','right') { $imgs += @('--image-references', "$art\3d_input\${n}_$v.png") }
  $a = @('generate','create','tripo_h3_1_multiview_to_3d') + $imgs + @('--geometry_quality','detailed','--texture_quality','detailed','--pbr','false','--face_limit','80000','--wait','--wait-timeout','25m','--json')
  $r = & node $cli @a 2>&1 | Out-String
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+\.glb)"')
  if (-not $m.Success) { "FAIL tripo :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(400, $r.Length)); exit }
  Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $tri; "TRIPO $((Get-Item $tri).Length)"
}
& $blender -b --python "$sp\reorient.py" -- $tri $front $h 2>&1 | Select-String 'DIMS|EXPORTED|Error' | ForEach-Object { "REORIENT $($_.Line)" }
Push-Location $repo
$paths = @("haengin1/concept/art/3d_input/${n}_*.png", "haengin1/concept/art/3d/${n}_tripo.glb", "haengin1/concept/art/3d/${n}_front.glb", "haengin1/concept/art/3d/anim/kkanjok_idle243.glb", "haengin1/concept/art/3d/anim/seokdal_idle243.glb", "haengin1/concept/art/3d/anim/scrum_idle243.glb", "haengin1/concept/art/3d/anim/combat/siwoo_*.glb", "haengin1/concept/art/3d/anim/combat/taeo_*.glb")
git add -- $paths 2>&1 | Out-Null
git commit -q -m "Enemy naengjanggo: re-crop A-pose views with arms intact and rebuild the Tripo model; rigged idles for the other enemies and extra M2 combat clips`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -- $paths 2>&1 | Out-Null
git push -q origin claude/festive-davinci-97qbrd 2>&1 | Out-Null
$sha = (git rev-parse HEAD).Trim()
Pop-Location
"PUSHED $sha"
$url = "https://raw.githubusercontent.com/oneyoungkim/oneyoungkim/$sha/haengin1/concept/art/3d/${n}_front.glb"
$r = & node $cli generate create 3d_rigging --model_url $url --height_meters $h --enable_animation true --animation_action_id 243 --wait --wait-timeout 25m --json 2>&1 | Out-String
$m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $rig; "RIG $((Get-Item $rig).Length)" }
else { "FAIL rig :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(400, $r.Length)) }
"BALANCE " + (& node $cli account status 2>&1 | Out-String).Trim()
"ALL_DONE"
