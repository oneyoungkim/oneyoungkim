param([string]$Name, [string]$Prefix, [double]$Height, [string]$Orientation = 'align_image')
# Tripo H3.1 multiview (detailed) -> Higgsfield 3D Rigging, through the Higgsfield CLI without a shell.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$in = 'C:\클로드\haengin1-repo\haengin1\concept\art\3d_input'
$out = 'C:\클로드\haengin1-repo\haengin1\concept\art\3d'
$imgs = @(); foreach ($v in 'front','left','back','right') { $imgs += @('--image-references', "$in\${Prefix}_$v.png") }
$a = @('generate','create','tripo_h3_1_multiview_to_3d') + $imgs + @('--geometry_quality','detailed','--texture_quality','detailed','--pbr','false','--face_limit','80000','--orientation',$Orientation,'--wait','--wait-timeout','25m','--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 "$PSScriptRoot\$Name.tripo.json"
$j = $r | ConvertFrom-Json; $u = $j[0].result_url
"TRIPO $($j[0].status) $u"
if (-not $u) { exit 1 }
Invoke-WebRequest -Uri $u -OutFile "$out\$Name.glb"; "SAVED $Name.glb $((Get-Item "$out\$Name.glb").Length)"
$r2 = & node $cli generate create 3d_rigging --model_url $u --height_meters $Height --wait --wait-timeout 25m --json 2>&1 | Out-String
$r2 | Set-Content -Encoding utf8 "$PSScriptRoot\$Name.rig.json"
$j2 = $r2 | ConvertFrom-Json; $u2 = $j2[0].result_url
"RIG $($j2[0].status) $u2"
if ($u2) { Invoke-WebRequest -Uri $u2 -OutFile "$out\${Name}_rigged.glb"; "SAVED ${Name}_rigged.glb $((Get-Item "$out\${Name}_rigged.glb").Length)" }
