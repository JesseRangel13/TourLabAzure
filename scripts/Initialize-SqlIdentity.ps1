[CmdletBinding()]
param(
    [ValidateSet('LocalDb','Docker')][string]$Profile = 'LocalDb',
    [string]$OutputDirectory = '.local/sql',
    [string]$Database = 'tourlab_azure'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root = Split-Path $PSScriptRoot
Push-Location $root
try {
    if ($Profile -eq 'LocalDb') {
        $instances = & SqlLocalDB.exe info
        if ($LASTEXITCODE -ne 0) { throw 'SQL Server LocalDB is unavailable.' }
        if ($instances -notcontains 'tourlab-azure') {
            & SqlLocalDB.exe create tourlab-azure 17.0 -s
            if ($LASTEXITCODE -ne 0) { throw 'Isolated LocalDB creation failed.' }
        } else {
            & SqlLocalDB.exe start tourlab-azure
            if ($LASTEXITCODE -ne 0) { throw 'Isolated LocalDB start failed.' }
        }
    }
    $privateFile = Join-Path $OutputDirectory 'provisioner.json'
    if (!(Test-Path -LiteralPath $privateFile)) {
        & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- initialize $OutputDirectory $Database $Profile local-only
        if ($LASTEXITCODE -ne 0) { throw 'Private SQL/Identity setup failed.' }
    }
    & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- bootstrap $privateFile
    if ($LASTEXITCODE -ne 0) { throw 'SQL schema migration/seed failed.' }
} finally { Pop-Location }
