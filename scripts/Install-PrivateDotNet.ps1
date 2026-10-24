$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
if (!$IsWindows) { throw 'Use the official SDK10.0.402 installer for your platform; this verified ZIP installer is Windows-x64 only.' }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Windows-x64 SDK required by this helper.' }
$version = '10.0.402'
$destination = Join-Path $root '.local/dotnet'
if (Test-Path $destination) { throw 'Private SDK directory exists; no overwrite or cleanup is performed.' }
$release = (Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json' -TimeoutSec 20).releases |
    Where-Object { $_.sdk.version -eq $version } | Select-Object -First 1
$file = $release.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' }
if (!$file) { throw 'Exact official SDK artifact unavailable.' }
New-Item -ItemType Directory -Force "$root/.local/tools" | Out-Null
$archive = Join-Path $root ".local/tools/dotnet-sdk-$version.zip"
Invoke-WebRequest $file.url -OutFile $archive -TimeoutSec 180
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ine $file.hash) { throw 'Official SDK hash mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $destination
. "$PSScriptRoot/Use-Toolchain.ps1"
& dotnet --version
if ($LASTEXITCODE -ne 0) { throw 'Private SDK verification failed.' }
