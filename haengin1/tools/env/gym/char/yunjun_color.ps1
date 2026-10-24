param([string]$Refs)   # 참조 사진 경로 쉼표 목록(scratchpad, 커밋 안 함). 비우면 글만으로 생성
# 이윤준(혜화 MMA 관장, 실존 인물 — 대표 허락으로 공개 사진 참조) 색 설정화 — GPT Image 2.5 high 2k 2:3 (2.75cr).
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$dir = Join-Path $root 'concept\art\fujimoto\m3'
New-Item -ItemType Directory -Force $dir | Out-Null
$file = Join-Path $dir 'yunjun_color.png'
if (Test-Path $file) { "SKIP yunjun_color"; exit }
$p = (Get-Content (Join-Path $PSScriptRoot 'yunjun_color.txt') -Raw -Encoding UTF8).Trim()
$a = @('generate', 'create', 'gpt_image_2_5', '--prompt', $p, '--quality', 'high', '--resolution', '2k', '--aspect_ratio', '2:3')
foreach ($r in ($Refs -split ',')) { if ($r.Trim()) { $a += @('--image-references', $r.Trim()) } }
$a += @('--wait', '--wait-timeout', '15m', '--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot '..\logs\yunjun_color.json')
$m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE yunjun_color $((Get-Item $file).Length)" }
else { "FAIL yunjun_color :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(600, ($r -replace "`r?`n", ' ').Length)) }
