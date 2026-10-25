[CmdletBinding()]
param([string]$ConfigurationDirectory = '.local/sql', [switch]$InstallBrowser)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root = Split-Path $PSScriptRoot
if($isWin){
    $ca=[Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $root '.local/certificates/ca.crt'))
    try{
        if(!(Test-Path ('Cert:\CurrentUser\Root\'+$ca.Thumbprint))){throw 'Chromium verification requires trusting this lab CA in CurrentUser Root. Complete the Windows security prompt from Trust-LocalCa.ps1; TLS validation remains required.'}
    }finally{$ca.Dispose()}
}
$previous=@{}
foreach($name in 'NODE_EXTRA_CA_CERTS','PLAYWRIGHT_BROWSERS_PATH','PLAYWRIGHT_SKIP_BROWSER_GC','AZURELAB_IDENTITY_SEEDS'){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
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
    if ($LASTEXITCODE -ne 0) { throw 'Browser checks failed; inspect private assertion reports; do not bypass TLS.' }
    $report=if($env:AZURELAB_BROWSER_REPORT){$env:AZURELAB_BROWSER_REPORT}else{Join-Path $root '.local/browser-report'}
    [xml]$junit=Get-Content -Raw (Join-Path $report 'junit.xml')
    if([int]$junit.testsuites.tests -lt 12 -or [int]$junit.testsuites.skipped -ne 0 -or [int]$junit.testsuites.failures -ne 0 -or [int]$junit.testsuites.errors -ne 0){throw 'Full browser coverage requires at least twelve tests with zero failures/errors/skips.'}
} finally {
    foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Pop-Location
}
