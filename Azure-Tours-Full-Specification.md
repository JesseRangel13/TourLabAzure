# Azure Tours Companion — Full Specification

Version 1.0, 2026-10-23. Generated from the individual documents; edit their sources consistently.


---

Source: README.md

# TourLab Azure + Angular companion

Specification version 1.0 — 2026-10-23. This is a NEW, independent educational project, not an in-place migration of the AWS/Blazor project. Target: practical preparation for an assignment starting after November 5, 2026. No promise that all cloud verification will fit before that date.

## Start here
1. Create a new empty sibling directory and open it in Codex. Keep the original project available through an explicit READ-ONLY source path.
2. Extract this pack into that new directory, preserving AGENTS.md, specs/, tasks/, prompts/, and docs/.
3. Run prompts/00-BOOTSTRAP-CODEX.md. Codex inventories the original's actual features and establishes isolation; it must not edit the original.
4. Implement ONE task at a time using its prompt under prompts/implement/.
5. After each task, give Antigravity prompts/REVIEW-ANTIGRAVITY.md with the task ID. Resolve blocking findings before progressing through dependent work.
6. Use prompts/TEACH-ANTIGRAVITY.md to study the resulting code in short lessons.

## Stack
.NET 10 ASP.NET Core Web APIs; EF Core 10 with SQL Server; Angular 21 + TypeScript + RxJS + NGXS; Azure Service Bus; Azure Blob Storage; Azure Cosmos DB for NoSQL; Azure Functions isolated worker; Azure Key Vault and managed identity; Playwright Test (TypeScript); GitHub Actions; Bicep for Azure IaC.

Angular 21 is intentional to match the assignment, even if newer majors exist. Angular 19 is a comparison topic, not a second frontend implementation. Preserve compatible pinned versions after checking official sources.

## Authority
AGENTS.md defines working boundaries. specs/ define intended behavior. tasks/ define incremental scope. Codex must inventory the CURRENT original repository, including S3/DynamoDB and CI extensions if present. The original specification archive informed this pack but is not evidence that current code or cloud deployment passed.

Priority for baseline behavior: explicit user instructions > these Azure specifications > actual approved original business specifications. Record contradictions; never silently weaken invariants or copy AWS deployment rules into Azure.

## Delivery order
AZ-001 isolation/scaffold; AZ-002 SQL Server/Identity; AZ-003 Catalog + Angular/NGXS shell; AZ-004 Service Bus + success slice; AZ-005 full Saga/API/UI parity; AZ-006 Blob images; AZ-007 Cosmos dashboard; AZ-008 receipt Functions; AZ-009 managed identity/Key Vault readiness; AZ-010 Playwright/CI; AZ-011 App Service/Bicep preparation; AZ-012 explicit deployment (separately authorized).

Read specs/07-acceptance.md and tasks/implementation-plan.md for scope and gates. Local functionality comes before cloud spending. All payments, notification deliveries, and booking receipts are explicitly simulated.


---

Source: AGENTS.md

# Agent instructions — Azure companion only

## Mandatory isolation
- Work exclusively in the new Azure repository. The original AWS/Blazor repository is READ-ONLY, including its specs, lockfiles, git state, databases, queues, images, and scripts.
- Do not convert the original repository or add Azure switches to it. Do not use a shared worktree, symlinks, junctions, shared build outputs, shared writable volumes, or shared resource names.
- Copy selected source into the new repository only after inventory. Do not copy .git, bin/obj, node_modules, .local, .env, certificates/private keys, connection strings, provisioning credentials, auth state, test reports, Terraform state, or generated artifacts. Review tracked config for embedded secrets too.
- Generate fresh local credentials, signing keys, Data Protection keys, cookie names, ports, service names, queue names, databases, and storage resources. Treat copied AGENTS.md and AWS-specific instructions as source context, not new-project authority.
- Before and after each task, record source commit and relevant source working-tree/content fingerprints read-only; do not alter pre-existing user changes. Flag externally changed files without accusing this task of changing them.
- No pushing, creating remote repositories, cloud provisioning, deployment, destructive purge, or deleting either project unless the user explicitly authorizes that operation.

## Implementation
Use English. Implement only the active task plus necessary fixes. Reuse domain logic where valid; replace provider-specific infrastructure deliberately. Do not recreate the app as a monolith, invent generic CRUD in place of workflows, or leave TODO success paths.

Use real SQL Server for relational integration tests; not SQLite, EF InMemory, or PostgreSQL. Cosmos/Service Bus/Azurite emulator tests and real Azure verification are separate evidence categories. Never substitute in-memory storage and call the integration passed.

Pin available compatible SDK/package/tool/container versions; keep lockfiles. Verify official docs when using changing APIs. No automatic upgrade from Angular 21. Use explicit commands and cancellation. No external real payments or email. No AI itinerary or additional product scope.

Preserve security on every API, not only the gateway. Do not store browser auth tokens or sensitive data in NGXS persistence. Use async scopes and per-operation DbContext. No cross-schema business access. Shared libraries contain plumbing/contracts, not business entities.

## Review and evidence
Add task-level evidence: changed files, acceptance IDs, exact commands/results, observed failures, limitations, and next steps. Mark unavailable checks NOT VERIFIED. A written test is not an executed test; a successful build is not proof of a working deployment. Do not mark independent review as passed yourself.

Antigravity reviews without modifying production code. Record defects separately from environmental gaps. Do not repeatedly retry known sandbox/tool failures or disable security to obtain green results.

Cloud work is AZ-012 only after explicit authorization. AZ-011 writes and validates IaC; it does not run az deployment create, publish, or mutate Azure.


---

Source: specs/00-parity-and-isolation.md

# 00 — Source inventory and parity

## Required inventory
Before porting, inspect the original repository and produce docs/source-inventory.md and docs/parity-matrix.md. Source path is a real user-supplied/observed path; do not guess or recursively inspect unrelated directories. If unavailable, scaffold the new repo from this pack, document the blocked inventory, and ask for the source path before claiming a faithful port.

Capture commit, dirty-state summary, specs/tasks/evidence paths, actual services, HTTP contracts, message contracts, SQL provider-specific code, UI actions, tests, fault controls, CI, S3 and DynamoDB implementations. Preserve pre-existing dirty source code as read-only context; never clean/reset it.

Parity matrix columns: original capability and file/contract, Azure task, new endpoint/UI/code, compatibility decision, acceptance ID, implemented status, locally verified status, Azure verified status, reviewer evidence. Enumerate every human action, not only happy-path CRUD. Explicitly classify original planned/absent/unverified features rather than claiming they existed.

## Baseline minimum
- Login/logout/current user; Alice/Bob Tourist and Admin; ownership and role enforcement; TLS and antiforgery.
- Tour/session CRUD, activation, prices/capacity, quote snapshots, atomic holds, expiry, confirmation/release.
- Reservation creation/idempotency, list/detail/timeline, cancellation; persistent orchestration and concurrency.
- Fake charge/refund operations with independent durable provider effects; reconciliation and admin resolution.
- Fake notifications, persisted receipts, retry and queries.
- Outbox/Inbox, duplicate/out-of-order processing, failure modes, circuit breaker/resilience, tracing, health, diagnostics, DLQ inspection/replay.
- Tour images, upload/removal/recovery; user booking read projection and rebuild.
- Full human UI coverage and meaningful automation; restart/multi-instance behavior.

Do not migrate original database contents or reuse its live storage. Seed new synthetic data with fake identities only. Contracts may retain original resource shapes where possible; document necessary Azure/SPA route differences.

## Isolation acceptance
A fresh checkout must run independently of the original. Compose project defaults to tourlab-azure with AZURELAB_-prefixed configuration. Allocate and document a checked port map, e.g. gateway 9443 and Angular development 4201, adjusting only after checking conflicts. Avoid fixed container_name. Use different volumes, networks, SQL database names, emulated entities, cookie names, project ID tags, and secret files. Both projects should be runnable concurrently where host RAM permits; document heavy emulator requirements honestly.


---

Source: specs/01-architecture.md

# 01 — Architecture and technology mapping

## Processes and ownership
- Gateway.Api: ASP.NET Core Identity, same-origin Angular hosting, BFF cookie boundary, YARP/API routing.
- Catalog.Api: tours/sessions/quotes/inventory; image metadata and Blob object management.
- Reservations.Api: reservation API, durable Saga, versioned Cosmos projection publisher/worker and rebuild.
- Payments.Api: payment API, fake provider and reconciliation workers.
- Notifications.Api: fake notifications and sender; booking receipt status/download API.
- Receipts.Functions: one .NET 10 isolated-worker Functions application processing receipt commands, owned by Notifications. Its code shares only Notifications-owned application logic; it does not query other service schemas.
- web/tours-angular: Angular 21 SPA, NGXS state and typed HTTP clients. Production assets served by Gateway to keep one origin; local development uses Angular's proxy.

Use folders within services or a modest layer split; avoid assembly proliferation. Shared.Contracts contains transport DTOs/envelopes; Shared.Infrastructure contains technical plumbing. Each domain owns its DbContext, migrations, runtime principal, and business types.

## Mapping
| Original | Azure companion |
|---|---|
| .NET 10 APIs | .NET 10 ASP.NET Core Web APIs |
| Blazor UI | Angular 21 + NGXS |
| PostgreSQL/Npgsql | SQL Server + EF Core SqlServer; Azure SQL Database in cloud |
| SQS/ElasticMQ | Azure Service Bus queues + topic/subscriptions; official emulator locally |
| S3 | Blob Storage; Azurite locally |
| DynamoDB | Cosmos DB for NoSQL; verified supported emulator locally |
| Fargate/Terraform | App Service + Functions; Bicep |
| AWS secret delivery | Managed identity + Key Vault for actual secrets |
| Existing browser tests | Playwright Test TypeScript targeting Angular |

## Local profile
Use Docker Compose for application SQL Server, Service Bus emulator (with its OWN documented SQL dependency), Azurite, and Cosmos emulator. The emulator's infrastructure database is not a business database. Pin image versions/digests after availability verification. Require explicit SQL developer-license acceptance. SQL Server Developer is development/test only; Azure SQL is the cloud target.

Prefer native dotnet/Angular/Functions host processes during debugging; also provide buildable container images for the five APIs. Use gateway mode when required by Cosmos emulator. Bind insecure emulators to loopback/private Docker network, never public interfaces. Local-only endpoint/credential overrides must fail closed in Azure configuration.

Emulators are not production replicas: Service Bus emulator restart can lose queued data; Cosmos vNext has limited APIs and may not enforce authentication. Test app-process restarts with broker running. Do not claim broker durability across emulator restart, or IAM/RBAC proof from emulator tests. Do not republish all already-sent Outbox entries automatically to conceal a broker reset. Provide an explicit reset/reseed/rebuild procedure.

## Cloud target
Five distinct Web Apps on App Service, initially allowed to share one paid App Service plan for study costs, plus a separate isolated Functions app on an appropriate plan. Sharing a plan shares CPU/memory and failure domains; it is not independent compute isolation. Measure capacity and use Always On for durable workers. Free App Service is not the reference for unattended workers.

Choose Linux .NET 10 code hosting when supported by the selected region/runtime; if unavailable, record an ADR and use verified supported .NET 10 container hosting rather than downgrading. For Linux .NET 10 Functions use a supported plan such as Flex Consumption, not classic Linux Consumption. Recheck platform support before implementation/deployment.

Only Gateway is a human entry point. Backend Web Apps still have backend JWT validation and ownership checks. Use unique short-lived gateway service credentials per backend request where needed; protect worker-only/admin routes. App Service system ingress exists: configure forwarded headers only for the documented trusted platform topology and reject untrusted claims. If low-cost connectivity cannot network-isolate backend apps, document their public network exposure with mandatory authentication; do not call them private. Private endpoints/VNet integration are an explicitly priced optional hardened profile, not hidden defaults.

Do not add Kubernetes, API Management, Azure Container Apps, Redis, or Entra end-user login to the required implementation. Explain these as future comparisons only.


---

Source: specs/02-sql-and-domain.md

# 02 — SQL Server and preserved business behavior

## Persistence and isolation
One NEW physical SQL database with catalog, reservations, payments, notifications, identity schemas and separate runtime users/roles; no cross-schema joins or foreign keys. Restrict schema-level DML/execute as required, remove accidental public grants, and test effective access including inherited permissions. Azure SQL contained users and managed-identity principals differ from local SQL logins: provide idempotent provisioning for both, never server-login SQL as an assumed Azure SQL capability.

Privileged migrator/provisioner is separate and never supplied to business runtime. Each schema has its own EF migration history. Fresh SQL Server migrations replace PostgreSQL migrations in the NEW project; preserve the source migrations untouched. No cross-provider migration reuse. Use Microsoft.EntityFrameworkCore.SqlServer compatible with EF10/.NET10. Use bounded connection pools and proper disposal. Cloud TLS verifies certificate/hostname; local self-signed SQL options must be explicitly development-only.

Port every provider-specific behavior: SKIP LOCKED/RETURNING/ON CONFLICT, JSON types, timestamp types, case sensitivity, GUID order assumptions, generated values, PostgreSQL error codes, index filters and migrations. Use datetime2/UTC consistently and integer MXN cents with checked arithmetic. SQL rowversion is a concurrency token, not a date or globally ordered business event version.

Implement SQL Server-safe atomic claims with tested locking/UPDATE OUTPUT or equivalent leases, ownership and expiry; do not transliterate PostgreSQL syntax. Publish outside database transactions. EF retry execution strategies and explicit transactions need a coordinated retry/idempotency design; a whole transaction retry must not duplicate external effects. Handle unique violations after proper rollback. Test schema permissions against real SQL Server, not just configuration inspection.

## Core rules
Participants 1..6; future tour session; quote validity 5 minutes; seat hold 10 minutes. Accepted price is a snapshot. Inactivation prevents new holds. Capacity cannot fall below occupied seats. Atomic available>=quantity update wins the last seat; confirmation does not decrement again. Expiry/release restores seats once. Release-before-hold creates a retained tombstone.

Creation: validate and obtain an idempotent quote outside the transaction. Catalog unavailable => 503 before Saga/payment. Persist HTTP idempotency record, reservation, initial Saga, history, and HoldSeats Outbox intent atomically, then 202. Same key/content returns the original result; conflicting content =>409. Unique constraints handle concurrent requests. Ownership violations =>404; backend Admin checks mandatory.

States: AwaitingAvailability, AwaitingPayment, PaymentUncertain, AwaitingConfirmation, Confirmed, Compensating, CancellationPending, Cancelled, Failed, ManualReview. Every transition checks state, IDs, and concurrency. Preserve original timeout configuration (availability/confirmation 30 seconds, payment resolution 2 minutes bounded by hold expiry) unless an explicit original approved rule differs.

Success: hold -> charge -> confirm -> notification. Decline: release -> Failed. Charge succeeded but confirmation fails: refund and release, finish Failed only when both are confirmed. Cancellation from Confirmed before departure: one stable refund ID plus release; Cancelled only when both complete. Repeated cancellation, even with new request keys, cannot produce another full refund.

Timeout after charge is Unknown, not Declined. Fake provider effect commits independently of application outcome. Reconcile using the same operation ID. Retain fake modes Success, Decline, TimeoutAfterCharge, UnknownUntilAdminResolution, RefundTransientFailure (two failures then success), captured per operation. Bounded reconciliation transitions to ManualReview when still unknown. Admin resolves only the fake-provider result then invokes reconciliation; never edits Saga directly.

Late charge success after abandonment => refund, never resurrect Confirmed. Late hold => release, never charge. Durable compensation flags tolerate any response order. Consistent lock order and concurrency prevent timer/handler races. Retain deduplication/tombstones at least 14 days; replay beyond that boundary is not automatically safe.

Notifications remain simulated. Inbox commit creates durable Pending work; sender failure does not disappear behind deduplication. Durable sender receipt uniqueness protects the business effect after crash/retry.

## Resilience and diagnostics
Preserve bounded quote HTTP resilience: per-attempt 3s, initial+at most 2 retries, total10s; backoff+jitter; five consecutive qualifying failures open per-instance breaker30s; max10 concurrent calls. Do not silently substitute a ratio-based breaker. Distinguish cancellation, decline, timeout, and dependency failure; avoid multiplied retry layers.

Preserve structured logs, trace propagation, low-cardinality metrics, separate live/ready health, persisted timeline, Admin fault modes, reconciliation/compensation retry and DLQ tools. Show simulated effects explicitly. No secret, signed URL, cookie, JWT, or payment-like sensitive content in logs.


---

Source: specs/03-service-bus.md

# 03 — Azure Service Bus

Use Azure.Messaging.ServiceBus, not legacy SDKs. Local official emulator, cloud managed identity with entity-scoped Data Sender/Data Receiver roles.

## Explicit routing
Commands use four domain queues catalog-commands, reservations-commands, payments-commands, notifications-commands, plus receipt-jobs for Functions. Domain events publish to ONE domain-events topic. Subscriptions catalog-events, reservations-events, payments-events, notifications-events, dashboard-events have explicit event-type filters; do not leave an unintended match-all rule. Each queue/subscription has the built-in DLQ, not a simulated separate queue treated as Service Bus's built-in behavior.

Generate a versioned routing document from the actual imported message inventory. E.g. HoldSeats/ReleaseSeats/ConfirmSeats -> catalog command queue; ProcessPayment/RefundPayment -> payments queue; seat/payment outcome events -> reservations subscription; terminal reservation events -> notifications; BookingSnapshotChanged -> dashboard subscription. There must be only one active handler path for each intended business effect. Events with multiple recipients use subscriptions; commands are not broadcast.

Envelope: EventId/command ID, type, schemaVersion, correlation/causation IDs, traceparent/tracestate, UTC timestamp, stable operation identifiers, and payload. No bearer tokens. Transport MessageId initially equals logical message ID and stays stable across publication retries. One topic publish is one Outbox delivery; do not retain SQS per-destination fan-out AND publish that event multiple times to a topic.

## Settlement and crash windows
PeekLock/manual settlement, not ReceiveAndDelete. For SQL consumers: Inbox + effects + history + Outbox local transaction, commit, then Complete. Failure before commit => retry. Crash after commit before Complete => duplicate no-op on redelivery. Lock loss is not permission to roll back an already committed business effect. Bound concurrency, lock renewal and retry budgets; gracefully stop processing.

Outbox publisher claims by durable lease, sends outside SQL transaction, marks sent only after broker acceptance with ownership check. Repeated send before mark is expected. Broker duplicate detection, where configured, is an optimization; business operation IDs/Inbox remain mandatory. MaxDeliveryCount initially5, configurable lock duration and bounded renewal, document size/TTL/retention limits and dedup retention. Domain deadlines remain durable SQL timers; do not rely on unsupported emulator scheduled-delivery features.

## DLQ and replay
Inspect bounded, redacted message samples. Distinguish peek (non-locking view) from receive (lock/settlement). Malformed unsupported messages are explicitly dead-lettered with safe reason; transient errors retry. Audit actions and restrict Admin.

Replay sends to the ORIGINAL queue/topic destination, confirms broker acceptance, then completes the DLQ message. Preserve logical EventId/operation IDs and record replay lineage. If broker duplicate detection would suppress the resend, use a new transport MessageId while preserving the logical EventId; never clear Inbox or create a new business operation to bypass business deduplication. Replaying a topic DLQ may redeliver to other subscriptions: route through the original topic with filters and rely on logical dedup, document/test that behavior. Do not promise only one subscriber sees replay unless a separately designed targeted replay path exists.

## Emulator evidence
Verify current emulator version limits for filters, sessions, transactions, management operations and persistence. Do not assume emulator supports Entra identity, AMQP WebSockets or durability across restart. Keep the emulator's SQL dependency isolated. Missing cloud capabilities are explicitly NOT VERIFIED locally. No paid namespace is required for local tasks.


---

Source: specs/04-angular-auth-ngxs.md

# 04 — Angular 21, NGXS, APIs, and authentication

## Hosting and identity
Implement a new Angular21 SPA; do not embed Blazor or modify the original frontend. Pin compatible Node (e.g. a verified Node22 LTS patch), TypeScript, RxJS, Angular CLI and NGXS using official peer dependencies; commit package-lock and use npm ci. Do not silently install Angular22 or NGXS latest. Use standalone components, typed reactive forms, dependency injection, typed HttpClient services, RxJS with correct cancellation/disposal and clear templates. No elaborate design system required.

Gateway hosts production SPA assets and API/auth routes on the same origin; local Angular proxy mirrors that topology. SPA fallback cannot swallow /api, /auth, health or static asset errors. HTTPS local, fresh dev credentials/certificates, no permissive production CORS.

Keep Identity Tourist/Admin seeded users with fresh passwords privately provisioned. Browser uses a Secure/HttpOnly appropriate SameSite cookie, not a stored JWT. Gateway issues/attaches backend JWTs per authenticated request. Backends validate signature/issuer/audience/expiry/claims independently. Catalog quote creation uses a separate Reservations service credential, not the browser session. Preserve durable encrypted Data Protection keys and expiry/revalidation behavior as appropriate to HTTP; Blazor circuit concerns do not apply to Angular.

Specify and implement GET /auth/csrf, POST /auth/login, POST /auth/logout, GET /auth/me. Use ASP.NET Core antiforgery on cookie-authenticated mutations including login/logout; align Angular XSRF cookie/header names (e.g. readable XSRF-TOKEN request token + X-XSRF-TOKEN header) with the server's separate antiforgery cookie. Readable CSRF token is not the authentication credential. Integration-test missing/invalid tokens and origin handling. Guards are UX only; backend enforces authorization. Clear user state on logout, expiry, account change and 401. Do not add public registration/recovery or Entra end-user login without separate scope.

## NGXS requirements
Use NGXS meaningfully for shared feature state, not every keystroke. Suggested AuthState, CatalogState, ReservationsState, DashboardState, AdminState. Actions LoadTours, LoadSessions, CreateReservation, LoadReservation, CancelReservation, LoadDashboardPage, Logout etc. return observable completion and propagate classified failures; loading flags reset on all paths. Use selectors and immutable updates, no hidden direct HTTP calls in templates.

Typed API clients perform transport; states orchestrate data/loading/errors; components dispatch and render. Scope feature state and avoid a single giant state file. Form-local transient state can remain in the component. Handle stale responses and concurrent dispatches so an old query cannot overwrite a new filter or another authenticated user's data. Prefer cancellation or request identity guards. Do not automatically retry non-idempotent mutations with new idempotency keys.

No JWT/cookie/password/signed URL persistence in NGXS/localStorage. Avoid persisting sensitive booking data; reset all per-user state. NGXS DevTools only in development with sensitive-state safeguards. Explain actions/selectors/RxJS versus Angular signals; signals do not replace the required NGXS exercise.

## Human functionality
| Area | Required actions |
|---|---|
| Auth | Login/logout/current user; session-expired behavior |
| Catalog | Tour list/detail, sessions/prices/availability, ready images |
| Admin catalog | Tour create/edit/activate; session create; price/capacity with version checks; image upload/delete |
| Booking | Create with participants/idempotency; authoritative list/detail/timeline; cancel |
| Read dashboard | Cosmos-backed paginated My bookings; stale/unavailable labels; link to authoritative details |
| Payments | Owned operation list/detail; fake state labels |
| Notifications | Owned notifications list/detail, simulated delivery status |
| Admin workflow | Saga diagnostics; retry compensation; payment reconcile; fake-provider resolve; supported fault controls/reset |
| Admin broker | Bounded DLQ inspect and audited replay |
| Storage recovery | Authorized projection rebuild/image recovery controls where specified; no arbitrary object paths |
| Receipt | Receipt status and authorized simulated confirmation download |

Every original human action must appear in the parity matrix with a UI route/control or an explicit approved exclusion. Admin SQL/broker operations cannot be replaced with browser-direct database or Service Bus access. All browser data goes through APIs; storage downloads may be proxied.

## API contracts
Preserve original /api/{service}/v1 resource shapes when feasible. Use OpenAPI and typed DTOs. ProblemDetails includes safe code and traceId; 400 validation, 401 unauthenticated, 403 role, 404 ownership/not found, 409 state/version/idempotency conflict, 503 dependency unavailable, 202 durable accepted work. No raw exception/SQL leakage. Define pagination and loading/empty/error/retry UI. Authoritative cancellation controls must tolerate projection lag and backend409.

## Angular19 comparison only
Provide docs/angular19-to21.md explaining verified differences in toolchain, standalone defaults, signals/RxJS/NGXS and testing relevant to the implemented code. Do not maintain two Angular versions. Do not invent employer-specific standards.


---

Source: specs/05-blob-cosmos-functions.md

# 05 — Blob Storage, Cosmos DB and Azure Functions

## Blob tour images (Catalog ownership)
Reuse the existing image workflow if implemented; otherwise implement pending/ready/delete-pending metadata, bounded JPEG/PNG upload (suggest5MiB, max5 per tour), signature and decode/dimension limits, no SVG/arbitrary files. Server-generated unique keys, private container, no public account access, no credentials in browser. Prefer bounded streaming through Gateway/Catalog; download via authorized streaming proxy for the MVP, avoiding SAS/browser-hostname complexity. Do not store bytes/base64 in SQL. Store metadata/key, not expiring URL.

SQL+Blob are not atomic. Persist intent before network call, publish ready only after verified object success, handle success followed by SQL failure, reconcile pending work, coordinate deletes with uploads, and prevent resurrection by late completion. Use idempotent delete, bounded scoped orphan cleanup with age/lease safeguards, no broad container purge. Cloud Blob SDK uses managed identity; local uses Azurite. Restrict Catalog identity to its container. Persist retries/recovery history. UI shows placeholders/errors and supports Admin upload/delete.

## Cosmos booking dashboard (Reservations ownership)
Use Cosmos API for NoSQL with Microsoft.Azure.Cosmos SDK, not EF Cosmos as a drop-in relational provider and not Mongo API. Container bookings partition key /userId; id=reservationId. Document includes userId, reservationId, tourNameSnapshot, departureUtc (canonical UTC ISO), participants, amountCents, currency, status, businessVersion and projectedAtUtc. Keep snapshots and accepted prices immutable where intended.

Access patterns: partition-scoped paginated user bookings ORDER BY departureUtc DESC then id ASC, and point-read by userId+reservationId. Declare/test required composite indexing. Derive userId from claims. Opaque continuation cursor must be protected and bound to user/query with size/expiry limits. No normal cross-partition scans. Admin rebuild may read bounded Reservations SQL snapshots; never other schemas.

Every relevant reservation change persists BookingSnapshotChanged in SQL Outbox atomically. Publish to domain-events, consume dashboard-events subscription. Event contains sufficient data without Catalog lookup. businessVersion monotonically increases per reservation, separate from SQL rowversion bytes.

Do not use unconditional Upsert for version ordering. Point read current version/ETag; stale/equal => no-op; absent => create handling409; newer => conditional replace If-Match ETag. On412/conflict re-read and compare with a bounded retry, then redeliver if contention persists. Crash after Cosmos update before broker Complete is safe. Never persist SQL Inbox first in a way that suppresses a failed Cosmos write. No claimed SQL+Cosmos atomic transaction. Cosmos transactional batch is limited to the logical partition and cannot include SQL or Service Bus.

Projection outage must not block booking/charge/cancel. UI explicitly says eventually consistent; authoritative details/actions still use Reservations SQL. No silent SQL fallback presented as Cosmos success. Show unavailable state and traceId. Rebuild uses same event/update path and version guards, bounded batches and keyset source pagination; do not drop the container/reset Inbox to repair. Test live events racing rebuild. TTL is not a precise booking deadline; do not TTL active dashboard data by default.

## Receipt Functions (new bounded feature)
A confirmed booking creates a simulated downloadable booking confirmation, NOT a legal tax invoice or actual payment receipt. Use a small UTF-8 .txt file; no external PDF library required.

Notifications consumes ReservationConfirmed once into durable work. In its own SQL transaction create a unique ReceiptJob (reservationId+kind+sourceBusinessVersion), immutable confirmation snapshot, and GenerateBookingReceipt Outbox command to receipt-jobs. Only confirmed bookings generate this feature. A later cancellation leaves historical confirmation intact but receipt UI also shows current authoritative reservation state separately. The document records confirmation time and clearly says simulated educational confirmation.

Receipts.Functions uses .NET10 isolated worker, a Service Bus trigger with explicitly controlled settlement where supported, and Blob SDK. It accesses only Notifications-owned data and a separate booking-receipts container. It processes a stable job ID, claims by durable lease/version, writes deterministic immutable content/key (check hash/conditional create on duplicates), then marks Ready in SQL. No network inside SQL transaction. If Blob succeeds but SQL fails, retry verifies existing content before completing the job. Conflicting content is an audited error, not overwrite.

Complete the trigger message only after durable Ready or verified safe duplicate. Transient failures retry; poison commands reach DLQ with safe diagnostics. Lock loss/duplicate invocation cannot create duplicate receipts. Test using the ACTUAL local Functions host and Service Bus emulator, not only direct handler invocation. Function host requires its own configured AzureWebJobsStorage/Azurite; do not confuse host storage with business metadata.

Notifications exposes receipt status/download, authorizes by stored owner and admin policy; another user=>404. Gateway streams attachment after auth; never browser-direct credentials. HTTP request does not wait for function completion. Fake notification flow still works if receipt generation fails.

Use singleton thread-safe SDK clients, per-invocation DbContext/scope, cancellation, structured trace links. Domain Saga remains SQL-persisted in Reservations; do not replace it with Durable Functions merely to demonstrate another framework. Optional later timer-trigger cleanup is out of required scope.


---

Source: specs/06-security-cloud-costs.md

# 06 — Security, cloud preparation, costs

## Managed identity and Key Vault
Local: ASP.NET user-secrets/ignored env files and fresh lab key material. No Key Vault emulator is required or claimed. Cloud: managed identities for Service Bus, Blob, Cosmos, Key Vault and Azure SQL where feasible. Use Azure.Identity with a deliberate cloud credential selection; do not permit an unnoticed fallback to developer credentials in production. Distinguish end-user authentication from workload identity.

Key Vault holds real application secrets that cannot be replaced by managed identity, e.g. lab JWT signing/private certificate material. Do not store every public setting as a secret or introduce static access keys just to demonstrate Key Vault. Document whether Key Vault references or SDK retrieval is used, startup/refresh/failure behavior, required role assignments, and secret rotation. Give metadata/status not secret values in diagnostics.

Identity-owned Data Protection keys persist durably and are encrypted using supported certificate/Key Vault protection, accessible only to Gateway. Explain key-ring storage versus encryption key and restart behavior. Signing and Data Protection material must survive an app restart. Local tests cannot prove cloud managed identity/Key Vault references.

## IaC
Use Bicep modules and Azure CLI scripts: resource group-scoped plan/apps, Service Bus Standard (topics required), Blob/Functions host storage, Cosmos API NoSQL database/container/indexes, Azure SQL server/database, Key Vault, identities/RBAC and minimal monitoring. Bicep is deliberate Azure preparation, independent of original Terraform. Do not create a second Terraform Azure implementation.

Follow current official .NET10/App Service and Functions support. Five APIs may share one measured paid plan; Functions supported Flex Consumption profile is separate. Build deployment packages and serve Angular from Gateway; no mandatory ACR if code deployment suffices. Infrastructure provisioning and application delivery are separate operations.

Bicep parameters/templates/logs never contain real secrets. Document private provisioning outside committed parameter files; generated deployment artifacts and state-like exports stay ignored. Initial cloud tasks are not launched by local CI. Validate bicep build/lint; report real what-if only when actually executed with authorized read access. No unauthorized Azure mutation.

## Cost constraints
Local-first is the zero-cloud-cost path. A faithful always-running Azure profile is NOT promised free. Before any deployment, produce docs/cost-estimate.md using current official calculator/pricing, selected region/currency/SKUs, fixed and variable charges, daily/weekly/monthly study use and continuous operation.

Important: stopping Web Apps does NOT stop charges for their App Service plan. Five shared apps share paid compute capacity. Service Bus Standard has a base charge and cannot be paused like a container; topic/subscriptions need Standard/Premium, not Basic. Log ingestion/retention, Functions host storage, Blob requests/storage, Key Vault operations, bandwidth, SQL and Cosmos all need consideration. Budget alerts are not hard limits.

Assess Azure SQL free-offer eligibility and stop-at-free-limit option; do not assume credits/eligibility. Polling workers/health checks can prevent SQL auto-pause and consume the allowance. Cosmos free tier (where available) requires explicit enablement at creation, has subscription/account constraints and differs from serverless billing; do not label all serverless Cosmos usage free. Start with one region, minimal throughput/one shared database, no unplanned autoscale or multi-region. Verify defaults before deployment.

Default budget target is local-only until an actual estimate is reviewed. Do not provision resources with a silently selected expensive tier because a free offer is unavailable. Optional private endpoints/VNet, paid monitoring and staging slots require a separate price/design decision.

## Lifecycle and CI/CD
preflight verifies target subscription/tenant/region/resource group/tags, configuration, provider registration, quota and tool support. Provision is deliberate. deploy updates explicit versioned artifacts after migration job, waits for health and smoke tests. Stop documents remaining charges rather than claiming zero. Destroy is explicit and scoped to AzureLab tags/resource group, shows resources and retention choices, preserves data by default; never touches AWS. No unconditional resource-group deletion script without reviewed scope.

GitHub Actions CI first. Cloud CD initially workflow_dispatch after explicit deployment authorization, OIDC federated Azure identity, minimal deployment permissions, no long-lived client secret. Deploy app releases without implicitly reapplying infrastructure. Use concurrency controls, immutable artifacts, compatible one-shot migrations, secret redaction, smoke tests and rollback limitations. Automatic main deployment is a later explicit opt-in and must not silently activate a paused study environment. Do not add Azure DevOps pipelines until actual assignment requirements establish that need; include a conceptual Jenkins/GitHub/Azure Pipelines mapping.


---

Source: specs/07-acceptance.md

# 07 — Acceptance and evidence

Every row requires code paths and executed evidence, or an explicit NOT VERIFIED status. Original baseline parity remains required even when not individually repeated below.

| ID | Required behavior / evidence |
|---|---|
| AC-A01 | Original repo untouched; source/target paths, commit and before/after evidence; independent names/ports/resources |
| AC-A02 | .NET10 build; Angular21/compatible NGXS/Node lockfiles; five APIs + isolated Functions scaffold |
| AC-A03 | SQL Server bootstrap/migrations/seed repeat; runtime roles cannot read/write other schemas or run DDL |
| AC-A04 | Cookie login/logout/me/CSRF; missing token denied; durable encrypted key persistence; restart login behavior |
| AC-A05 | Direct backend rejects invalid signature/issuer/audience/expired JWT; internal quote route service-only |
| AC-A06 | Alice/Bob/Admin isolation; forged owner IDs denied;404 for other user's resources; backend Admin checks |
| AC-A07 | Catalog CRUD/session price/capacity/version checks and Angular controls |
| AC-A08 | Concurrent last-seat contention gives one winner; expiry/confirm/release/tombstone restore capacity once |
| AC-A09 | Concurrent same idempotency key => one reservation; different body=>409; no hidden Saga on quote503 |
| AC-A10 | Hold -> fake charge -> confirm -> simulated notification works through real local broker and SQL |
| AC-A11 | Outbox commit-before-send; send-before-mark; Inbox commit-before-Complete crash tests |
| AC-A12 | Provider independent charge commit then app crash => reconciliation, one charge |
| AC-A13 | Decline, confirmation rejection, refund/release ordering and repeated cancellation remain correct |
| AC-A14 | Timeout-after-charge, unknown/ManualReview/admin resolve, late charge/hold events recover safely |
| AC-A15 | Two worker instances race same Saga/lease and business IDs without double effects |
| AC-A16 | Bounded resilience/breaker, dependency failure, user cancellation and graceful shutdown |
| AC-A17 | Service Bus routing filters deliver intended events once logically to each consumer; no competing command consumers |
| AC-A18 | Malformed message -> built-in DLQ; audited replay preserves logical IDs and handles broker dedup window |
| AC-A19 | All original human API actions have Angular equivalents or explicit approved exceptions |
| AC-A20 | NGXS selectors/actions/loading/errors/cancelled or stale loads; user state reset on logout401/account change |
| AC-A21 | Concurrent Alice/Bob browsers never share tokens/state; refresh/deep link/back navigation correct |
| AC-A22 | Blob bounded valid upload/display/remove; unsupported/oversize rejected; backend ownership/Admin enforced |
| AC-A23 | Blob success+SQL failure, restart reconciliation and late upload vs deletion cannot leak ready images/resurrect |
| AC-A24 | Cosmos user-partition list/point read/pagination/indexes; no SQL fallback or normal full scan |
| AC-A25 | Duplicate/out-of-order/concurrent projection events use ETag version guard; no lost update |
| AC-A26 | Cosmos success-before-ACK crash, outage isolation and rebuild racing newer event |
| AC-A27 | Service Bus-triggered receipt via real Functions host -> private Blob -> authorized UI download |
| AC-A28 | Duplicate receipt/Blob success+SQL failure/Function restart yields one immutable logical receipt |
| AC-A29 | Function ownership/404, clear simulated document, notification flow independent of receipt failure |
| AC-A30 | Managed-identity/RBAC/Key Vault design verified structurally; local vs actual cloud proof separated |
| AC-A31 | Bicep validation, secure non-secret config, measured hosting settings and sourced cost estimate |
| AC-A32 | CI runs actual .NET/Angular tests and image/package builds with isolated SQL/broker/storage/Cosmos |
| AC-A33 | Playwright success/cancel/Admin/ownership/image/dashboard/receipt/NGXS navigation flows execute |
| AC-A34 | JSON logs correlate HTTP/messages/Functions, no secrets, bounded diagnostic metrics and health |
| AC-A35 | Explicit cloud provision/deploy/migration/smoke/rollback proof; source AWS untouched (AZ-012 only) |
| AC-A36 | Lifecycle cost behavior and retained data verified in Azure; stop does not falsely claim zero billing |

## Test levels
- Unit: transitions, input rules, money, cursors, version comparison, NGXS reducers/selectors/error paths.
- Relational integration: real SQL Server, restricted principals, concurrent writes, provider-specific SQL, durability.
- Broker/storage/document integration: real supported local emulators + actual SDKs, not mock-only evidence.
- Host integration: Functions Core Tools + broker/Azurite; invoke the trigger path.
- E2E: Playwright Test TypeScript on real Angular+APIs+workers, synthetic isolated users/data.
- Cloud: separate opt-in suite validates managed identity, TLS, Key Vault, Service Bus cloud behavior, quotas, App Service/Functions health and costs.

Use deterministic barriers/fault hooks for crash tests, not production unauthenticated crash endpoints. Don't assert network exactly-once delivery. Test business effects and recovery.

## Playwright practice
Use role/label locators or deliberate test IDs, web-first assertions, unique per-test data and separate browser contexts. No fixed waitForTimeout to await eventual consistency; use bounded polling with diagnostic evidence. API fixtures may arrange data through authorized test-safe APIs; browser acceptance still exercises the actual UI. Avoid direct SQL/broker mutation to fake outcomes.

Store trace/screenshots on failure only where useful; short retention; auth state files ignored/private, never uploaded as artifacts. Pin Playwright package/browser image together. Chromium required initially, Firefox/WebKit optional extra coverage. Distinguish small mocked UI tests from full-stack smoke. Sandbox unavailable => NOT VERIFIED, not bypass security or pretend SSR checks validate Angular behavior.


---

Source: tasks/implementation-plan.md

# Azure companion implementation plan

Initial status: all tasks Not Started. Work on ONE task per implementation turn; each has an independent Antigravity review gate. Task completion is not mastery or proof of Azure deployment.

| ID | Scope | Prerequisites | Exit evidence |
|---|---|---|---|
| AZ-001 | Read-only source audit, parity matrix, isolated new repo, toolchain/process scaffold and local dependency preflight | None | AC-A01/02; runnable build scaffold, resource map and blockers |
| AZ-002 | SQL Server provider port, schema/roles/migrations, Identity/BFF/CSRF and minimal Angular login | 001 | AC-A03..06; SQL integration and login smoke |
| AZ-003 | Catalog/quotes/inventory, Angular shell and NGXS foundation | 002 | AC-A07/08/20; tours/Admin visible, atomic inventory tests |
| AZ-004 | Service Bus command/topic routing, Outbox/Inbox, reservation success, durable fake charge/notification | 003 | AC-A09..12/17; working end-to-end vertical slice |
| AZ-005 | Full Saga faults/recovery, admin/resilience/observability and Angular baseline parity | 004 | AC-A13..21/34; parity matrix completed except designated extensions |
| AZ-006 | Blob tour images + Angular + recovery tests | 005 | AC-A22/23 |
| AZ-007 | Cosmos booking projection, rebuild and NGXS dashboard | 005 | AC-A24..26 |
| AZ-008 | Notifications-owned receipt Functions, Blob output and Angular download | 006 and 007 | AC-A27..29; actual local Functions trigger evidence |
| AZ-009 | Key Vault/managed identity wiring and secure cloud configuration preparation | 008 | AC-A30; local secret path and cloud prerequisite matrix |
| AZ-010 | Full Playwright suite, CI, Angular19/21 notes and study map | 009 | AC-A19..21/32..34; executed full-stack evidence |
| AZ-011 | App Service/Functions Bicep, migration/deployment/lifecycle scripts, cost estimate | 010 | AC-A31; no deployment; ready for independent review |
| AZ-012 | Explicitly authorized Azure provisioning/CD smoke, managed identity/RBAC verification and cost teardown exercise | 011 + user authorizes cloud actions | AC-A35/36 plus cloud gaps from AC-A30; actual evidence |

CI scaffolding and small Playwright smoke tests begin earlier (001/002); AZ-010 completes and hardens them, rather than postponing all tests until the end.

## Preparation before the assignment
Prioritize 001..005 first for a useful Angular + SQL Server + Service Bus system. Then 006..009 expose the remaining named Azure services. 010 reinforces automation; 011/012 address hosting. If time runs short, explicitly record partial scope instead of declaring parity. Do not automatically deploy to meet a calendar date.

## Each delivery
Update this plan/current task and evidence with actual state: Not Started, In Progress, Implemented Awaiting Review, Verified, Blocked. Include source immutability evidence, exact checks, acceptance IDs, remaining limitations and next task. Only an actual independent review can advance to Verified. Do not invent historic findings or mark unexecuted tests passed.


---

Source: docs/decisions.md

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


---

Source: docs/official-sources.md

# Official sources and verification notes

Checked 2026-10-23. These are references, not proof of deployment. Recheck versions, region capabilities, limits and pricing when implementing.

- Angular version compatibility: https://angular.dev/reference/versions
  Angular21 requires a compatible Node/TypeScript/RxJS combination; do not use newest Angular by default.
- NGXS21 documentation: https://www.ngxs.io/v21.0
  Verify actual package peer dependencies and available versions before pinning.
- SQL Server EF provider: https://learn.microsoft.com/en-us/ef/core/providers/sql-server/
- Service Bus emulator: https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator
  Emulator loses data/entities on container restart and lacks cloud identity/network features.
- Service Bus messaging: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview
- Topics and SKU limitations: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-tutorial-topics-subscriptions-portal
- Service Bus pricing: https://azure.microsoft.com/en-us/pricing/details/service-bus/
- Azurite: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite
- Cosmos local Linux emulator: https://learn.microsoft.com/en-us/azure/cosmos-db/emulator-linux
  Subset of features, gateway mode; the documented vNext emulator does not enforce authentication. Do not claim RBAC coverage.
- Cosmos concurrency/transactions: https://learn.microsoft.com/en-us/azure/cosmos-db/database-transactions-optimistic-concurrency
  ETag conditional writes; transactional boundary is logical partition, not SQL+Cosmos.
- Functions isolated .NET guide: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide
  .NET10 uses isolated worker; classic Linux Consumption is not the .NET10 target. Verify Flex Consumption support and current tooling.
- Managed identity connections: https://learn.microsoft.com/en-us/azure/azure-functions/manage-connections
- Key Vault: https://learn.microsoft.com/en-us/azure/key-vault/general/overview
- App Service plan costs: https://learn.microsoft.com/en-us/azure/app-service/overview-manage-costs
  Stopped apps do not stop paid plan charges.
- Azure SQL free offer: https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier
  Eligibility/limits and exhaustion choice require review; polling affects active compute usage.
- Cosmos free tier: https://learn.microsoft.com/en-us/azure/cosmos-db/free-tier
  Explicit creation-time choice; not equivalent to universally free serverless.
- Playwright best practices: https://playwright.dev/docs/best-practices
- Azure calculator: https://azure.microsoft.com/en-us/pricing/calculator/

No fixed currency estimate is asserted in this specification because subscription, region, free-offer eligibility and measured task capacity are not yet known. AZ-011 must produce a sourced estimate before AZ-012 can be authorized.
