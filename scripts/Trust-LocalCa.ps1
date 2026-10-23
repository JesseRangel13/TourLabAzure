[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (!$IsWindows) { throw 'On Linux/macOS import .local/certificates/ca.crt into your user/browser trust store; never disable TLS validation.' }
$certificate = Join-Path (Split-Path $PSScriptRoot) '.local/certificates/ca.crt'
Import-Certificate -FilePath $certificate -CertStoreLocation ('Cert:' + [IO.Path]::DirectorySeparatorChar + 'CurrentUser' + [IO.Path]::DirectorySeparatorChar + 'Root') | Select-Object Subject, Thumbprint
