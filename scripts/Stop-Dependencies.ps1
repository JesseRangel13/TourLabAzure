$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
& docker compose --project-directory $root --env-file "$root/.local/dependencies.env" -f "$root/compose.yaml" stop
if ($LASTEXITCODE -ne 0) { throw 'Dependency stop failed.' }
Write-Host 'Only this Compose project was stopped. Volumes and data were retained.'
