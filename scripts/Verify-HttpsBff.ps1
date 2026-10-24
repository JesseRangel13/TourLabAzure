[CmdletBinding()]
param([string]$ConfigurationDirectory = '.local/sql', [switch]$IncludeBooking, [switch]$IncludeRecovery, [switch]$IncludeImages)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root = Split-Path $PSScriptRoot
$privateRoot = [IO.Path]::GetFullPath((Join-Path $root $ConfigurationDirectory))
if (!$privateRoot.StartsWith((Join-Path $root '.local') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Only this lab private configuration is permitted.' }
if ($ConfigurationDirectory -match '\s') { throw 'Use a lab-relative configuration directory without spaces for this process helper.' }
$services = @('Gateway','Catalog','Reservations','Payments','Notifications')
$ports = @{ Gateway=9443; Catalog=9444; Reservations=9445; Payments=9446; Notifications=9447 }
foreach ($port in $ports.Values) {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
    try { $listener.Start() } catch { throw "Lab port $port is already in use; no existing process will be stopped." } finally { $listener.Stop() }
}
$run = Join-Path $root ('.local/https-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
Push-Location $root
$previousLive = $env:AZURELAB_LIVE_BFF
$previousConfig = $env:AZURELAB_INTEGRATION_CONFIG
function Stop-LabHost([Diagnostics.Process]$HostProcess) {
    if ($HostProcess.HasExited) { return }
    if ($PSVersionTable.PSEdition -eq 'Desktop') {
        # .NET Framework lacks Kill(entireProcessTree); target only this script's child PID.
        & "$env:WINDIR/System32/taskkill.exe" /PID $HostProcess.Id /T /F | Out-Null
        if ($LASTEXITCODE -ne 0 -and !$HostProcess.HasExited) { throw 'Lab host process-tree shutdown failed.' }
    } else {
        $HostProcess.Kill($true)
    }
    if (!$HostProcess.WaitForExit(5000)) { throw 'Lab host shutdown timed out.' }
}
function Start-LabHost([string]$Service) {
    $psExe = (Get-Process -Id $PID).Path
    $process = Start-Process $psExe -ArgumentList @('-NoProfile','-File','scripts/Start-Host.ps1','-Service',$Service,'-ConfigurationDirectory',$ConfigurationDirectory) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$run/$Service.out.log" -RedirectStandardError "$run/$Service.err.log"
    $processes[$Service] = $process
    $ready = $false
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) { throw "$Service exited before HTTPS readiness; inspect private logs." }
        & curl.exe --fail --silent --ssl-revoke-best-effort --cacert "$root/.local/certificates/ca.crt" "https://localhost:$($ports[$Service])/health/live" *> $null
        if ($LASTEXITCODE -eq 0) { $ready=$true; break }
        Start-Sleep -Milliseconds 300
    }
    if (!$ready) { throw "$Service HTTPS startup timed out." }
}
try {
    foreach ($service in $services) { Start-LabHost $service }
    $env:AZURELAB_LIVE_BFF = '1'
    $env:AZURELAB_INTEGRATION_CONFIG = Join-Path $privateRoot 'provisioner.json'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~LiveBffTests.RoundTrip' --logger 'trx;LogFileName=AZ-002-https-roundtrip.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Live HTTPS BFF roundtrip failed.' }
    if ($IncludeBooking) {
        & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~LiveBookingTests' --logger 'trx;LogFileName=AZ-004-live-booking.trx'
        if ($LASTEXITCODE -ne 0) { throw 'Live Service Bus booking/ownership checks failed.' }
    }
    if ($IncludeRecovery) {
        Write-Host 'Waiting for the unchanged 60-second credential rate window before the additional synthetic login phase.'
        Start-Sleep -Seconds 60
        & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~LiveRecoveryTests' --logger 'trx;LogFileName=AZ-005-live-recovery.trx'
        if ($LASTEXITCODE -ne 0) { throw 'Actual recovery workflow checks failed.' }
    }
    if ($IncludeImages) {
        & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~LiveImageTests.RoundTrip' --logger 'trx;LogFileName=AZ-006-https-images.trx'
        if ($LASTEXITCODE -ne 0) { throw 'HTTPS Blob image forwarding failed.' }
        Stop-LabHost $processes['Catalog']
        & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~LiveImageTests.Stage' --logger 'trx;LogFileName=AZ-006-crash-stage.trx'
        if ($LASTEXITCODE -ne 0) { throw 'Blob/SQL crash fixture failed.' }
        Start-LabHost 'Catalog'
        & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~LiveImageTests.Restarted' --logger 'trx;LogFileName=AZ-006-worker-restart.trx'
        if ($LASTEXITCODE -ne 0) { throw 'Catalog process restart recovery failed.' }
    }
    Stop-LabHost $processes['Gateway']
    Start-LabHost 'Gateway'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName~LiveBffTests.Restarted' --logger 'trx;LogFileName=AZ-002-https-restart.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Live HTTPS Gateway restart/TLS checks failed.' }
    Write-Host 'Real HTTPS BFF forwarding, role enforcement, cookie restart and untrusted-CA rejection passed.'
} finally {
    foreach($process in $processes.Values) { Stop-LabHost $process }
    $env:AZURELAB_LIVE_BFF = $previousLive
    $env:AZURELAB_INTEGRATION_CONFIG = $previousConfig
    Pop-Location
}
