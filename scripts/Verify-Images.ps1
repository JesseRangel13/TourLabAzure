[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile='LocalDb',[switch]$IncludeHttps)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$runId=[Guid]::NewGuid().ToString('N')
$directory=".local/images-$runId"
$previous=$env:AZURELAB_INTEGRATION_CONFIG
$previousImages=$env:AZURELAB_IMAGES
Push-Location $root
try {
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_images_$runId"
    & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- images-local $directory
    if($LASTEXITCODE -ne 0){throw 'Private Azurite initialization failed; no existing storage is deleted.'}
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
    $env:AZURELAB_IMAGES='1'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter FullyQualifiedName!~LiveImageTests --logger "trx;LogFileName=AZ-006-$runId.trx"
    if($LASTEXITCODE -ne 0){throw 'SQL/Azurite image checks failed; retain all evidence and data.'}
    if($IncludeHttps){ & "$PSScriptRoot/Verify-HttpsBff.ps1" -ConfigurationDirectory $directory -IncludeImages }
    Write-Host "SQL and actual Azurite checks passed. Profile/data retained at $directory."
} finally {$env:AZURELAB_INTEGRATION_CONFIG=$previous;$env:AZURELAB_IMAGES=$previousImages;Pop-Location}
