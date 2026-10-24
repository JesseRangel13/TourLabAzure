[CmdletBinding()]
param([string]$ConfigurationDirectory = '.local/sql', [ValidateSet('Gateway','Catalog','Reservations','Payments','Notifications')][string]$Service = 'Gateway')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root = Split-Path $PSScriptRoot
$ports = @{ Gateway=9443; Catalog=9444; Reservations=9445; Payments=9446; Notifications=9447 }
$env:AZURELAB_Kestrel__Certificates__Default__Path = Join-Path $root '.local/certificates/localhost.pfx'
$env:AZURELAB_Kestrel__Certificates__Default__Password = [IO.File]::ReadAllText((Join-Path $root '.local/certificates/pfx-password'))
$env:AZURELAB_URLS = "https://127.0.0.1:$($ports[$Service])"
$env:DOTNET_ENVIRONMENT = 'Development'
$schema = if ($Service -eq 'Gateway') { 'gateway' } else { $Service.ToLowerInvariant() }
$env:AZURELAB_CONFIG = Join-Path ([IO.Path]::GetFullPath((Join-Path $root $ConfigurationDirectory))) "$schema.json"
if (!(Test-Path -LiteralPath $env:AZURELAB_CONFIG)) { throw 'Run Initialize-SqlIdentity.ps1 before starting hosts.' }
try {
    & dotnet run --project "$root/src/$Service.Api" -c Release --no-build --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw "$Service host failed." }
} finally {
    Remove-Item Env:AZURELAB_Kestrel__Certificates__Default__Password -ErrorAction SilentlyContinue
}
