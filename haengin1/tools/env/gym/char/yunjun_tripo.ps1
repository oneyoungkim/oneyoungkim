# 이윤준 4방향(concept/art/3d/m3/input/yunjun_{front,left,back,right}.png) → Tripo 멀티뷰 3D detailed (18cr).
# 결과 concept/art/3d/m3/yunjun_tripo.glb (정면 +X). 그다음 tools/pipeline/reorient.py 로 +Z 로 돌려 yunjun_front.glb,
# 커밋·푸시한 뒤 SHA 고정 raw 주소로 yunjun_rig.ps1(3d_rigging + 동작 243). tools/pipeline/gen_3d.ps1 과 같은 호출.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$in = Join-Path $root 'concept\art\3d\m3\input'
$out = Join-Path $root 'concept\art\3d\m3\yunjun_tripo.glb'
if (Test-Path $out) { "SKIP yunjun_tripo"; exit }
$imgs = @(); foreach ($v in 'front','left','back','right') { $imgs += @('--image-references', (Join-Path $in "yunjun_$v.png")) }
$a = @('generate','create','tripo_h3_1_multiview_to_3d') + $imgs + @('--geometry_quality','detailed','--texture_quality','detailed','--pbr','false','--face_limit','80000','--orientation','align_image','--wait','--wait-timeout','25m','--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot '..\logs\yunjun_tripo.json')
$m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
$flat = ($r -replace "`r?`n", ' ')
if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $out; "DONE yunjun_tripo $((Get-Item $out).Length)" }
else { "FAIL yunjun_tripo :: " + $flat.Substring(0, [Math]::Min(600, $flat.Length)) }
