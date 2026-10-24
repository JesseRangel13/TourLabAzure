[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile='LocalDb')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$runId=[Guid]::NewGuid().ToString('N')
$directory=".local/recovery-$runId"
$previous=$env:AZURELAB_INTEGRATION_CONFIG
Push-Location $root
try {
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_recovery_$runId"
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
    & dotnet test tests/Scaffold.Tests -c Release --no-build --logger "trx;LogFileName=AZ-005-$runId.trx"
    if($LASTEXITCODE -ne 0){throw 'Recovery SQL/security checks failed; retain this database and report.'}
    Write-Host "Real SQL suite passed. Data/config retained at $directory. This does not verify the Service Bus path."
} finally { $env:AZURELAB_INTEGRATION_CONFIG=$previous;Pop-Location }