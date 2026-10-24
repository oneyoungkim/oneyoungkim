param(
  [Parameter(Mandatory = $true)][string]$Name,                           # gojun 등 (char/<Name>_color.txt, <Name>_apose.txt)
  [Parameter(Mandatory = $true)][ValidateSet('color', 'apose', 'tripo', 'rig')][string]$Step,
  [string]$Refs,                                                         # color: 참조 사진 쉼표 목록(선택)
  [ValidateSet('standard', 'detailed')][string]$Quality = 'standard',    # tripo: standard 9cr / detailed 18cr
  [string]$ModelUrl, [double]$Height = 1.75, [int]$Action = 243          # rig: SHA 고정 raw 주소, 키, 동작 번호(8cr)
)
# 캐릭터 파이프라인 범용판(tools/pipeline/README.md 순서). color 2.75 → apose 4.25 → (crop4.py) → tripo → (reorient.py, 커밋) → rig.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$art = Join-Path $root 'concept\art\fujimoto\m3'
$m3 = Join-Path $root 'concept\art\3d\m3'
$log = Join-Path $PSScriptRoot "..\logs\${Name}_$Step.json"
switch ($Step) {
  'color' {
    $file = Join-Path $art "${Name}_color.png"
    $p = (Get-Content (Join-Path $PSScriptRoot "${Name}_color.txt") -Raw -Encoding UTF8).Trim()
    $a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--quality', 'high', '--resolution', '2k', '--aspect_ratio', '2:3')
    foreach ($r in ($Refs -split ',')) { if ($r.Trim()) { $a += @('--image-references', $r.Trim()) } }
  }
  'apose' {
    $file = Join-Path $art "${Name}_apose.png"
    $p = (Get-Content (Join-Path $PSScriptRoot "${Name}_apose.txt") -Raw -Encoding UTF8).Trim()
    $a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--image-references', (Join-Path $art "${Name}_color.png"), '--quality', 'high', '--resolution', '4k', '--aspect_ratio', '16:9')
  }
  'tripo' {
    $file = Join-Path $m3 "${Name}_tripo.glb"
    $imgs = @(); foreach ($v in 'front', 'left', 'back', 'right') { $imgs += @('--image-references', (Join-Path $m3 "input\${Name}_$v.png")) }
    $a = @('generate', 'create', 'tripo_h3_1_multiview_to_3d') + $imgs + @('--geometry_quality', $Quality, '--texture_quality', $Quality, '--pbr', 'false', '--face_limit', '80000', '--orientation', 'align_image')
  }
  'rig' {
    $file = Join-Path $m3 ("${Name}_idle{0}.glb" -f $Action)
    $a = @('generate', 'create', '3d_rigging', '--model_url', $ModelUrl, '--height_meters', "$Height", '--enable_animation', 'true', '--animation_action_id', "$Action")
  }
}
if (Test-Path $file) { "SKIP $Name $Step"; exit }
$a += @('--wait', '--wait-timeout', '25m', '--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 $log
$m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
$flat = ($r -replace "`r?`n", ' ')
if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE $Name $Step $((Get-Item $file).Length)" }
else { "FAIL $Name $Step :: " + $flat.Substring(0, [Math]::Min(600, $flat.Length)) }
