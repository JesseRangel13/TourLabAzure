[CmdletBinding()]
param([switch]$AcceptSqlDeveloperLicense,[switch]$AcceptServiceBusLicense)
$ErrorActionPreference = 'Stop'
if (!$AcceptSqlDeveloperLicense -or !$AcceptServiceBusLicense) {
    throw 'Explicit -AcceptSqlDeveloperLicense and -AcceptServiceBusLicense are required. Read the license links in docs/local-execution.md. SQL Developer is development/test only.'
}
$root = Split-Path $PSScriptRoot
& docker info --format '{{.OSType}}'
if ($LASTEXITCODE -ne 0) { throw 'Docker Linux engine unavailable; no resources started.' }
$previous = $env:AZURELAB_ACCEPT_EULA
$env:AZURELAB_ACCEPT_EULA = 'Y'
try {
    & docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" up -d
    if ($LASTEXITCODE -ne 0) { throw 'Dependency startup failed. Inspect only this project; data is preserved.' }
    Write-Host 'Containers started; this does not verify emulator readiness. AZ-002/004/006/007 supply integration checks.'
} finally { $env:AZURELAB_ACCEPT_EULA = $previous }
