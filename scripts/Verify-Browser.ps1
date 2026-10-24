[CmdletBinding()]
param([switch]$InstallBrowser)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$env:NODE_EXTRA_CA_CERTS = Join-Path $root '.local/certificates/ca.crt'
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $root '.local/playwright'
$env:PLAYWRIGHT_SKIP_BROWSER_GC = '1'
$privateRoot = [IO.Path]::GetFullPath((Join-Path $root $ConfigurationDirectory))
if (!$privateRoot.StartsWith((Join-Path $root '.local') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Only this lab private configuration is permitted.' }
$env:AZURELAB_IDENTITY_SEEDS = Join-Path $privateRoot 'provisioner.json'
Push-Location "$root/tests/browser"
try {
    & npm ci --ignore-scripts
    if ($LASTEXITCODE -ne 0) { throw 'Locked browser install failed' }
    if ($InstallBrowser) {
        & npx --no-install playwright install chromium
        if ($LASTEXITCODE -ne 0) { throw 'Private browser runtime installation failed' }
    }
    # Start Gateway separately with Start-Host.ps1; first trust its CA in the browser/OS.
    & npx --no-install playwright test
    if ($LASTEXITCODE -ne 0) { throw 'Browser smoke failed; inspect local failure traces; do not bypass TLS.' }
} finally { Pop-Location }
