param(
  [Parameter(Mandatory = $true)][string]$Name,                                  # yunjun_v3 등 (char/<Name>_face.txt, <Name>_bust.txt)
  [Parameter(Mandatory = $true)][ValidateSet('face', 'bust', 'tripo')][string]$Step,
  [string]$Refs,                                                                # face: 참조 사진 쉼표 목록 / bust: 추가 참조(색 설정화 등)
  [ValidateSet('medium', 'high')][string]$Quality = 'medium',                  # face: medium 2k 1cr · high 2k 2.75cr
  [string]$Tag = ''                                                             # face: 두 번째 시도부터 _b, _c
)
# v3 닮음 파이프라인(2026-10-30): 정면 얼굴 설정화(1:1) → 머리+상반신 4방향 시트(4k 16:9) → (crop4.py) → Tripo detailed 머리 메시.
# 몸은 v2 리깅 메시를 그대로 두고 tools/pipeline/head_swap.py 로 머리만 바꿔 붙인다(뼈 24 그대로, 리깅 0cr).
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$art = Join-Path $root 'concept\art\fujimoto\m3'
$m3 = Join-Path $root 'concept\art\3d\m3'
$log = Join-Path $PSScriptRoot "..\logs\${Name}_$Step$Tag.json"
switch ($Step) {
  'face' {
    $file = Join-Path $art "${Name}_face$Tag.png"
    $p = (Get-Content (Join-Path $PSScriptRoot "${Name}_face$Tag.txt") -Raw -Encoding UTF8).Trim()
    $a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--quality', $Quality, '--resolution', '2k', '--aspect_ratio', '1:1')
    foreach ($r in ($Refs -split ',')) { if ($r.Trim()) { $a += @('--image-references', $r.Trim()) } }
  }
  'bust' {
    $file = Join-Path $art "${Name}_bust.png"
    $p = (Get-Content (Join-Path $PSScriptRoot "${Name}_bust.txt") -Raw -Encoding UTF8).Trim()
    $a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--quality', 'high', '--resolution', '4k', '--aspect_ratio', '16:9')
    foreach ($r in ($Refs -split ',')) { if ($r.Trim()) { $a += @('--image-references', $r.Trim()) } }
  }
  'tripo' {
    $file = Join-Path $m3 "${Name}_bust_tripo.glb"
    $imgs = @(); foreach ($v in 'front', 'left', 'back', 'right') { $imgs += @('--image-references', (Join-Path $m3 "input\${Name}_bust_$v.png")) }
    $a = @('generate', 'create', 'tripo_h3_1_multiview_to_3d') + $imgs + @('--geometry_quality', 'detailed', '--texture_quality', 'detailed', '--pbr', 'false', '--face_limit', '80000', '--orientation', 'align_image')
  }
}
if (Test-Path $file) { "SKIP $Name $Step$Tag"; exit }
$a += @('--wait', '--wait-timeout', '25m', '--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 $log
$m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
$flat = ($r -replace "`r?`n", ' ')
if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE $Name $Step$Tag $((Get-Item $file).Length)" }
else { "FAIL $Name $Step$Tag :: " + $flat.Substring(0, [Math]::Min(400, $flat.Length)) }
