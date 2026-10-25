[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$isWin=$IsWindows -or ($env:OS -like '*Windows*') -or ([IO.Path]::DirectorySeparatorChar -eq '\')
$certificate=Join-Path $root '.local/certificates/ca.crt'
if($isWin){
    Import-Certificate -FilePath $certificate -CertStoreLocation 'Cert:\CurrentUser\Root' | Select-Object Subject,Thumbprint
}else{
    throw 'Import this generated CA into both OS and Chromium NSS trust stores; see docs/testing-execution.md. Never disable TLS validation.'
}
