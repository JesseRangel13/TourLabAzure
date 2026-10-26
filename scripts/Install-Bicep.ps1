[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$isWin=$IsWindows -or ($env:OS -like '*Windows*') -or ([IO.Path]::DirectorySeparatorChar -eq '\')
$name=if($isWin){'bicep-win-x64.exe'}else{'bicep-linux-x64'}
$hash=if($isWin){'398AF294CF16AC4BECDBD008A77D148D0E8E91492309A6B60EC911B7E1CAB082'}else{'B09EC25A9D376C1F8E33EDE6ED22B587F915AD68488D5DB77A6F9541748C7F6E'}
$folder=Join-Path $root '.local/tools/bicep-0.48.1'
New-Item -ItemType Directory -Force $folder | Out-Null
$target=Join-Path $folder $(if($isWin){'bicep.exe'}else{'bicep'})
if(!(Test-Path -LiteralPath $target)) { Invoke-WebRequest "https://github.com/Azure/bicep/releases/download/v0.48.1/$name" -OutFile $target }
if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw 'Bicep SHA256 mismatch; do not execute.'}
if(!$isWin){ & chmod u+x $target; if($LASTEXITCODE -ne 0){throw 'Bicep executable permission failed.'} }
& $target --version
if($LASTEXITCODE -ne 0){throw 'Bicep version check failed.'}
