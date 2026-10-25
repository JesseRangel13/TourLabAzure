[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$isWin=$IsWindows -or ($env:OS -like '*Windows*')
$platform=if($isWin){'win-x64'}else{'linux-x64'}
$folder=Join-Path $root ".local/tools/functions-4.16.0-$platform"
$binary=if($isWin){'func.exe'}else{'func'}
# Preserve and reuse the previously verified Windows installation.
$old=Join-Path $root '.local/tools/functions-4.16.0/func.exe'
if($isWin -and (Test-Path $old)){& $old --version;if($LASTEXITCODE -ne 0){throw 'Functions runtime failed'};return}
if(Test-Path (Join-Path $folder $binary)){& "$folder/$binary" --version;if($LASTEXITCODE -ne 0){throw 'Functions runtime failed'};return}
$release=Invoke-RestMethod 'https://api.github.com/repos/Azure/azure-functions-core-tools/releases/tags/4.16.0'
$asset=$release.assets|Where-Object name -eq "Azure.Functions.Cli.$platform.4.16.0.zip"
if(!$asset){throw 'Pinned official Functions asset unavailable.'}
New-Item -ItemType Directory -Force $folder|Out-Null
$zip=Join-Path $root ".local/tools/functions-4.16.0-$platform.zip"
Invoke-WebRequest $asset.browser_download_url -OutFile $zip -UseBasicParsing
$expected=if($isWin){'5447017693611FE507EDCE98BB1D5EBC132E179297E67A49AD23F26B95B87DD1'}else{'C68C7780D94C7583157F7A7D1A05E0EC3E34057C3D55E4C7BE71FC4AA0C23667'}
if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expected){throw 'Pinned Functions archive integrity check failed.'}
if($isWin){Expand-Archive $zip $folder -Force}else{
    & unzip -q -o $zip -d $folder
    if($LASTEXITCODE -ne 0){throw 'Functions extraction failed'}
    & chmod +x "$folder/func"
    if($LASTEXITCODE -ne 0){throw 'Functions permissions failed'}
}
& "$folder/$binary" --version
if($LASTEXITCODE -ne 0){throw 'Pinned Functions executable failed'}
