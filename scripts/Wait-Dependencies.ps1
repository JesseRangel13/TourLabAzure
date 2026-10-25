[CmdletBinding()]
param([switch]$SkipBroker)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
# Readiness only; no creation, restart, purge or TLS bypass. SDK provisioning performs
# the final authenticated Cosmos/Blob verification in Verify-Integration/FullStack.
$ports=@(14339,10010,10011,10012,8089)
if(!$SkipBroker){$ports+=5673}
foreach($port in $ports){
    $ready=$false
    for($attempt=0;$attempt -lt 90;$attempt++){
        $client=[Net.Sockets.TcpClient]::new()
        try{
            $client.Connect('127.0.0.1',$port)
            if($client.Connected){$ready=$true;break}
        }catch{}finally{$client.Dispose()}
        Start-Sleep -Seconds 2
    }
    if(!$ready){throw "Lab dependency TCP port $port did not become available; retain data/logs."}
}
if(!$SkipBroker){
    $response=Invoke-WebRequest 'http://localhost:5301/health' -UseBasicParsing -TimeoutSec 5
    if($response.StatusCode -ne 200){throw 'Broker health unavailable.'}
}
Write-Host 'Dependency listeners ready; actual SDK tests are still required.'
