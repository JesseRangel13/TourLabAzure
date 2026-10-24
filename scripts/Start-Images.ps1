[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
& docker info --format '{{.OSType}}'
if($LASTEXITCODE -ne 0){throw 'Docker Linux engine is unavailable.'}
$existing=& docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" ps --all --quiet azurite
if($LASTEXITCODE -ne 0){throw 'Could not inspect this lab storage container.'}
if($existing){
    Write-Host 'Azurite already exists. Reuse it; this helper will not restart/recreate it or delete data.'
    return
}
& docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" up -d --no-deps --no-recreate azurite
if($LASTEXITCODE -ne 0){throw 'Azurite startup failed; inspect this lab only and retain its data.'}
Write-Host 'Only the dedicated Azure lab Azurite service started; SQL/broker/Cosmos were not changed.'
