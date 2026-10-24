[CmdletBinding()]
param([switch]$AcceptSqlDeveloperLicense,[switch]$AcceptServiceBusLicense)
$ErrorActionPreference='Stop'
if(!$AcceptSqlDeveloperLicense -or !$AcceptServiceBusLicense){
    throw 'Explicit license acceptance is required: SQL https://go.microsoft.com/fwlink/?LinkId=746388 and emulator https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt'
}
$root=Split-Path $PSScriptRoot
& docker info --format '{{.OSType}}'
if($LASTEXITCODE -ne 0){throw 'Docker Linux engine unavailable. No resources started.'}
$existing=& docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" ps -a -q servicebus
if($LASTEXITCODE -ne 0){throw 'Unable to inspect this lab broker.'}
if($existing){throw 'A lab broker already exists. Preserve its queued data; inspect topology/readiness without automatic recreation.'}
$previous=$env:AZURELAB_ACCEPT_EULA
$env:AZURELAB_ACCEPT_EULA='Y'
try{
    & docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" up -d broker-sql servicebus
    if($LASTEXITCODE -ne 0){throw 'Isolated broker startup failed; retain containers and logs.'}
    $ready=$false
    for($attempt=0;$attempt -lt 60;$attempt++){
        try{$response=Invoke-WebRequest 'http://localhost:5301/health' -UseBasicParsing -TimeoutSec 3;if($response.StatusCode -eq 200){$ready=$true;break}}catch{}
        Start-Sleep -Seconds 5
    }
    if(!$ready){throw 'Broker readiness timed out; no containers or data were deleted.'}
    Write-Host 'Local Service Bus emulator health passed. Run Verify-Workflow.ps1 for actual booking evidence.'
}finally{$env:AZURELAB_ACCEPT_EULA=$previous}