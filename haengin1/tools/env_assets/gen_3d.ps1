param(
  [Parameter(Mandatory = $true)][string]$Names,      # comma list of prop names (specs/props_meta.json)
  [ValidateSet('tripo', 'tripo_d', 'hunyuan', 'hunyuan_lp', 'meshy', 'meshy_lp', 'sam')][string]$Model = 'tripo',
  [string]$OutDir                                     # default: <scratchpad-free> concept/art/env/props/raw
)
# Image -> 3D through the Higgsfield CLI (argument array, no shell). Input = concept/art/env/props/ref/<name>.png.
# Raw GLB -> <OutDir>/<name>__<model>.glb, CLI JSON -> logs/<name>__<model>.json. Skips existing outputs.
# A 503 still runs and bills on the server: check `generate list` before re-running.
$ErrorActionPreference = 'Continue'
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $OutDir) { $OutDir = Join-Path $root 'concept\art\env\props\raw' }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$logDir = Join-Path $PSScriptRoot 'logs'; New-Item -ItemType Directory -Force $logDir | Out-Null
$meta = Get-Content (Join-Path $PSScriptRoot 'specs\props_meta.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($n in ($Names -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
  $img = Join-Path $root "concept\art\env\props\ref\$n.png"
  $out = Join-Path $OutDir "${n}__$Model.glb"
  if (Test-Path $out) { "SKIP $n $Model"; continue }
  if (-not (Test-Path $img)) { "NOREF $n"; continue }
  $fl = [int]$meta.$n.face_limit; if ($fl -le 0) { $fl = 10000 }
  switch ($Model) {
    'tripo'      { $a = @('generate','create','tripo_h3_1_image_to_3d','--image-references',$img,'--face_limit',"$fl",'--pbr','false','--texture','true') }
    'tripo_d'    { $a = @('generate','create','tripo_h3_1_image_to_3d','--image-references',$img,'--face_limit',"$fl",'--pbr','false','--texture','true','--geometry_quality','detailed','--texture_quality','detailed') }
    'hunyuan'    { $a = @('generate','create','hunyuan3d_v3_image_to_3d','--image-references',$img,'--generate_type','Normal') }
    'hunyuan_lp' { $a = @('generate','create','hunyuan3d_v3_image_to_3d','--image-references',$img,'--generate_type','LowPoly') }
    'meshy'      { $a = @('generate','create','meshy_v7_image_to_3d','--image-references',$img,'--target_polycount',"$fl",'--should_texture','true','--enable_pbr','false') }
    'sam'        { $a = @('generate','create','sam_3_3d','--image-references',$img,'--prompt',($n -replace '_',' '),'--export_textured_glb','true') }
    'meshy_lp'   { $a = @('generate','create','meshy_v7_image_to_3d','--image-references',$img,'--model_type','lowpoly','--should_texture','true','--enable_pbr','false') }
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
