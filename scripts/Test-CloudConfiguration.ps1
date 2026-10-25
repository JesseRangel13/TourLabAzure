[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('gateway','catalog','reservations','payments','notifications','receipts')][string]$Workload,
      [Parameter(Mandatory)][string]$Path)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$inputFile=[IO.Path]::GetFullPath($Path)
Push-Location $root
try{
    & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- validate-cloud $Workload $inputFile
    if($LASTEXITCODE -ne 0){throw 'Offline cloud configuration validation failed; no credentials or cloud resources were accessed.'}
}finally{Pop-Location}