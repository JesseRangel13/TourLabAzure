[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$runId=[Guid]::NewGuid().ToString('N')
$directory=".local/workflow-$runId"
$previousConfig=$env:AZURELAB_INTEGRATION_CONFIG
$previousBroker=$env:AZURELAB_LIVE_BROKER
$previousWorkflow=$env:AZURELAB_LIVE_WORKFLOW
Push-Location $root
try {
    $health=Invoke-WebRequest 'http://localhost:5301/health' -UseBasicParsing -TimeoutSec 5
    if($health.StatusCode -ne 200){throw 'Local Service Bus health is unavailable; no broker result may be claimed.'}
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -OutputDirectory $directory -Database "tourlab_azure_workflow_$runId"
    $broker=[System.Data.Common.DbConnectionStringBuilder]::new()
    $broker['Endpoint']='sb://localhost:5673'
    $broker['SharedAccessKeyName']='RootManageSharedAccessKey'
    $broker['SharedAccessKey']='SAS_KEY_VALUE'
    $broker['UseDevelopmentEmulator']=$true
    foreach($service in 'catalog','reservations','payments','notifications'){
        $path=Join-Path $root "$directory/$service.json"
        $config=Get-Content $path -Raw|ConvertFrom-Json
        $config|Add-Member -NotePropertyName Messaging -NotePropertyValue @{
            Enabled=$true;ConnectionString=$broker.ConnectionString
        } -Force
        $config|ConvertTo-Json -Depth 12|Set-Content $path -Encoding UTF8
    }
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
    $env:AZURELAB_LIVE_BROKER='1'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~BrokerIntegrationTests' --logger "trx;LogFileName=AZ-004-routing-$runId.trx"
    if($LASTEXITCODE -ne 0){throw 'Actual broker routing checks failed; retain data and report.'}
    $env:AZURELAB_LIVE_WORKFLOW='1'
    & "$PSScriptRoot/Verify-HttpsBff.ps1" -ConfigurationDirectory $directory -IncludeBooking
    Write-Host "Actual local Service Bus booking verification passed. Configuration/database retained at $directory."
} finally {
    $env:AZURELAB_INTEGRATION_CONFIG=$previousConfig
    $env:AZURELAB_LIVE_BROKER=$previousBroker
    $env:AZURELAB_LIVE_WORKFLOW=$previousWorkflow
    Pop-Location
}