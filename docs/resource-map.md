# Independent local resource map

Source and target are distinct siblings. No source volumes, networks, containers, databases, keys,
cookie names, .git directory or build output are reused. TCP bind checks passed on2026-10-23
before host startup; ports can change externally and preflight must be rerun.

| Target process/dependency | Loopback port(s) | Names / ownership |
|---|---|---|
| Gateway.Api | HTTPS9443 | identity schema planned; cookie __Host-tourlab-azure in002 |
| Catalog.Api | HTTPS9444 | catalog schema planned |
| Reservations.Api | HTTPS9445 | reservations schema planned |
| Payments.Api | HTTPS9446 | payments schema planned |
| Notifications.Api | HTTPS9447 | notifications schema planned |
| Angular development | HTTPS4201 | proxy same-origin API topology |
| Receipts.Functions | 7072 planned | Notifications-owned; host absent currently |
| Application SQL Server | 14339 ->1433 | new tourlab_azure database in002; SQL Developer only |
| Broker SQL Server | No host publication | separate engine/volume; no business tables |
| Service Bus AMQP/health | 5673 ->5672;5301 ->5300 | independent broker namespace; queues/topic prefixed tourlab-azure- |
| Azurite Blob/Queue/Table | 10010/10011/10012 ->10000/10001/10002 | fresh tourlabazure account/key; private tour-images/booking-receipts containers in006/008 |
| Cosmos vNext | 8089 ->8081 | gateway HTTP local simulation only; no explorer; tourlab-azure database/bookings container in007 |

Compose project tourlab-azure automatically scopes services, sql-data, broker-sql-data, blob-data,
cosmos-data and application/broker networks. No fixed container_name or globally fixed volume name.
AZURELAB_ configuration prefix; no cloud credential or endpoint override is selected by default.
App containers use internal8080 without host publication when built; native HTTPS is the debugging profile.
Dockerfile builds five separately named images; no AWS base image/state is imported.
This scaffold is not cloud-ready; AZ-009 adds fail-closed cloud identity configuration.

## Capacity and fidelity

Two SQL Server engines plus Service Bus, Cosmos, Azurite, .NET and Angular can consume substantial RAM.
Allocate at least12GiB to Docker as a starting study allowance and measure actual use; this is a planning
assumption, not a measured requirement or guarantee. Run dependency profiles only when needed.
SQL Developer licenses allow development/test, not production.
Service Bus emulator restart can lose messages/entities despite its SQL dependency volume.
No automatic republish of sent Outbox records is provided. Future reset/reseed/rebuild is explicit.
Cosmos emulator is a gateway-only subset and does not enforce cloud authentication.
The local HTTP emulator exceptions are loopback/private-network only; no TLS validation bypass exists.

## AZ-006 image storage ownership

Catalog uses private tourlab-azure-tour-images in the dedicated local tourlabazure Azurite account.
Container binding is fixed; object keys are server-generated tours/<tour-guid>/<random-guid>.
Azurite3.37.0 is digest-pinned in compose.yaml; loopback10010 Blob,10011 Queue,10012 Table,
dedicated tourlab-azure_blob-data. Only Blob is used by AZ-006; host/receipt storage is later work.
Fresh account key remains ignored/private; no source AWS bucket, source data, shared volume or
running source resource is used. Existing broker containers/volume are retained unchanged.
Real Azure account creation/public-access denial and container-scoped managed-identity RBAC
remain separately authorized future cloud verification; local account-key emulator is not that proof.

## AZ-007 Cosmos projection ownership

Reservations alone owns database tourlab-azure-dashboard / container bookings, partition /userId,
id=reservationId and declared departureUtc DESC,id ASC composite index. No default TTL.
Cosmos gateway uses loopback8089 via existing digest-pinned container tourlab-azure-cosmos-1 and
retained dedicated tourlab-azure_cosmos-data volume; explorer is disabled. Local .NET uses HTTPS
with the existing Azure lab CA/leaf, read-only certificate/key mounts and a fresh ignored custom
emulator account key. No original Dynamo table, volume, endpoint, credential or dataset is used.
The emulator's internal PostgreSQL implementation is not a business relational provider or shared DB.

Only Reservations DashboardWorker consumes existing dashboard-events (BookingSnapshotChanged);
workflow reservations-events excludes it. Existing broker containers/configured filter are reused.
Fresh relational/live profiles have separate credentials, SQL databases and Reservations cursor
certificate/key ring; Gateway Identity keys are not reused by Dashboard.
Real cloud account/container-scoped roles, composite-index enforcement and emulator restart
durability remain separate evidence. No cloud resources or unrestricted endpoints were provisioned.

## AZ-008 receipt ownership

Notifications owns ReceiptJobs/ReceiptAttempts/snapshots and their unique reservation/kind/sourceVersion
key in its own SQL schema. Receipts.Functions shares this owner and receives only that runtime
principal, never migrator, Catalog, Payments, Reservations or Identity credentials.

The only GenerateBookingReceipt effect handler is the actual isolated Functions trigger on existing
tourlab-azure-receipt-jobs. Native Core Tools binds127.0.0.1:9450; API ports remain9443..9447.
No broker/container topology restart or recreation was performed.

Business UTF8 documents use private tourlab-azure-booking-receipts in existing Azurite on10010.
Functions AzureWebJobsStorage is separately configured for standard azure-webjobs host coordination
containers using loopback10010/10011/10012. The existing dedicated local account is shared; its key
does not prove container-scoped RBAC. No business metadata is stored in host storage, no source S3
objects/credentials are used, and no Blob credentials reach Angular.

Fresh receipts-* and receiptshost-* SQL databases/private profiles and their data are retained.
The host profile is separate from regression Outbox staging. Only verification-owned process trees
are killed/restarted; the existing broker, broker SQL, Azurite and Cosmos containers/data remain.
## AZ-009 prepared cloud security ownership — no cloud resources created

Each of Gateway/Catalog/Reservations/Payments/Notifications/Receipts has a separate prepared
system-assigned identity. Azure SQL remains one lab database with owned schemas; explicit contained
principal object IDs and separate migrator identities are generated as a review artifact only.
Receipt identity shares the Notifications-owned lock role but receives additional table/immutable-
column restrictions. This does not grant access to other schemas or the application's Outbox/Inbox.

Gateway owns its Identity SQL protected ring, human-signing secret and Gateway wrapping key.
Reservations owns its quote-signing secret, Cosmos dashboard, separate cursor-ring Blob and wrapping
key. Catalog has its own image-container permissions. Notifications reads receipt documents;
Functions conditionally writes/verifies that receipt container and uses a different host-storage
account. Host storage is not a business database or receipt key ring.

Exact entity/container/key/secret scopes are prepared in config/cloud/permissions.json. Public
templates and the SQL generator assign no role and create no account, key, vault, database or endpoint.
All real resource IDs, proxy topology, RBAC/Entra access and deployment/lifecycle proof remain AZ011/012.
Existing local containers/volumes/data and all original AWS resources are retained.