# 이윤준 A포즈 4방향 시트 — GPT Image 2.5 high 4k 16:9 (4.25cr), 색 설정화를 참조로. tools/pipeline/enemy_apose.ps1 과 같은 식.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$dir = Join-Path $root 'concept\art\fujimoto\m3'
$file = Join-Path $dir 'yunjun_apose.png'
if (Test-Path $file) { "SKIP yunjun_apose"; exit }
$p = (Get-Content (Join-Path $PSScriptRoot 'yunjun_apose.txt') -Raw -Encoding UTF8).Trim()
$a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--image-references', (Join-Path $dir 'yunjun_color.png'), '--quality', 'high', '--resolution', '4k', '--aspect_ratio', '16:9', '--wait', '--wait-timeout', '15m', '--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot '..\logs\yunjun_apose.json')
$m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
$flat = ($r -replace "`r?`n", ' ')
if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE yunjun_apose $((Get-Item $file).Length)" }
else { "FAIL yunjun_apose :: " + $flat.Substring(0, [Math]::Min(600, $flat.Length)) }
