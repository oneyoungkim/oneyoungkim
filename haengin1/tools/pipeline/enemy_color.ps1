# Enemy color concept art (GPT Image 2.5 high 2k 2:3), one per enemy.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$dir = 'C:\클로드\haengin1-repo\haengin1\concept\art\fujimoto\enemy'
New-Item -ItemType Directory -Force $dir | Out-Null
foreach ($n in 'kkanjok','seokdal','naengjanggo','scrum') {
  $file = "$dir\${n}_color.png"
  if (Test-Path $file) { "SKIP $n"; continue }
  $p = (Get-Content "$PSScriptRoot\enemy\${n}_color.txt" -Raw -Encoding UTF8).Trim()
  $r = & node $cli generate create gpt_image_2_5 --prompt $p --quality high --resolution 2k --aspect_ratio 2:3 --wait --wait-timeout 15m --json 2>&1 | Out-String
  $m = [regex]::Match($r, '"result_url":\s*"([^"]+)"')
  if ($m.Success) { Invoke-WebRequest -Uri $m.Groups[1].Value -OutFile $file; "DONE $n $((Get-Item $file).Length)" }
  else { "FAIL $n :: " + ($r -replace "`r?`n", ' ').Substring(0, [Math]::Min(300, $r.Length)) }
}
"ALL_DONE"
