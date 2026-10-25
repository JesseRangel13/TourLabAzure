# AZ-009 security execution and configuration

AZ-009 prepares workload identity and Key Vault paths. It creates no Azure resource, assigns no cloud
role, retrieves no real vault secret and runs no Azure SQL statement. Public templates deliberately
contain invalid placeholders. A structural PASS is not proof of authentication, RBAC or deployment.

## Local execution

Run only from the Azure companion. Existing SQL2025 LocalDB, Service Bus, Azurite and Cosmos are
retained. The emulator licenses were accepted in the earlier tasks. Scripts never recreate containers
or purge data to repair a test.

```powershell
. .\scripts\Use-Toolchain.ps1
.\scripts\Verify-Foundation.ps1
.\scripts\Verify-Security.ps1 -IncludeHttps
```

The security helper creates a fresh retained SQL/private profile, configures the existing private
image/receipt containers and Cosmos dashboard, and executes the real SQL/storage regression suite.
It includes provider-restart/certificate-rollover tests using a separate retained SQL database, plus
effective receipt-table grants checked through SQL EXECUTE AS. This proves SQL authorization semantics,
not Entra authentication. Its HTTPS phase delegates to the receipt helper with another fresh SQL
profile and actual isolated Functions host/broker/Azurite. Owned child processes stop in finally.
No original repository resource is used.

Local credentials remain in ignored restricted .local profiles or process environment overrides.
Use-Toolchain supports Windows PowerShell5.1. ASP.NET configuration can consume process environment
settings such as AZURELAB_ConnectionStrings__Runtime; do not print values or put them in command history,
Git, test artifacts or frontend state. User-secrets is an optional ASP.NET configuration alternative
when an explicit UserSecretsId is supplied by the operator; this task does not create a second store
or claim it was executed. The verified default is the generated restricted file/profile path.

Local SQL accepts only the dedicated (localdb)\\tourlab-azure or127.0.0.1,14339 endpoints with bounded
schema credentials/pools; developer authentication chains and remote SQL are rejected. LocalDB's
documented local TLS exception and the Docker profile's explicit local certificate exception remain
Development-only. API HTTPS continues to trust the generated lab CA with normal name/chain checks.

## Explicit cloud mode

All six hosts require AzureSecurity:Mode=ManagedIdentity outside Development. Local mode/defaults
cannot start in Production. ManagedIdentity in Development is rejected; local verification never
silently accesses a cloud account.

System-assigned managed identity is the supported deliberate selection for each app. Azure SDK clients
use ManagedIdentityCredential(ManagedIdentityId.SystemAssigned); SQL uses only Active Directory Managed
Identity with no user/password. No DefaultAzureCredential, Azure CLI, Visual Studio, environment client
secret or interactive credential-chain fallback is registered. User-assigned identity overrides are
not supported by this profile and are rejected instead of being ignored.

Templates: config/cloud/{gateway,catalog,reservations,payments,notifications,receipts}.json.example.
Replace placeholders privately and supply the resulting public settings through the deployment
configuration in AZ-011/012. Do not add access keys, JWT private material or SQL passwords to templates.

```powershell
.\scripts\Test-CloudConfiguration.ps1 -Workload gateway -Path .local/review/gateway.json
.\scripts\Prepare-AzureSqlPrincipals.ps1 -Plan .local/review/sql-principals.json -Output .local/review/azure-principals.sql
```

The first command loads/validates JSON and public keys only; it does not acquire a token or read Key
Vault. The second generates an artifact and refuses overwrite; it never connects to SQL. Both report
only safe status or exception type. The shipped example files intentionally fail until placeholders
are replaced. See cloud-security-checklist.md for checks that offline validation cannot establish.

API cloud settings use the normal nested configuration keys, or corresponding environment separators
(e.g. AZURELAB_Messaging__FullyQualifiedNamespace). Functions trigger/host settings must ALSO be visible
to the Functions HOST before worker startup. Configure these unprefixed platform app settings:

| Host setting | Cloud value |
|---|---|
| ReceiptBus__fullyQualifiedNamespace | owned tourlabazure*.servicebus.windows.net |
| ReceiptBus__credential | managedidentity |
| AzureWebJobsStorage__credential | managedidentity |
| AzureWebJobsStorage__blobServiceUri | separate owned host account HTTPS endpoint |
| AzureWebJobsStorage__queueServiceUri | same host account queue HTTPS endpoint |
| AzureWebJobsStorage__tableServiceUri | same host account table HTTPS endpoint |

Omit the scalar ReceiptBus/AzureWebJobsStorage connection strings and user-assigned client/resource
ID overrides. The worker JSON alone cannot configure the platform listener. The offline receipts
template represents both hierarchies; deployment must flatten host keys appropriately. Host storage
has its own account, separate from the private booking-receipts business container/account. The
platform performs host authentication; worker validation cannot retroactively guard a host that was
already started with unsafe platform settings. Validate both before an authorized deployment.

## Signing secrets and rotation

Only Gateway reads tourlab-azure-human-signing; only Reservations reads tourlab-azure-quote-signing.
Key Vault access uses the Secrets SDK with one explicit GetSecret(name,version), not secret listing or
whole-vault configuration enumeration. The reference must be HTTPS, same configured lab vault, exact
allowlisted secret name and explicit32hex version. Public settings contain the expected uppercase
SHA256 of the RSA SubjectPublicKeyInfo DER. The retrieved3072-bit-or-stronger private key must match it.

The startup read has a20second overall cancellation budget,5second SDK network timeout and no SDK
retries. Disabled, not-yet-active, expired, malformed, wrong-version/wrong-key or denied secrets fail
startup. Failure diagnostics contain a static safe code, no response body, value or inner SDK exception.
The private PEM remains in process memory and the disposable signing RSA; it is not written to disk.

There is no background refresh or immediate revocation of cached signing secrets. Disabling, expiring or deleting a vault version does not erase the RSA already held by a running signer. Emergency revocation requires removing that public key from validators and stopping/restarting the signer into an approved version. Updating a vault version alone does not change
running signers. Rotate deliberately:

1. Generate/store the next private key through a separately authorized secure operator process.
   Distribute ONLY its public SPKI and computed key ID to validators alongside the previous public key.
2. Restart validators with their bounded two-key allowlist. Confirm the new key is accepted while the
   existing issuer, audience, algorithm, role, ownership and lifetime restrictions still apply.
3. Change the signer's explicit secret-version URI and expected public fingerprint; restart it.
   New JWTs carry the SHA256 key ID. Unknown/missing cloud key IDs cannot select a validation key.
4. Retain the old public key until every previously issued token has expired (up to15minutes plus
   clock skew), then remove it with another controlled validator restart.

Existing local key-file configuration remains supported; local token validation retains the original
compatibility with old kid-less test tokens. Production key rotation does not weaken signature,
issuer/audience, expiry, role or service-token claim checks.

## Durable protected keys

Gateway persists its Data Protection ring in identity.DataProtectionKeys through its own SQL runtime
principal. Its cloud encryption key is the versionless Key Vault URI
/keys/tourlab-azure-gateway-protection. Only Gateway receives access to that ring/key.

Reservations uses a separate cursor ring Blob
/tourlab-azure-dashboard-keys/cursor-ring.xml and its own versionless
/keys/tourlab-azure-dashboard-protection key. It cannot access Gateway's ring or wrapping key.
The ring storage holds encrypted data-protection key material; the Key Vault key wraps/unwraps it.
These are distinct from the JWT signing secrets, TLS certificates and business receipt documents.

Use the supported Azure Data Protection Keys/Blobs packages. Crypto SDK operations have a5second
network timeout and no SDK retries. Supported Data Protection APIs perform synchronous protection/
unprotection internally; do not wrap them in Task.Run or add fire-and-forget refresh. Cloud key grants,
wrapping/unwrapping and Blob/SQL persistence still require actual Azure verification. If wrapping or
unwrapping fails, protection/authentication fails closed; no volatile or plaintext-ring fallback is
configured. SQL-only health readiness is not proof of Key Vault permission.

Versionless wrapping-key references allow new Data Protection keys to use rotated vault key versions.
Existing encrypted keys record the version needed for decryption. Retain every prior wrapping version
still required by protected keys/cookies/cursors. Do not delete keys or the ring as a rotation shortcut.

Local Gateway supports an active PFX plus at most two previous decryption PFX entries under
DataProtection:PreviousCertificates (Path/Password) in its ignored private configuration. Active
certificates must be currently valid and have a strong RSA private key; previous certificates may be
retained for decryption. The real SQL test checks both public certificate envelopes and decrypts old
and new protected data after recreating providers. Local dashboard retains its existing separate
certificate-encrypted private ring. Actual process restart with the same certificate is verified
through the HTTPS BFF helper. No automatic certificate deletion/rotation operation was added.

## Least privilege and ingress

Read config/cloud/permissions.json and docs/cloud-security-checklist.md. Service Bus grants are
entity-specific and reflect the actual subscriptions/queues used by each worker, including currently
empty consumers. No namespace Data Owner or runtime Contributor-to-subscription grant is requested.
Cloud development fault/replay controls stay disabled, so they do not justify extra sender privileges.

Azure SQL generation requires an explicit owned database and11distinct nonempty managed-identity
OBJECT IDs (five runtimes, five separate migrators and Receipts). It generates contained external
users with SID/TYPE=E, validates existing SID/type, establishes distinct schema owners, grants own-schema
DML, denies runtime DDL/other-schema access and protects migration histories. No CREATE LOGIN/master
assumption or db_owner grant exists. The marker/EngineEdition/database guards must match the reviewed
Azure SQL target. Runtime identities are never migrators.

Receipts is a distinct principal in the Notifications-owned lock/runtime role, with additional object
denials: SELECT/UPDATE ReceiptJobs, INSERT ReceiptAttempts; no business-table/Outbox/Inbox access and
no job insertion/deletion. Reapply the artifact after migrations before activating workers so new
objects and migration-history denials are covered. The existing config/sql/azure-principals.sql is an
intentional refusal stub directing operators to explicit-ID generation; the old display-name draft
must not be executed as a deployment shortcut.

Cloud APIs require an exact AllowedHosts entry, canonical same-origin Gateway browser origin, HTTPS
owned backend endpoints and exact reviewed Ingress:KnownProxies IPs (at most8). Only one forwarded
protocol hop is trusted. Untrusted proxies cannot set HTTPS through a forged header, and HTTP is
rejected. Automatic ASPNETCORE_FORWARDEDHEADERS_ENABLED trust is forbidden. Do not guess platform
proxy IPs or enable broad forwarding to make login pass; the actual App Service ingress topology and
HTTPS-only setting require AZ-011 preparation/AZ-012 verification. This does not label public backend
network endpoints private. Backends retain independent JWT and ownership checks.

## Evidence limits

There is no Key Vault emulator. VaultSdkTests uses the real SDK with MOCKED HTTPS transport and fake
credential solely to verify the versioned GET, scope, disabled/expired checks and safe failures.
CloudSecurityTests/IngressSecurityTests establish structural and framework behavior only.
ProtectionSqlTests establishes real SQL encryption/permission behavior with locally arranged principals.
None establishes real managed identity, Entra token acquisition, cloud RBAC, Key Vault crypto, Azure SQL
external-user authentication, Cosmos auth or actual Functions identity-based host/listener access.
These are explicitly listed in cloud-security-checklist.md and remain NOT VERIFIED.

Official references checked2026-10-24:
[isolated Functions identity connections](https://learn.microsoft.com/azure/azure-functions/manage-connections),
[SQL managed identity authentication](https://learn.microsoft.com/sql/connect/ado-net/sql/azure-active-directory-authentication),
[Data Protection configuration](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview),
[Key Vault Secrets SDK](https://learn.microsoft.com/dotnet/api/overview/azure/security.keyvault.secrets-readme),
[CREATE USER SID/TYPE](https://learn.microsoft.com/sql/t-sql/statements/create-user-transact-sql),
[Cosmos data-plane roles](https://learn.microsoft.com/azure/cosmos-db/nosql/reference-data-plane-security).