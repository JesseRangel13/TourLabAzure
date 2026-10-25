[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$folder=Join-Path $root '.local/tools/functions-4.16.0'
if(Test-Path (Join-Path $folder 'func.exe')){& "$folder/func.exe" --version;return}
$release=Invoke-RestMethod 'https://api.github.com/repos/Azure/azure-functions-core-tools/releases/tags/4.16.0'
$asset=$release.assets|Where-Object name -eq 'Azure.Functions.Cli.win-x64.4.16.0.zip'
if(!$asset){throw 'Pinned official Functions asset unavailable.'}
New-Item -ItemType Directory -Force $folder|Out-Null
$zip=Join-Path $root '.local/tools/functions-4.16.0.zip'
Invoke-WebRequest $asset.browser_download_url -OutFile $zip -UseBasicParsing
$expectedHash='5447017693611FE507EDCE98BB1D5EBC132E179297E67A49AD23F26B95B87DD1'
if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expectedHash){throw 'Pinned Functions archive integrity check failed.'}
Expand-Archive $zip $folder -Force
& "$folder/func.exe" --version