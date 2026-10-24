[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile='LocalDb',[switch]$IncludeHttps)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$run=[Guid]::NewGuid().ToString('N')
$directory=".local/dashboard-$run"
$previous=$env:AZURELAB_INTEGRATION_CONFIG
$previousCosmos=$env:AZURELAB_COSMOS
$previousBroker=$env:AZURELAB_LIVE_BROKER
Push-Location $root
try {
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_dashboard_$run"
    & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- dashboard-local $directory
    if($LASTEXITCODE -ne 0){throw 'Owned Cosmos initialization failed; data retained.'}
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
    $env:AZURELAB_COSMOS='1'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName!~LiveDashboardTests&FullyQualifiedName!~BrokerDashboardTests' --logger "trx;LogFileName=AZ-007-$run.trx"
    if($LASTEXITCODE -ne 0){throw 'Real SQL/Cosmos checks failed; retain the profile, database and document data.'}
    if($IncludeHttps) {
        $health=Invoke-WebRequest 'http://localhost:5301/health' -UseBasicParsing -TimeoutSec 5
        if($health.StatusCode -ne 200){throw 'Existing broker is unavailable; no broker will be recreated.'}
        $directory=".local/dashboardlive-$run"
        & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_dashboardlive_$run"
        & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- dashboard-local $directory
        if($LASTEXITCODE -ne 0){throw 'Live isolated Cosmos profile failed.'}
        $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
        $broker=[System.Data.Common.DbConnectionStringBuilder]::new()
        $broker['Endpoint']='sb://localhost:5673'
        $broker['SharedAccessKeyName']='RootManageSharedAccessKey'
        $broker['SharedAccessKey']='SAS_KEY_VALUE'
        $broker['UseDevelopmentEmulator']=$true
        foreach($service in 'catalog','reservations','payments','notifications'){
            $path=Join-Path $root "$directory/$service.json"
            $config=Get-Content -Raw $path|ConvertFrom-Json
            $config|Add-Member -NotePropertyName Messaging -NotePropertyValue @{Enabled=$true;ConnectionString=$broker.ConnectionString} -Force
            $config|ConvertTo-Json -Depth 12|Set-Content $path -Encoding UTF8
        }
        $env:AZURELAB_LIVE_BROKER='1'
        & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~BrokerDashboardTests' --logger "trx;LogFileName=AZ-007-broker-$run.trx"
        if($LASTEXITCODE -ne 0){throw 'Actual broker/Cosmos settlement checks failed.'}
        & "$PSScriptRoot/Verify-HttpsBff.ps1" -ConfigurationDirectory $directory -IncludeDashboard
    }
    Write-Host "Dashboard verification passed. Retained SQL/private configuration at $directory; Cosmos data retained."
} finally {
    $env:AZURELAB_INTEGRATION_CONFIG=$previous
    $env:AZURELAB_COSMOS=$previousCosmos
    $env:AZURELAB_LIVE_BROKER=$previousBroker
    Pop-Location
}
