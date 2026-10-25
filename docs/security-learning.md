# AZ-009 security walkthrough

The browser's identity and an application's workload identity solve different problems. Identity
cookies say which human is using Gateway; backend JWTs carry that person's bounded role/owner claims.
Managed identity lets one app access its own Azure dependencies. A workload token does not turn Bob
into Alice, remove backend JWT validation, or make an exposed backend private.

Follow the implementation in Shared.Infrastructure/Security:

1. AzureSecurity validates the selected local/cloud mode, service-owned endpoints, strict SQL,
   Functions platform settings, storage separation and explicit proxy trust before secret retrieval.
2. StartupSecrets.Validate is offline. LoadAsync performs one allowlisted versioned signing-secret
   GET only for Gateway or Reservations, with cancellation and a public fingerprint check.
3. SigningMaterial loads local files or the already retrieved in-memory PEM. New JWTs carry a public
   fingerprint key ID. Cloud validators allow at most two public keys during a deliberate rollover.
4. Gateway keeps its encrypted Data Protection ring in Identity SQL. Key Vault wraps that ring's keys;
   it does not store the cookie itself. Reservations has a separate cursor ring and wrapping key.
5. AzureSecurityPlan produces reviewable external-principal SQL from explicit IDs. Runtime grants,
   schema isolation, application locks and migration privileges remain separate.

A startup secret cache is intentionally not an automatic rotation mechanism. A pinned secret version
and expected fingerprint make each release reproducible; validators need the new public key before
the signer starts using it. Data Protection uses a different policy: versionless wrapping-key URI for
new keys, but each encrypted key preserves the version needed for old protected data. Deleting an old
wrapping version can destroy the ability to decrypt valid cookies even if the latest key works.

The SQL permission test impersonates a restricted local user and processes a real receipt. That proves
the actual SQL engine accepts the required SELECT/UPDATE/INSERT/app-lock operations and rejects other
tables. It does not prove Entra resolves a managed identity to that user in Azure. Likewise the mocked
HTTPS SDK test verifies a GET path and token scope, not actual Key Vault RBAC.

Practice explaining:

- Why does Active Directory Default violate this production credential-selection policy?
- Why are public URLs and public SPKI keys ordinary configuration, while private signing PEM is a secret?
- What fails if the Functions host never receives ReceiptBus__fullyQualifiedNamespace?
- Which role covers host coordination storage, and why is it separate from receipt write permission?
- Why does retaining a SQL key ring without its wrapping key fail to preserve login across restarts?
- Why can't an untrusted X-Forwarded-Proto header establish that a request arrived over HTTPS?
- Which checks remain impossible to establish with LocalDB/Azurite or an offline configuration PASS?