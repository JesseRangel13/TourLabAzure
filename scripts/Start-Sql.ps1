[CmdletBinding()]
param([switch]$AcceptSqlDeveloperLicense)
$ErrorActionPreference = 'Stop'
if (!$AcceptSqlDeveloperLicense) { throw 'Explicit -AcceptSqlDeveloperLicense is required; SQL Developer is development/test only.' }
$root = Split-Path $PSScriptRoot
$previous = $env:AZURELAB_ACCEPT_EULA
$env:AZURELAB_ACCEPT_EULA = 'Y'
try {
    & docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) { throw 'Docker Linux engine unavailable.' }
    & docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" up -d sql
    if ($LASTEXITCODE -ne 0) { throw 'Azure-only SQL container startup failed.' }
    $ready = $false
    for ($attempt=0; $attempt -lt 45; $attempt++) {
        & docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" exec -T sql /bin/sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "SELECT 1"' *> $null
        if ($LASTEXITCODE -eq 0) { $ready=$true; break }
        Start-Sleep -Seconds 2
    }
    if (!$ready) { throw 'Azure SQL container did not become ready within the bounded startup window.' }
    Write-Host 'Only the isolated SQL container is ready; broker/storage dependencies were not started.'
} finally { $env:AZURELAB_ACCEPT_EULA = $previous }
