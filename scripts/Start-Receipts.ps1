[CmdletBinding()]
param([string]$ConfigurationDirectory='.local/sql',[int]$Port=9450)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$privateRoot=[IO.Path]::GetFullPath((Join-Path $root $ConfigurationDirectory))
if(!$privateRoot.StartsWith((Join-Path $root '.local')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Only private Azure lab configuration permitted.'}
$func=Join-Path $root '.local/tools/functions-4.16.0/func.exe'
if(!(Test-Path $func)){
    $platform=if($env:OS -like '*Windows*'){'win-x64/func.exe'}else{'linux-x64/func'}
    $func=Join-Path $root ".local/tools/functions-4.16.0-$platform"
}
if(!(Test-Path $func)){throw 'Install pinned Functions Core Tools via Install-Functions.ps1 first.'}
$settings=Get-Content -Raw (Join-Path $privateRoot 'receipts-host.json')|ConvertFrom-Json
foreach($property in $settings.PSObject.Properties){[Environment]::SetEnvironmentVariable($property.Name,$property.Value,'Process')}
$env:AZURELAB_RECEIPTS_CONFIG=Join-Path $privateRoot 'receipts.json'
# Core Tools/host must see settings before worker startup; business JSON alone is insufficient.
Push-Location (Join-Path $root 'src/Receipts.Functions/bin/Release/net10.0')
try {
    & $func start --no-build --address 127.0.0.1 --port $Port --enable-json-output
    if($LASTEXITCODE -ne 0){throw 'Functions host failed; inspect private logs.'}
} finally {Pop-Location}
