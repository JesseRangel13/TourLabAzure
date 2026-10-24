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
