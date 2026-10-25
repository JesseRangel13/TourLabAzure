[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile='LocalDb',[switch]$BrokerOnly,[switch]$IsolatedBroker,[switch]$AcceptSqlDeveloperLicense,[switch]$AcceptServiceBusLicense)
$ErrorActionPreference='Stop'
if($IsolatedBroker -and (!$AcceptSqlDeveloperLicense -or !$AcceptServiceBusLicense)){throw 'Isolated test broker requires both explicit license acceptance switches.'}
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
. "$PSScriptRoot/Test-BrokerProfile.ps1"
$testBroker=$null
$run=[Guid]::NewGuid().ToString('N')
$directory=".local/integration-$run"
$previous=@{}
foreach($name in 'AZURELAB_INTEGRATION_CONFIG','AZURELAB_IMAGES','AZURELAB_COSMOS','AZURELAB_RECEIPTS','AZURELAB_LIVE_BROKER'){
    $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
}
function Assert-Results([string]$Name,[int]$Minimum){
    if($LASTEXITCODE -ne 0){throw "Actual $Name regression failed; retain profiles and data."}
    [xml]$trx=Get-Content -Raw "$directory/results/$Name.trx"
    $counts=$trx.TestRun.ResultSummary.Counters
    if([int]$counts.total -lt $Minimum -or [int]$counts.notExecuted -ne 0 -or [int]$counts.failed -ne 0){throw "Incomplete $Name category: expected zero skips/failures."}
}
function Initialize-Features([string]$Output){
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $Output -Database ('tourlab_azure_'+[IO.Path]::GetFileName($Output).Replace('-','_'))
    foreach($feature in 'images-local','dashboard-local','receipts-local'){
        & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- $feature $Output
        if($LASTEXITCODE -ne 0){throw "Actual $feature initialization failed."}
    }
}
Push-Location $root
try{
    if(!$BrokerOnly){
    Initialize-Features $directory
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
    $env:AZURELAB_IMAGES='1';$env:AZURELAB_COSMOS='1';$env:AZURELAB_RECEIPTS='1';$env:AZURELAB_LIVE_BROKER=$null
    $excluded='LiveBffTests','LiveBookingTests','LiveRecoveryTests','LiveImageTests','LiveDashboardTests','LiveReceiptTests','ReceiptHostTests','BrokerIntegrationTests','BrokerRecoveryTests','BrokerDashboardTests'
    $filter=($excluded|ForEach-Object {"FullyQualifiedName!~.$_"}) -join '&'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter $filter --results-directory "$directory/results" --logger 'trx;LogFileName=sql-storage.trx'
    Assert-Results 'sql-storage' 183
    }else{
        New-Item -ItemType Directory $directory|Out-Null
        $env:AZURELAB_IMAGES='1';$env:AZURELAB_COSMOS='1';$env:AZURELAB_RECEIPTS='1'
    }
    # WebApplicationFactory regression hosts above have messaging disabled. Broker
    # probes get their own database so no staged regression Outbox can be published.
    if($IsolatedBroker){$testBroker=Start-IsolatedTestBroker $run $AcceptSqlDeveloperLicense.IsPresent $AcceptServiceBusLicense.IsPresent}
    $brokerDirectory=".local/brokerprobe-$run"
    Initialize-Features $brokerDirectory
    foreach($service in 'catalog','reservations','payments','notifications'){
        $path=Join-Path $root "$brokerDirectory/$service.json"
        $config=Get-Content -Raw $path|ConvertFrom-Json
        $config|Add-Member -NotePropertyName Messaging -NotePropertyValue @{Enabled=$true;LocalTestBroker=$IsolatedBroker.IsPresent;ConnectionString=($('Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;').Replace(':5673;',$(if($IsolatedBroker){':5674;'}else{':5673;'})))} -Force
        $config|ConvertTo-Json -Depth 12|Set-Content $path -Encoding UTF8
    }
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$brokerDirectory/provisioner.json"
    $env:AZURELAB_LIVE_BROKER='1'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~.BrokerIntegrationTests|FullyQualifiedName~.BrokerRecoveryTests|FullyQualifiedName~.BrokerDashboardTests' --results-directory "$directory/results" --logger 'trx;LogFileName=broker.trx'
    Assert-Results 'broker' 7
    Write-Host "Selected actual integration categories passed. Retained profiles: $directory and $brokerDirectory. Functions/browser checks run separately."
}finally{
    Stop-IsolatedTestBroker $testBroker
    foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Pop-Location
}
