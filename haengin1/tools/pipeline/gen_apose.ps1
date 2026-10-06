param([string]$Name, [string]$PromptFile, [string[]]$Refs)
# Generate one GPT Image 2.5 high 4k 16:9 sheet through the Higgsfield CLI without a shell in between.
$cli = Join-Path (npm root -g) '@higgsfield/cli/bin/higgsfield.js'
$dir = 'C:\클로드\haengin1-repo\haengin1\concept\art\fujimoto'
$p = (Get-Content $PromptFile -Raw -Encoding UTF8).Trim()
$a = @('generate','create','gpt_image_2_5','--prompt',$p,'--quality','high','--resolution','4k','--aspect_ratio','16:9')
foreach ($r in $Refs) { $a += @('--image-references', (Join-Path $dir $r)) }
$a += @('--wait','--wait-timeout','15m','--json')
$r = & node $cli @a 2>&1 | Out-String
$r | Set-Content -Encoding utf8 "$PSScriptRoot\$Name.job.json"
$j = $r | ConvertFrom-Json
$u = $j[0].result_url; if (-not $u) { $u = $j.result_url }
"STATUS $($j[0].status) URL $u"
if ($u) { Invoke-WebRequest -Uri $u -OutFile (Join-Path $dir "$Name.png"); "SAVED $Name.png $((Get-Item (Join-Path $dir "$Name.png")).Length)" }
