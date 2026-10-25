# AZ-009 cloud verification prerequisites — NOT VERIFIED

This is a review/checklist artifact. No command in AZ-009 provisions resources, assigns roles,
retrieves real vault secrets, authenticates to Azure SQL or deploys code. Actual execution requires
the separate AZ-012 authorization and the AZ-011 hosting/cost review. Offline tests cannot mark
any of the cloud rows below passed.

| Capability | Prepared artifact/path | Required actual Azure evidence |
|---|---|---|
| Target ownership | lab name guards; future AZ-011 tagged IaC | exact subscription/tenant/resource IDs/region/tags; no original AWS resources |
| Workload identity | explicit system MI for each of six hosts | correct principal OBJECT ID per app; token audience; no developer fallback |
| Azure SQL | strict MI runtime strings; explicit-ID principal generator | contained external users authenticate; effective schema/DDL/history denials, inherited/public grants and app locks |
| Receipt SQL least privilege | receipt-specific object denials; real local SQL role test | actual Function identity can finish a job/audit but cannot read business/Outbox/Inbox/other schemas |
| Migration identity | five separate bounded schema migrator principals | one-shot migrations with runtime off; runtime never inherits migration permissions; rerun denials afterward |
| Gateway signer | exact versioned human-signing secret + public fingerprint | Gateway get succeeds; other workloads denied; expiry/disabled/403/rotation/startup behavior |
| Reservations signer | exact versioned quote-signing secret + public fingerprint | Reservations get succeeds; Catalog receives public keys only; other apps denied |
| Gateway protection | SQL ring + versionless Gateway wrapping key | wrap/unwrap and restart/cookie/CSRF survive; prior versions remain usable; other identities denied |
| Dashboard protection | separate key-ring Blob/container + wrapping key | persistent protected pagination across restart/scale; Gateway and unrelated apps denied |
| Blob images | container-only Contributor for Catalog | upload/read/delete recovery; anonymous access/account public access disabled; other identities denied |
| Receipt documents | custom read/write Function; read-only Notifications | create/hash-check/read permitted; delete/other-container/account-key operations denied |
| Functions host | separate host account; host Storage Blob Data Owner | host storage/listener identity works before worker startup; business storage is not host storage |
| Service Bus | per-entity Sender/Receiver map | intended routing/settlement/DLQ with MI; forbidden entities denied; no shared-access-key fallback |
| Cosmos | container-scoped data-plane role for Reservations | actual metadata/item/query permissions; ETag/version logic; unrelated account/container denied |
| Ingress/TLS | exact host/origin/proxy validation; no automatic trust | verified App Service proxy topology, HTTPS-only, correct forwarded scheme; forged headers/direct HTTP fail |
| Network exposure | authenticated backends; future AZ-011 access plan | actual public exposure documented; optional private networking separately priced/authorized |
| Diagnostics | safe codes/no secret response values | inspect App Service/Functions/SDK logs, traces, failures and deployment artifact retention for secrets |
| Signing rotation | two-public-key allowlist + pinned signer restart | coordinated rollout, unknown kid rejected, old tokens expire without permissive fallback; vault disable alone does not revoke cached signing RSA |
| Key retirement | retain wrapping versions/ring | do not delete versions needed by extant cookies/cursors/key ring; backup/restore and recovery evidence |
| Cloud lifecycle | no deployment/CD in this task | selected cost profile reviewed; explicit deployment, health/smoke/rollback and retained-data decisions |

Before any authorized cloud operation, compare all six configurations together:
Gateway's expected signing fingerprint must occur in every backend's human-key allowlist;
Reservations' quote fingerprint must occur in Catalog's quote-key allowlist. Backend URLs/origins
must match the actual apps; SQL server/database and Service Bus namespace must be the intended shared
lab resources; Notifications and Functions must reference the same receipt container, while Functions
host storage uses a different account. Offline per-workload validation does not establish these
cross-file deployment facts or cloud ownership.

Resolve placeholders using actual reviewed metadata. Principal IDs are managed identity OBJECT IDs,
not application/client IDs. SID/TYPE creation bypasses directory name validation; verifying those IDs
against the intended apps is therefore mandatory before executing the reviewed artifact. Never point
the LocalDB provisioning script at Azure, and never reuse the source repository's secrets or accounts.

Functions host settings are platform settings, not merely worker configuration. Validate the
unprefixed ReceiptBus__/AzureWebJobsStorage__ entries before host activation. The minimum documented
host role is Storage Blob Data Owner on its separate host account for this Service Bus-triggered app;
Blob-trigger/Durable/deployment-storage requirements must be reassessed if added in AZ-011.
No broader Storage Account Contributor or Service Bus Data Owner grant is implied.

Public endpoints stay HTTPS with platform certificate/hostname validation. Local lab CA, emulator
keys, SAS/access keys, local certificate passwords and test barriers are rejected in cloud settings.
No real Azure negative permission test or paid resource is necessary to finish the local AZ-009 task.