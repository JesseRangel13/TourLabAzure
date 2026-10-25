[CmdletBinding()]
param([ValidateSet('LocalDb','Docker')][string]$Profile='LocalDb',[switch]$InstallBrowser,[switch]$IsolatedBroker,[switch]$AcceptSqlDeveloperLicense,[switch]$AcceptServiceBusLicense)
$ErrorActionPreference='Stop'
if($IsolatedBroker -and (!$AcceptSqlDeveloperLicense -or !$AcceptServiceBusLicense)){throw 'Isolated test broker requires both explicit license acceptance switches.'}
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
. "$PSScriptRoot/Test-BrokerProfile.ps1"
$testBroker=$null
$run=[Guid]::NewGuid().ToString('N')
$directory=".local/e2e-$run"
$processes=@{}
$previous=@{}
foreach($name in 'AZURELAB_IDENTITY_SEEDS','AZURELAB_BASE_URL','AZURELAB_BROWSER_REPORT'){
    $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
}
function Check([string]$Message){if($LASTEXITCODE -ne 0){throw $Message}}
function Start-Owned([string]$Name,[string[]]$Arguments){
    $options=@{FilePath=(Get-Process -Id $PID).Path;ArgumentList=$Arguments;WorkingDirectory=$root;
        PassThru=$true;RedirectStandardOutput="$directory/$Name.out.log";RedirectStandardError="$directory/$Name.err.log"}
    if($isWin){$options.WindowStyle='Hidden'}
    $processes[$Name]=Start-Process @options
    @($processes.Keys|ForEach-Object {@{name=$_;id=$processes[$_].Id}})|ConvertTo-Json|Set-Content "$directory/processes.json"
}
Push-Location $root
try{
    # Refuse collisions before creating resources. Never stop pre-existing listeners.
    foreach($port in 9443,9444,9445,9446,9447,9450){
        $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
        try{$listener.Start()}catch{throw "Port $port is occupied; existing processes are preserved."}finally{$listener.Stop()}
    }
    if($IsolatedBroker){$testBroker=Start-IsolatedTestBroker $run $AcceptSqlDeveloperLicense.IsPresent $AcceptServiceBusLicense.IsPresent}
    $healthPort=if($IsolatedBroker){5302}else{5301}
    $health=Invoke-WebRequest "http://127.0.0.1:$healthPort/health" -UseBasicParsing -TimeoutSec 5
    if($health.StatusCode -ne 200){throw 'Actual Service Bus emulator is required.'}
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -Profile $Profile -OutputDirectory $directory -Database "tourlab_azure_e2e_$run"
    foreach($feature in 'images-local','dashboard-local','receipts-local'){
        & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- $feature $directory
        Check "Actual $feature setup failed. Private data retained."
    }
    foreach($service in 'catalog','reservations','payments','notifications'){
        $path=Join-Path $root "$directory/$service.json"
        $config=Get-Content -Raw $path|ConvertFrom-Json
        $config|Add-Member -NotePropertyName Messaging -NotePropertyValue @{Enabled=$true;LocalTestBroker=$IsolatedBroker.IsPresent;ConnectionString=($('Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;').Replace(':5673;',$(if($IsolatedBroker){':5674;'}else{':5673;'})))} -Force
        $config|ConvertTo-Json -Depth 12|Set-Content $path -Encoding UTF8
    }
    if($IsolatedBroker){
        $workerPath=Join-Path $root "$directory/receipts.json"
        $worker=Get-Content -Raw $workerPath|ConvertFrom-Json
        $worker|Add-Member -NotePropertyName ReceiptTestBroker -NotePropertyValue $true -Force
        $worker|ConvertTo-Json -Depth 12|Set-Content $workerPath -Encoding UTF8
        $hostPath=Join-Path $root "$directory/receipts-host.json"
        $hostSettings=Get-Content -Raw $hostPath|ConvertFrom-Json
        $hostSettings.ReceiptBus=$hostSettings.ReceiptBus.Replace(':5673;',':5674;')
        $hostSettings|ConvertTo-Json -Depth 12|Set-Content $hostPath -Encoding UTF8
    }
    & "$PSScriptRoot/Install-Functions.ps1"
    Start-Owned 'functions' @('-NoProfile','-File','scripts/Start-Receipts.ps1','-ConfigurationDirectory',$directory)
    $trigger=$false
    for($attempt=0;$attempt -lt 120;$attempt++){
        if($processes.functions.HasExited){throw 'Functions exited; inspect private logs.'}
        if((Get-Content -Raw "$directory/functions.out.log" -ErrorAction SilentlyContinue) -match 'GenerateBookingReceipt: serviceBusTrigger'){$trigger=$true;break}
        Start-Sleep -Milliseconds 500
    }
    if(!$trigger){throw 'Actual Functions listener startup timed out.'}
    $curl=if($isWin){'curl.exe'}else{'curl'}
    $curlArguments=@('--fail','--silent','--cacert',"$root/.local/certificates/ca.crt")
    if($isWin){$curlArguments+='--ssl-revoke-best-effort'}
    foreach($service in 'Catalog','Reservations','Payments','Notifications','Gateway'){
        Start-Owned $service @('-NoProfile','-File','scripts/Start-Host.ps1','-Service',$service,'-ConfigurationDirectory',$directory)
        $port=@{Gateway=9443;Catalog=9444;Reservations=9445;Payments=9446;Notifications=9447}[$service]
        $ready=$false
        for($attempt=0;$attempt -lt 60;$attempt++){
            if($processes[$service].HasExited){throw "$service exited; inspect private logs."}
            & $curl @curlArguments "https://localhost:$port/health/live" *> $null
            if($LASTEXITCODE -eq 0){$ready=$true;break}
            Start-Sleep -Milliseconds 500
        }
        if(!$ready){throw "$service HTTPS liveness startup timed out; authenticated readiness is verified by the browser suite."}
    }
    $env:AZURELAB_BASE_URL='https://localhost:9443'
    $env:AZURELAB_BROWSER_REPORT=Join-Path $root "$directory/browser-report"
    & "$PSScriptRoot/Verify-Browser.ps1" -ConfigurationDirectory $directory -InstallBrowser:$InstallBrowser
    $trace=[IO.File]::ReadAllText((Join-Path $env:AZURELAB_BROWSER_REPORT 'receipt-trace-id.txt')).Trim()
    if($trace -notmatch '^[a-f0-9]{32}$' -or !(Select-String -LiteralPath "$directory/functions.out.log" -SimpleMatch $trace -Quiet)){
        throw 'Actual browser booking trace was not observed in the Functions host log.'
    }
    '{"schemaVersion":1,"passed":true,"functionsTraceVerified":true}'|Set-Content "$directory/completed.json"
    Write-Host "Full-stack browser verification passed. Retained profile, reports and logs: $directory."
}finally{
    foreach($process in $processes.Values){
        if(!$process.HasExited){
            if($PSVersionTable.PSEdition -eq 'Desktop'){
                & "$env:WINDIR/System32/taskkill.exe" /PID $process.Id /T /F|Out-Null
            }else{$process.Kill($true)}
            if(!$process.WaitForExit(5000)){Write-Warning 'An owned child did not exit; inspect its recorded PID.'}
        }
    }
    Stop-IsolatedTestBroker $testBroker
    foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Pop-Location
}
