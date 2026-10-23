[CmdletBinding()]
param([switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Push-Location $root
$failures = [Collections.Generic.List[string]]::new()
try {
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -ne '10.0.401') { $failures.Add('.NET SDK 10.0.401 required') }
    $node = & node --version
    if ($LASTEXITCODE -ne 0 -or $node -ne 'v24.18.0') { $failures.Add('Node 24.18.0 required') }
    $npm = & npm --version
    if ($LASTEXITCODE -ne 0 -or $npm -ne '12.0.1') { $failures.Add('npm 12.0.1 required') }
    $reparse = @(Get-ChildItem -LiteralPath $root -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
    if ($reparse.Count) { $failures.Add('Root contains writable link/reparse point') }
    Write-Host "Build toolchain: .NET $sdk; Node $node; npm $npm"
    if (!$BuildOnly) {
        & docker info --format '{{.OSType}}'
        if ($LASTEXITCODE -ne 0) { $failures.Add('Docker Linux engine NOT VERIFIED/unavailable') }
        if (!(Get-Command func -ErrorAction SilentlyContinue)) { $failures.Add('Functions Core Tools NOT VERIFIED/unavailable') }
        $ports = @(9443,9444,9445,9446,9447,4201,14339,5673,5301,10010,10011,10012,8089,7072)
        foreach ($port in $ports) {
            $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
            try { $listener.Start() } catch { $failures.Add("Port $port is in use/unavailable") } finally { $listener.Stop() }
        }
    }
    if ($failures.Count) { $failures | ForEach-Object { Write-Host "NOT VERIFIED: $_" }; exit 1 }
    Write-Host 'Requested preflight checks passed. No dependencies or cloud resources were started.'
} finally { Pop-Location }
