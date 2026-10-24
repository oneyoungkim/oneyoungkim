param(
  [Parameter(Mandatory = $true)][string]$Names,      # 소품 이름 쉼표 목록 또는 all (specs/props_meta.json)
  [ValidateSet('hunyuan', 'hunyuan_lp', 'sam', 'tripo')][string]$Model = 'hunyuan',
  [string]$OutDir                                     # 기본 props/raw. 병렬로 돌릴 때 raw2 등 다른 폴더(끝나면 raw 로 옮김)
)
# 체육관 소품: 참조 그림 → 3D (Higgsfield CLI, 인자 배열). 입력 concept/art/env/gym/props/ref/<name>.png
# 결과 props/raw/<name>__<model>.glb, CLI JSON logs/<name>__<model>.json. 있는 결과는 건너뜀. 503 이어도 과금되니 `generate list` 확인.
$ErrorActionPreference = 'Continue'
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$refDir = Join-Path $root 'concept\art\env\gym\props\ref'
$outDir = if ($OutDir) { $OutDir } else { Join-Path $root 'concept\art\env\gym\props\raw' }
New-Item -ItemType Directory -Force $outDir | Out-Null
$logDir = Join-Path $PSScriptRoot 'logs'; New-Item -ItemType Directory -Force $logDir | Out-Null
$meta = Get-Content (Join-Path $PSScriptRoot 'specs\props_meta.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$list = if ($Names -eq 'all') { $meta.PSObject.Properties.Name } else { $Names -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ } }
foreach ($n in $list) {
  $img = Join-Path $refDir "$n.png"
  $out = Join-Path $outDir "${n}__$Model.glb"
  if (Test-Path $out) { "SKIP $n $Model"; continue }
  if (-not (Test-Path $img)) { "NOREF $n"; continue }
  $fl = [int]$meta.$n.face_limit; if ($fl -le 0) { $fl = 10000 }
  switch ($Model) {
    'hunyuan'    { $a = @('generate','create','hunyuan3d_v3_image_to_3d','--image-references',$img,'--generate_type','Normal') }
    'hunyuan_lp' { $a = @('generate','create','hunyuan3d_v3_image_to_3d','--image-references',$img,'--generate_type','LowPoly') }
    'sam'        { $a = @('generate','create','sam_3_3d','--image-references',$img,'--prompt',($n -replace '_',' '),'--export_textured_glb','true') }
    'tripo'      { $a = @('generate','create','tripo_h3_1_image_to_3d','--image-references',$img,'--face_limit',"$fl",'--pbr','false','--texture','true') }
  }
  $a += @('--wait', '--wait-timeout', '30m', '--json')
  $r = & node $cli @a 2>&1 | Out-String
  $r | Set-Content -Encoding utf8 (Join-Path $logDir "${n}__$Model.json")
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) {
    Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $out
    "DONE $n $Model $((Get-Item $out).Length)"
  } else {
    $flat = ($r -replace "`r?`n", ' ')
    "FAIL $n $Model :: " + $flat.Substring(0, [Math]::Min(400, $flat.Length))
  }
}
"ALL_DONE"
