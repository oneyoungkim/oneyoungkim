param(
  [Parameter(Mandatory = $true)][string]$Spec,   # specs/<file>.json  {common, suffix, defaults, items:[{name,out,prompt,aspect,resolution,quality,background,refs}]}
  [string[]]$Only,                              # run only these item names
  [switch]$DryRun                               # print the argument list, create nothing
)
# GPT Image 2.5 batch through the Higgsfield CLI, called with an argument array (no shell, so prompts never split).
# Skips items whose output file already exists. Writes the raw CLI JSON to logs/<name>.json.
# A 503 still runs and bills on the server: check `generate list` before re-running a failed item.
$ErrorActionPreference = 'Continue'
if ($Only) { $Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }  # -File passes 'a,b' as one string
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path          # haengin1/
$specPath = if (Test-Path $Spec) { $Spec } else { Join-Path $PSScriptRoot $Spec }
$s = Get-Content $specPath -Raw -Encoding UTF8 | ConvertFrom-Json
$logDir = Join-Path $PSScriptRoot 'logs'; New-Item -ItemType Directory -Force $logDir | Out-Null
$d = $s.defaults
foreach ($it in $s.items) {
  if ($Only -and ($Only -notcontains $it.name)) { continue }
  $out = Join-Path $root $it.out
  if (Test-Path $out) { "SKIP $($it.name)"; continue }
  New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
  $body = if ($it.prompt -is [array]) { $it.prompt -join ' ' } else { $it.prompt }
  $p = (@($s.prefix, $body, $s.common) | Where-Object { $_ }) -join ' '
  $q = if ($it.quality) { $it.quality } elseif ($d.quality) { $d.quality } else { 'high' }
  $res = if ($it.resolution) { $it.resolution } elseif ($d.resolution) { $d.resolution } else { '2k' }
  $ar = if ($it.aspect) { $it.aspect } elseif ($d.aspect) { $d.aspect } else { '1:1' }
  $a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--quality', $q, '--resolution', $res, '--aspect_ratio', $ar)
  $bg = if ($it.background) { $it.background } else { $d.background }
  if ($bg) { $a += @('--background', $bg) }
  $refs = @(); if ($d.refs) { $refs += $d.refs }; if ($it.refs) { $refs += $it.refs }
  foreach ($r in $refs) { $a += @('--image-references', (Join-Path $root $r)) }
  $a += @('--wait', '--wait-timeout', '20m', '--json')
  if ($DryRun) { "DRY $($it.name) :: $($p.Length) chars, $q $res $ar bg=$bg refs=$($refs -join ',')"; continue }
  $r = & node $cli @a 2>&1 | Out-String
  $r | Set-Content -Encoding utf8 (Join-Path $logDir "$($it.name).json")
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) {
    Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $out
    "DONE $($it.name) $((Get-Item $out).Length)"
  } else {
    $flat = ($r -replace "`r?`n", ' ')
    "FAIL $($it.name) :: " + $flat.Substring(0, [Math]::Min(400, $flat.Length))
  }
}
"ALL_DONE"
