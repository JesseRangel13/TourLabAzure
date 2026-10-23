[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$local = Join-Path $root '.local'
if (Test-Path -LiteralPath $local) { throw '.local already exists; reuse it. No secrets are overwritten.' }
New-Item -ItemType Directory -Path "$local/certificates" -Force | Out-Null
if ($IsWindows) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls.exe $local /inheritance:r /grant:r "${identity}:(OI)(CI)F" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to restrict private configuration ACL.' }
} else {
    & chmod 700 $local "$local/certificates"
    if ($LASTEXITCODE -ne 0) { throw 'Failed to restrict private configuration permissions.' }
}
function New-Random([int]$length = 32) { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes($length)) }
$sql = 'Az1!' + (New-Random)
$brokerSql = 'Az2!' + (New-Random)
$blobKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
@(
    'AZURELAB_ACCEPT_EULA=N'
    "AZURELAB_SQL_PASSWORD=$sql"
    "AZURELAB_BROKER_SQL_PASSWORD=$brokerSql"
    "AZURELAB_AZURITE_ACCOUNTS=tourlabazure:$blobKey"
) | Set-Content -LiteralPath "$local/dependencies.env"
$caKey = [Security.Cryptography.RSA]::Create(3072)
$leafKey = [Security.Cryptography.RSA]::Create(3072)
try {
    $hash = [Security.Cryptography.HashAlgorithmName]::SHA256
    $padding = [Security.Cryptography.RSASignaturePadding]::Pkcs1
    $caRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=TourLab Azure Local CA', $caKey, $hash, $padding)
    $caRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true,$false,0,$true))
    $caRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign,$true))
    $now = [DateTimeOffset]::UtcNow
    $ca = $caRequest.CreateSelfSigned($now.AddMinutes(-5),$now.AddYears(1))
    $leafRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost',$leafKey,$hash,$padding)
    $leafRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false,$false,0,$true))
    $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    $san.AddDnsName('localhost')
    $san.AddIpAddress([Net.IPAddress]::Loopback)
    $leafRequest.CertificateExtensions.Add($san.Build())
    $eku = [Security.Cryptography.OidCollection]::new()
    [void]$eku.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))
    $leafRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku,$true))
    $issued = $leafRequest.Create($ca,$now.AddMinutes(-5),$now.AddMonths(6),[Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    $leaf = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($issued,$leafKey)
    [IO.File]::WriteAllText("$local/certificates/ca.crt",$ca.ExportCertificatePem())
    [IO.File]::WriteAllText("$local/certificates/localhost.crt",$leaf.ExportCertificatePem())
    [IO.File]::WriteAllText("$local/certificates/localhost.key",$leafKey.ExportPkcs8PrivateKeyPem())
    $password = New-Random
    [IO.File]::WriteAllBytes("$local/certificates/localhost.pfx",$leaf.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,$password))
    [IO.File]::WriteAllText("$local/certificates/pfx-password",$password)
    # The CA private key is never persisted; future regeneration is a deliberate new trust decision.
    $leaf.Dispose(); $issued.Dispose(); $ca.Dispose()
} finally { $leafKey.Dispose(); $caKey.Dispose() }
Write-Host 'Fresh private dependency credentials and lab CA generated under .local. Nothing was started or trusted.'
