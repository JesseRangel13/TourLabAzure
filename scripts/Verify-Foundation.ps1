[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root = Split-Path $PSScriptRoot
Push-Location $root
try {
    & "$PSScriptRoot/Test-Preflight.ps1" -BuildOnly
    if ($LASTEXITCODE -ne 0) { throw 'Toolchain preflight failed' }
    & dotnet restore TourLab.Azure.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked solution restore failed' }
    & dotnet msbuild src/Receipts.Functions/Receipts.Functions.csproj -t:GenerateFunctionsExtensionProject -verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Functions extension restore failed' }
    & dotnet build TourLab.Azure.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw '.NET build failed' }
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName!~SqlIsolationTests&FullyQualifiedName!~IdentityHttpTests&FullyQualifiedName!~LiveBffTests&FullyQualifiedName!~CatalogTests&FullyQualifiedName!~CatalogHttpTests&FullyQualifiedName!~BookingSqlTests&FullyQualifiedName!~BrokerIntegrationTests&FullyQualifiedName!~LiveBookingTests&FullyQualifiedName!~RecoverySqlTests&FullyQualifiedName!~RecoveryHttpTests&FullyQualifiedName!~BrokerRecoveryTests&FullyQualifiedName!~LiveRecoveryTests&FullyQualifiedName!~ImageIntegrationTests&FullyQualifiedName!~LiveImageTests' --logger 'trx;LogFileName=AZ-001-hosts.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Host tests failed' }
    Push-Location web/tours-angular
    try {
        & npm ci --ignore-scripts
        if ($LASTEXITCODE -ne 0) { throw 'Locked Angular install failed' }
        & npm test
        if ($LASTEXITCODE -ne 0) { throw 'Angular tests failed' }
        & npm audit --audit-level=moderate
        if ($LASTEXITCODE -ne 0) { throw 'Angular dependency audit failed' }
    } finally { Pop-Location }
    & "$PSScriptRoot/Build-Web.ps1"
    & docker compose --env-file .local/dependencies.env config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Compose structure validation failed; generate .local first' }
    Write-Host 'Foundation checks passed. No SQL, emulator, Functions host, browser or cloud acceptance is implied.'
} finally { Pop-Location }
