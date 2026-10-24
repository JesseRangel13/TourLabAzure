$toolchainRoot = Split-Path $PSScriptRoot
$privateDotnet = Join-Path $toolchainRoot '.local/dotnet'
$isWin = $IsWindows -or ($env:OS -like '*Windows*') -or ([IO.Path]::DirectorySeparatorChar -eq '\')
$dotnetBinary = Join-Path $privateDotnet $(if ($isWin) { 'dotnet.exe' } else { 'dotnet' })
if (Test-Path -LiteralPath $dotnetBinary) {
    $env:DOTNET_ROOT = $privateDotnet
    if (($env:PATH -split [IO.Path]::PathSeparator) -notcontains $privateDotnet) {
        $env:PATH = $privateDotnet + [IO.Path]::PathSeparator + $env:PATH
    }
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
