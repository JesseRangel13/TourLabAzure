# Initial decisions

D01 — Separate independent repository, selective read-only import. No dual-cloud switches in original; no live data migration.
D02 — Angular21 + NGXS is the required new frontend; Blazor stays solely in original. Angular19 compatibility is study documentation.
D03 — SQL Server owns transactions; Cosmos owns only a derived dashboard. Raw SQL/provider differences are explicitly ported and tested.
D04 — Service Bus queues for commands, topic/subscriptions for events. Retain application dedup and Outbox; no distributed SQL/broker transaction claim.
D05 — Blob private containers for tour images and simulated confirmation documents; server-streamed upload/download MVP.
D06 — Functions demonstrates real triggered processing through bounded receipt feature; SQL Saga remains unchanged in ownership, no Durable Functions rewrite.
D07 — BFF cookie boundary for browser; backend JWT validation and cloud managed identities solve different security problems.
D08 — Five independent API Web Apps may share a measured paid plan for learning cost; document shared compute limitations. Functions uses supported isolated hosting.
D09 — Local first, no automatic cloud mutation. Managed identity/RBAC/Key Vault have explicit cloud verification gates.
D10 — GitHub Actions remains initial pipeline; no parallel Azure DevOps implementation without requirement. Build scripts remain portable.

Codex must append evidence-backed ADRs for material changes, rather than silently overriding these decisions.

## AZ-001 decisions from source inventory (2026-10-23)

D11 — Source is the explicitly observed sibling Tour Microservices; implementation commands use the
Azure sibling, never the initial chat cwd. Local independent Git only; no remote/push/commit required.
No source code is copied in this foundation; reuse is decided by domain task after inventory.

D12 — Source CatalogQuoteClient.cs/current configuration and Azure02 specify total10s,
attempt3s, initial+2 retries, five consecutive failures/breaker30s and max10 calls.
An older review's40s statement conflicts with current implementation. Resolve in favor of Azure/current
code10s. No resilience implementation exists yet; verify in AZ-005.

D13 — Source images enforce2MiB/2048x2048/3 slots/two validations. Azure05's5MiB/5 slots are suggestions,
and it says reuse the actual source workflow. Preserve stricter existing limits in AZ-006.
Downloads deliberately change from short signed S3 URLs to authorized Blob streaming proxy.
No inventory, price or payment rule is changed.

D14 — Source contains Angular22 as an additional partial frontend. Do not import it or downgrade that
source. New Angular21+NGXS is independent. Preserve human behavior through parity mapping.
Source /auth/token is a credential/rate-limited API-tool endpoint; retain this backend boundary in002
without adding a browser JWT export/persistence feature.

D15 — Empty broker entities in001 are intentional scaffolding. AZ-004 owns typed filters/consumers.
Physical entities gain tourlab-azure- prefix; topic events are published once, not SQS fan-out copied.
No Azure receipt-processing success path exists before008.

D16 — Registry-verified Cosmos preview is pinned to immutable digest, with gateway HTTP bound only
to loopback/private network. Emulator authentication/RBAC, restart persistence and cloud TLS remain
separate acceptance evidence. SQL Developer and Service Bus licenses need explicit script switches.
Container engines are not automatically started or original resources modified to pass preflight.

D17 — Official current Functions docs use Azure.Functions.Sdk; use verified1.0.1 and worker2.52.0
for net10. Explicit generated-extension restore accompanies locked solution restore. No obsolete
in-process scaffold or direct-handler substitute is presented as actual Functions host evidence.

## AZ-002 decisions (2026-10-23)

D18 — AZ-001 has an actual independent PASS in docs/reviews/AZ-001.md; its old current-task file was
stale. Docker is still unavailable. Installed SQL Server2025 Express LocalDB17.0.4025.3 can provide
real SQL Server evidence in a new private tourlab-azure instance. It is not an in-memory substitute.
Use a new contained database and separate password-authenticated schema principals, not the owner's
integrated credential in runtime hosts. Named-pipe local connections are a Development-only profile;
Docker SQL2022 and actual Azure SQL/TLS/managed identities remain separate evidence categories.

D19 — AZ-002 ports persistence foundations (owned Inbox/Outbox, leases, concurrency, Identity and
Data Protection) and fresh SQL Server migrations. Domain tables/handlers arrive with AZ-003 onward;
do not prematurely implement business success paths. SQL rowversion is separate from future businessVersion.
Transport JSON uses checked nvarchar(max), UTC datetime2, explicit binary collation and unique indexes.
No automatic EF transaction retries duplicate external effects. Claim uses UPDATE OUTPUT with
UPDLOCK/READPAST/READCOMMITTEDLOCK so it is also valid with READ_COMMITTED_SNAPSHOT enabled.

D20 — Human JWTs are short-lived and audience-bound to each backend; Gateway signs each proxied
request from its validated cookie identity. The separate Reservations quote signer cannot create
human roles and Gateway never possesses that private key. The specified internal quote path remains
service-only and explicitly503 until AZ-003 implements it; no fabricated quote is returned.
All browser mutations (including login/logout and proxy mutations) enforce CSRF and allowed Origin.
API-tool /auth/token remains credential-based/rate-limited and rejects browser Origin/Fetch headers.

D21 — Use unique AZURELAB-XSRF-TOKEN / X-AZURELAB-XSRF-TOKEN names alongside
__Host-tourlab-azure and __Host-tourlab-azure-csrf. Cookies are not isolated by TCP port;
unique names protect concurrent localhost labs. Refresh the request token after login.

D22 — Initial restore detected current .NET crypto advisories. Verify Microsoft's release metadata,
install SHA-512-checked SDK10.0.402 privately under Azure.local/dotnet (runtime/ASP.NET10.0.13),
pin EF/Identity/JWT/EF tools10.0.13 and crypto Pkcs10.0.13, and update .NET container digests.
The original/global SDK remains unchanged. Do not disable NuGet audit or upgrade Angular.

## AZ-003 decisions (2026-10-23)

D23 — AZ-002 independent PASS is present in reviews/AZ-002.md. Its default SQL file collision and
Chromium trust gap remain environmental limitations, not reasons to weaken isolation. AZ-003
verifies a new tagged LocalDB database via Verify-Catalog.ps1 and preserves all existing SQL files/data.

D24 — Preserve inspected source CatalogOperations/InventoryHandlers/contracts and their invariants.
PostgreSQL advisory locks become finite transaction-owned SQL Server application locks scoped to
the Catalog runtime role. FOR UPDATE becomes UPDLOCK/HOLDLOCK; conditional UPDATE remains the
allocation boundary. UTC datetime2 conversion, filtered nullable QuoteId uniqueness, SQL decimal
checks and separate business versions are explicit. No provider-specific migration reuse.

D25 — Catalog HTTP DTOs stay in its Application namespace; only integration messages/envelope belong
in Shared.Contracts. Queries/commands/quote/inventory remain explicit concrete typed handlers.
Internal inventory has no public endpoints. Outbox intents and scoped expiry are implemented;
broker dispatch and Inbox consumption remain AZ-004, fault selection AZ-005. This is not a claim
of exactly-once message delivery.

D26 — Source Catalog has create/read/edit/activation, not destructive delete. Preserve inactivation
rather than inventing deletion of quotes/holds or historical sessions. Source quote snapshots
survive price/name updates while valid; inactivity still prevents new holds. No business-rule
contradiction affecting money/inventory was found; no accepted-price rule was silently changed.

D27 — CatalogState uses request sequence plus account generation guards and immutable updates.
Mutations are serialized in one feature store and never automatically retried. Forms are local;
Admin navigation is an aid, backend authorization remains mandatory. Signals render selectors;
NGXS and RxJS own shared state and cancellation. Known browser TLS failure is not bypassed.

D28 — Required JSON constructor fields uncovered BadHttpRequestException being mapped to 500
by the existing safe handler in Development. Classify it with its safe HTTP status and generic
InvalidRequest ProblemDetails; retain no parser/body detail in responses.
D29 — The source Catalog Outbox addresses SQS consumer queues, but specs/03-service-bus.md
requires one domain-events topic in Azure. All Catalog outcomes and TourSessionChanged intents
therefore target tourlab-azure-domain-events exactly once per logical event, not separate queues
or per-subscriber copies. This routing difference is resolved before activating a dispatcher.
AZ-004 will add the topic/subscriptions/filters and actual broker evidence; AZ-003 asserts only
durable intent destination, not delivery.
## AZ-004 decisions (2026-10-24)

D30 — AZ-003 independent PASS is confirmed. Port only service-owned domain/transport logic;
replace SQS fan-out with one Service Bus topic publish and filtered subscriptions. Preserve
durable provider/sender effects, idempotent booking and safe compensation guards internally.
Advanced fault/admin/cancellation controls remain AZ-005. No cloud operations are authorized.

D31 — Actual emulator 2.0.1 startup rejected a 14-day transport TTL: its maximum is one hour.
Use an explicit one-hour local broker/message TTL, as documented by Microsoft's emulator
quota/schema, while retaining SQL Inbox/idempotency/provider/tombstone records without purge
(at least 14 days). Transport TTL and business deduplication retention are distinct. Local TTL,
restarts and emulator persistence are not production durability proof. No existing broker accepted
traffic before this startup validation failed; restarting the failed new lab container loses no
accepted messages. Broker SQL volume is retained.
## AZ-005 decisions (2026-10-24)

D32 — AZ-004 independent PASS is present. Its prose mentions compensation after notification
failure, but authoritative source W-01/W-04 and Azure specs/02 explicitly preserve a Confirmed
booking while durable notification work retries. Notification failure is not a new refund rule.
No business rule is silently changed; source code/specifications govern this detail.

D33 — Cancellation is allowed only from Confirmed before departure, fixes the already assigned
refund ID and waits for both refund and release acknowledgements. Different request keys cannot
start another full refund. Admin retries continue from durable flags and reuse operation IDs.
Uncertain payments must be resolved through the fictional provider and actual reconciliation,
never by editing Saga state. Exact actors are audited in the owning schema.

D34 — Service Bus inspection uses non-locking peek, bounded to ten redacted metadata items,
with an explicit sequence cursor and commands/events selection. Bodies, credentials, lock tokens
and arbitrary entity paths never reach the browser. Replay uses a five-minute inspection and
60-second SQL owner lease, bounds search to 50 PeekLock DLQ items, sends to the original command
queue or original topic, and only then completes the original. Unrelated locks are abandoned.
Failures after acceptance retain the original/uncertain audit; no claim of atomic broker/SQL commit.

D35 — Each replay send gets a fresh transport MessageId, preserving the unchanged envelope's
logical MessageId, DeliveryId and operation IDs, with replay lineage headers. This avoids a
broker duplicate-detection window suppressing a deliberate resend; SQL Inbox/business IDs remain
mandatory. Topic replay can reach every matching subscription. Malformed, misrouted or older
than 14-day envelopes are not replayable. No Inbox clearing, new charge ID or payload editing.

D36 — Fault controls are backend Admin-only, bounded to 0..100 occurrences, durable and audited,
and fail closed unless Development plus explicit LabFeaturesEnabled. Captured provider/sender
modes remain attached to each operation. Pause controls retain work; no queue purge or reset of
historical data. Read the final resolved environment/configuration at DI resolution rather than
capturing preliminary WebApplicationFactory configuration.

D37 — Persist optional bounded W3C parent/state on each service's own Saga/payment/refund/
notification work record. Restore it in independent worker operations and propagate envelopes;
never use IDs as metric dimensions or record token/cookie/body/connection strings. Diagnostics
use bounded owned-schema metadata, durable counts plus explicitly process-local worker/duplicate
observations. Enabled messaging readiness performs a bounded non-locking real broker probe.
No Functions/cloud observability is claimed by the five API implementation.

D38 — Existing credential throttling remains ten requests/minute. The expanded synthetic HTTPS
test phases exceeded it once; the harness now waits one unchanged 60-second window before its
additional login phase. No production rate limit or assertion was relaxed.

D39 — Preserve existing broker containers and SQL/DLQ data. Use fresh SQL profiles, generated
service-owned operations/tracing migrations and existing pinned SDKs. Local emulator routing,
replay and worker shutdown evidence is separate from remote CI, Chromium and actual Azure.
Browser certificate verification remains mandatory; the known Chromium CA gap is not bypassed.

D40 — AZ-006 preserves the source image bounds (2MiB,three images/tour,2048x2048), which are stricter
than the Azure spec's suggested examples. Server-generated random Blob keys are independent of the
client's stable GUID intention. Authenticated proxy downloads replace S3 signed URLs; no browser
credentials/SAS/public access. Retain decoder/signature/CRC/ending validation and bounded concurrency.

D41 — SQL image intent commits before conditional immutable Blob PUT/HEAD. Ready completion checks
owner,lease expiry and Pending state. DeletePending retains upload lease; retained hourly-swept Deleted
tombstones remove uncertain late objects without resurrection. Scoped bounded SQL claims use
UPDLOCK/READPAST/READCOMMITTEDLOCK; no network in a transaction, broad purge or untracked object scan.
Persist attempt counts/history. See images-learning.md for failure and age/retry boundaries.

D42 — Pin Blob12.30.1,Core1.62.0,Identity1.21.0 forwarding facade and StbImageSharp2.30.16 after official
API/package verification. Local credentials stay in ignored restricted profiles and only target the
dedicated loopback Azurite account/container. Cloud is managed-identity/HTTPS preparation only;
container-scoped RBAC/public-account restrictions still require actual Azure evidence in later tasks.

D43 — Keep existing broker/data untouched. Verify images using fresh retained real SQL profiles and
actual SDK/Azurite, plus helper-owned Catalog stop/start recovery fixtures. Angular state/render tests,
browser discovery and Chromium/cloud verification are separate evidence. Known browser CA gap remains
NOT VERIFIED; no TLS bypass, cloud deployment, commit/push or resource destruction.

D44 — AZ-007 publishes a full BookingSnapshotChanged atomically with creation and every existing Saga
Record transition. Existing numeric Reservation.Version is the monotonic businessVersion, separate
from SQL rowversion bytes. ProjectionSequence is a new indexed SQL identity for rebuild keyset only.
Accepted price/name/departure/participants remain immutable. All snapshots use the existing single
domain-events topic and existing explicit dashboard-events filter; no duplicate workflow handler.

D45 — Preserve original dashboard maximum20 rows and4KiB cursor input, with15minute owner/size/query
binding and a separate Reservations certificate-encrypted Data Protection ring. Azure spec05 overrides
the source creation-key ordering with departure DESC,id ASC. The pinned emulator ignored MaxItemCount;
use server TOP(size+1), bounded keyset continuation and four SDK response pages instead of weakening
page-size assertions. Native SDK continuation behavior/composite-index enforcement are not claimed.

D46 — Actual Cosmos SDK gateway writes point-read version/ETag, create with409 re-read or replace
with IfMatchEtag/412 re-read, at most five attempts within20s. Old/equal-safe versions do not regress;
contradictory equal or changed immutable fields are poison. Validate read owner/key/canonical UTC/
money/status/version before returning data. Never unconditional Upsert or SQL Inbox-before-Cosmos.
Dedicated PeekLock worker completes only after durable Cosmos success or verified safe no-op.

D47 — Admin rebuild reads only bounded Reservations SQL snapshots via protected run/position/upper
bound cursor, audits actor, and transactionally enqueues same-type Outbox work with stable retry keys.
Live changes race rebuild safely through the same ETag/version path. No container/Inbox reset or
cross-schema read. Source /v1/my-bookings read/rebuild actions map to new explicit dashboard API/UI
paths; /v1/reservations remains authoritative and separate. Projection outage returns safe503/traceId;
Angular explicitly labels lag and never substitutes SQL results for a Cosmos success.

D48 — Use existing digest-pinned vNext emulator in HTTPS/gateway mode with the Azure lab CA/leaf and
fresh private custom emulator key. SDK3.63.2 plus explicit Newtonsoft.Json13.0.4 are pinned/locked;
System.Text.Json is still used for documents. vNext auth/composite-index enforcement, RU/cloud RBAC/
managed identity and restart durability remain NOT VERIFIED. No paid account or TLS bypass.

D49 — Preserve existing broker/Azurite data and containers. Isolate relational staged work from a
fresh live SQL profile before broker/host tests. Consume retained legitimate lab events through the
normal projection path, never discard/purge. Keep poison in built-in dashboard DLQ and support repair
through explicit version-safe rebuild. Stop/restart only helper-owned API process trees.

D50 — AZ-008 extends version1 ReservationConfirmed additively with optional ConfirmationSnapshot.
All new producer confirmations include accepted name/departure/participants/MXN cents, confirmed UTC
and the exact next Reservation.Version committed by the same transition. Legacy events remain valid
for durable simulated notification delivery, record LegacyConfirmationWithoutSnapshot once, and do
not fabricate historical receipt data. This resolves the pre-AZ008 contract gap explicitly; no
cross-schema lookup or weakening of existing delivery assertions. Snapshot contradictions are poison.

D51 — ReceiptJob, immutable snapshot, normal notification and GenerateBookingReceipt Outbox commit
with Notifications Inbox acceptance. Unique reservation/kind/sourceVersion protects the logical job.
Functions belongs to Notifications and references only its owned receipt application/SQL model.
No business EF entities or rules move into Shared.Contracts/Shared.Infrastructure.

D52 — Explicit SQL lease owner/version/expiry brackets conditional Blob creation outside transactions.
Deterministic UTF8/key/SHA256, bounded size/MIME/ETag-bound actual-byte verification protect duplicates.
Ready commits before manual broker Complete; conflict preserves the object and persists safe audit.
Transient failure leaves durable retry work; no Inbox-before-Blob shortcut or delivery exactly-once claim.
The60s SQL lease and renewed broker lock permit bounded90s busy-owner wait after crash.

D53 — Use real Core Tools4.16.0/runtime4.1054.250.26428, existing SDK1.0.1/Worker2.52.0 and pinned
ServiceBus extension5.24.0. Worker indexing means functions.metadata is not required. Functions uses
separately configured AzureWebJobsStorage in the existing lab Azurite account, standard host containers,
and a distinct private booking-receipts business container. Runtime sees Notifications SQL only.
Local account-key/ServiceBus connection settings are Development-only; cloud identity is AZ009/012.

D54 — Owner/Admin Notifications receipt status/download plus Gateway attachment enforce independent
backend authorization, other-owner404, no-store/nosniff and bounded verified bytes. Angular/NGXS keeps
status metadata only, resets on user changes/401 and ignores stale loads/downloads; transient browser
URLs are revoked. Historical confirmation and current authoritative SQL state are visibly separate.
Documents are simulated educational confirmations, not actual payment or legal tax receipts.

D55 — Actual private Development-only crash barrier pauses one selected job after Blob verification,
with no HTTP crash endpoint. Verification kills only its own Functions child tree and tests actual
redelivery, ETag preservation, duplicate and poison/conflict DLQ. Fresh retained profiles separate
SQL regression staging from live workers. Existing broker/Azurite/Cosmos/source resources stay intact.
No cloud deployment, data purge, browser TLS bypass, commit/push or independent AZ008 review claim.