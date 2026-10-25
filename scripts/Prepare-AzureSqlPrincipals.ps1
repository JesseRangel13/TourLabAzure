[CmdletBinding()]
param([Parameter(Mandatory)][string]$Plan,[string]$Output='.local/azure-principals-review.sql')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$inputFile=[IO.Path]::GetFullPath($Plan)
$target=[IO.Path]::GetFullPath((Join-Path $root $Output))
if(!$target.StartsWith((Join-Path $root '.local')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must stay inside this Azure lab .local directory.'}
Push-Location $root
try{
    & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- azure-sql-plan $inputFile $target
    if($LASTEXITCODE -ne 0){throw 'SQL artifact generation failed. Nothing was executed.'}
}finally{Pop-Location}