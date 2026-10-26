#requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ConfigurationDirectory)
. "$PSScriptRoot/Use-Toolchain.ps1"
. "$PSScriptRoot/Cloud-Common.ps1"
$dir=Get-LabPrivatePath $ConfigurationDirectory
$run=Get-LabPrivatePath ('.local/hosting-measure-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$services=@('Gateway','Catalog','Reservations','Payments','Notifications')
foreach($port in 9443..9447){$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port);try{$listener.Start()}finally{$listener.Stop()}}
$processes=@();$saved=@{}
$names=@('AZURELAB_CONFIG','DOTNET_ENVIRONMENT','AZURELAB_URLS','AZURELAB_Messaging__Enabled','AZURELAB_Kestrel__Certificates__Default__Path','AZURELAB_Kestrel__Certificates__Default__Password')
foreach($n in $names){$saved[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
try{
    $env:DOTNET_ENVIRONMENT='Development';$env:AZURELAB_Messaging__Enabled='false'
    $env:AZURELAB_Kestrel__Certificates__Default__Path=Join-Path $script:AzureLabRoot '.local/certificates/localhost.pfx'
    $env:AZURELAB_Kestrel__Certificates__Default__Password=Get-Content -Raw (Join-Path $script:AzureLabRoot '.local/certificates/pfx-password')
    foreach($i in 0..4){
        $w=$services[$i];$env:AZURELAB_CONFIG=Join-Path $dir ($w.ToLowerInvariant()+'.json');$env:AZURELAB_URLS='https://127.0.0.1:'+(9443+$i)
        $dll=Join-Path $script:AzureLabRoot "src/$w.Api/bin/Release/net10.0/$w.Api.dll"
        $p=Start-Process (Get-Command dotnet).Source -ArgumentList ('"'+$dll+'"') -WorkingDirectory $script:AzureLabRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$run/$w.out.log" -RedirectStandardError "$run/$w.err.log"
        $processes+=@{service=$w;process=$p}
    }
    foreach($i in 0..4){
        $ok=$false
        for($attempt=0;$attempt -lt 30;$attempt++){
            $status=& curl.exe --silent --output NUL --write-out '%{http_code}' --connect-timeout 2 --max-time 3 --ssl-revoke-best-effort --cacert "$script:AzureLabRoot/.local/certificates/ca.crt" "https://localhost:$(9443+$i)/health/live"
            if($LASTEXITCODE -eq 0 -and $status -eq '200'){$ok=$true;break};Start-Sleep -Milliseconds 500
        }
        if(!$ok){throw 'Owned measurement host did not become live.'}
        foreach($request in 1..10){
            $status=& curl.exe --silent --output NUL --write-out '%{http_code}' --connect-timeout 2 --max-time 3 --ssl-revoke-best-effort --cacert "$script:AzureLabRoot/.local/certificates/ca.crt" "https://localhost:$(9443+$i)/health/live"
            if($LASTEXITCODE -ne 0 -or $status -ne '200'){throw 'Measurement HTTPS request failed.'}
        }
    }
    Start-Sleep -Seconds 5
    $rows=@($processes | ForEach-Object {$_.process.Refresh();[ordered]@{service=$_.service;workingSetBytes=$_.process.WorkingSet64;peakWorkingSetBytes=$_.process.PeakWorkingSet64;privateBytes=$_.process.PrivateMemorySize64;totalProcessorSeconds=$_.process.TotalProcessorTime.TotalSeconds}})
    [ordered]@{schemaVersion=1;environment='Windows local Development; five APIs; workers disabled; no business load or Functions';httpsLivenessRequests=50;cloudCapacityVerified=$false;processes=$rows;totalWorkingSetBytes=($rows | ForEach-Object { $_.workingSetBytes } | Measure-Object -Sum).Sum;totalPeakWorkingSetBytes=($rows | ForEach-Object { $_.peakWorkingSetBytes } | Measure-Object -Sum).Sum} | ConvertTo-Json -Depth 5 | Set-Content "$run/measurement.json"
    Copy-Item "$run/measurement.json" "$script:AzureLabRoot/docs/evidence/AZ-011-local-hosting.json"
    Write-Host 'Limited local memory observation recorded. This is not a cloud capacity/load acceptance result.'
}finally{
    foreach($item in $processes){if(!$item.process.HasExited){$item.process.Kill($true);$item.process.WaitForExit()}}
    foreach($n in $names){[Environment]::SetEnvironmentVariable($n,$saved[$n],'Process')}
}
