[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile='LocalDb',[switch]$IncludeHttps,[switch]$SkipRelational)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$run=[Guid]::NewGuid().ToString('N')
$previous=@{}
foreach($name in 'AZURELAB_INTEGRATION_CONFIG','AZURELAB_RECEIPTS','AZURELAB_RECEIPT_HOST'){
    $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
}
$process=$null
$directory=''
function Check([string]$Message){if($LASTEXITCODE -ne 0){throw $Message}}
function Stop-Receipts {
    if($null -eq $script:process -or $script:process.HasExited){return}
    if($PSVersionTable.PSEdition -eq 'Desktop'){
        & "$env:WINDIR/System32/taskkill.exe" /PID $script:process.Id /T /F|Out-Null
        if($LASTEXITCODE -ne 0 -and !$script:process.HasExited){throw 'Owned Functions shutdown failed.'}
    }else{$script:process.Kill($true)}
    if(!$script:process.WaitForExit(5000)){throw 'Owned Functions shutdown timed out.'}
}
function Start-Receipts([string]$Suffix){
    $psExe=(Get-Process -Id $PID).Path
    $script:process=Start-Process $psExe -ArgumentList @('-NoProfile','-File','scripts/Start-Receipts.ps1','-ConfigurationDirectory',$script:directory) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$directory/functions-$Suffix.out.log" -RedirectStandardError "$directory/functions-$Suffix.err.log"
    for($attempt=0;$attempt -lt 100;$attempt++){
        if($script:process.HasExited){throw 'Functions host exited; inspect private logs.'}
        $log=Get-Content -Raw "$directory/functions-$Suffix.out.log" -ErrorAction SilentlyContinue
        if($log -match 'GenerateBookingReceipt: serviceBusTrigger'){return}
        Start-Sleep -Milliseconds 300
    }
    throw 'Functions trigger startup timed out.'
}
Push-Location $root
try {
    $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,9450)
    try{$listener.Start()}catch{throw 'Functions port 9450 is busy; no existing process will be stopped.'}finally{$listener.Stop()}
    & "$PSScriptRoot/Install-Functions.ps1"
    $env:AZURELAB_RECEIPTS='1'
    if(!$SkipRelational){
        $directory=".local/receipts-$run"
        & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_receipts_$run"
        & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- receipts-local $directory
        Check 'Private receipt SQL/Azurite profile initialization failed.'
        $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
        & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName!~LiveReceiptTests&FullyQualifiedName!~ReceiptHostTests' --logger "trx;LogFileName=AZ-008-sql-$run.trx"
        Check 'SQL/Azurite regression checks failed; profile and data retained.'
    }
    # A fresh host profile prevents staged regression Outbox commands from activating workflows.
    $directory=".local/receiptshost-$run"
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_receiptshost_$run"
    & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- receipts-local $directory
    Check 'Host private configuration initialization failed.'
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
    $env:AZURELAB_RECEIPT_HOST='1'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~ReceiptHostTests.Stage' --logger "trx;LogFileName=AZ-008-host-stage-$run.trx"
    Check 'Durable host crash fixture failed.'
    Start-Receipts 'before-crash'
    $observed=$false
    for($attempt=0;$attempt -lt 150;$attempt++){
        if($process.HasExited){throw 'Functions exited before deterministic Blob barrier.'}
        if(Test-Path "$directory/receipt-after-blob.marker"){$observed=$true;break}
        Start-Sleep -Milliseconds 300
    }
    if(!$observed){throw 'Actual trigger did not reach Blob success barrier.'}
    Stop-Receipts
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~ReceiptHostTests.Crashed' --logger "trx;LogFileName=AZ-008-host-crashed-$run.trx"
    Check 'Actual Functions crash boundary assertion failed.'
    Start-Receipts 'restarted'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~ReceiptHostTests.Restarted' --logger "trx;LogFileName=AZ-008-host-restarted-$run.trx"
    Check 'Actual Functions redelivery/duplicate/DLQ checks failed.'
    if($IncludeHttps){
        $broker='Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'
        foreach($service in 'catalog','reservations','payments','notifications'){
            $path=Join-Path $root "$directory/$service.json"
            $config=Get-Content -Raw $path|ConvertFrom-Json
            $config|Add-Member -NotePropertyName Messaging -NotePropertyValue @{Enabled=$true;ConnectionString=$broker} -Force
            $config|ConvertTo-Json -Depth 12|Set-Content $path -Encoding UTF8
        }
        & "$PSScriptRoot/Verify-HttpsBff.ps1" -ConfigurationDirectory $directory -IncludeReceipts
        $trace=[IO.File]::ReadAllText((Join-Path $root "$directory/receipt-live-trace.txt"))
        if(!(Select-String -LiteralPath "$directory/functions-restarted.out.log" -SimpleMatch $trace -Quiet)){
            throw 'Actual receipt Function did not emit the booking trace ID; logs retained.'
        }
    }
    Write-Host "Actual Functions/SQL/Azurite verification passed; retained private host profile $directory."
} finally {
    Stop-Receipts
    foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Pop-Location
}