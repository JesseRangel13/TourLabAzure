$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Push-Location "$root/web/tours-angular"
try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Angular production build failed.' }
    $destination = "$root/src/Gateway.Api/wwwroot"
    New-Item -ItemType Directory -Force $destination | Out-Null
    # Never delete application data. All files here are built SPA assets.
    Copy-Item -Path 'dist/tours/browser/*' -Destination $destination -Recurse -Force
} finally { Pop-Location }
