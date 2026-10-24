# Cosmos booking dashboard — design and learning

## Authority and preserved boundary

AZ-006 has actual independent Antigravity PASS. Its narrative contains descriptive names that differ
from the code (decoder/container/file paths); this task uses actual implementation and specs as authority,
without changing the review. Reviewed read-only original Bookings/BookingProjection.cs and BookingStore.cs
for snapshot/conditional-update/cursor/rebuild behavior. No Dynamo SDK/entities/config/secrets or source
migrations were copied. Azure uses a dedicated filtered subscription to the existing single topic.

Reservations SQL remains authoritative for creation, Saga, price, seats, cancellation and compensation.
Cosmos owns an informational, eventually consistent copy. Its outage cannot make SQL falsely succeed,
start another financial effect or prevent an ordinary SQL booking/cancel command.
No cross-schema business query or Catalog lookup occurs during projection/rebuild.

## SQL snapshot publication

BookingSnapshotChanged contains owner/reservation ID, immutable accepted tour name, UTC departure,
participants, MXN cents/currency, status and businessVersion. BookingSnapshots.Add runs in the same SQL
transaction as creation and every existing Saga Record transition (including cancellation/recovery).
One Outbox event publishes once logically to the existing domain-events topic; transport duplicates
remain permitted. All normal workflow handlers share this transition point, rather than a second
reflection dispatcher or EF interceptor.

Existing Reservation.Version is already an explicit long business counter, incremented under existing
Saga locks and used as EF concurrency token. It supplies businessVersion; it is not SQL rowversion bytes.
ProjectionSequence is a separate SQL identity/index solely for stable bounded rebuild keyset scans.
It is not an event/version/creation-time claim. Fresh SQL migration backfills existing rows without
deleting business data. Existing rows can be explicitly rebuilt; previously published Outbox is not reset.

## Cosmos version guard

A singleton gateway-mode SDK client uses bounded HTTP connections, six-second SDK request timeout,
zero429 retries plus an application20s update budget. Each update validates its complete snapshot:

1. Point-read by /userId plus id=reservationId and obtain ETag.
2. Absent=>CreateItem;409 causes bounded re-read.
3. Older business version=>safe no-op; equal identical payload=>safe duplicate.
   Contradictory equal-version data or newer changes to immutable accepted fields=>classified poison.
4. Newer=>ReplaceItem with IfMatchEtag.412 causes bounded re-read/compare, at most five attempts.
   Contention after budget remains retryable. Never unconditional Upsert or SQL Inbox-before-Cosmos.

The document stores canonical fixed-width UTC ISO departure, projectedAtUtc and businessVersion.
The required composite index is departureUtc descending, id ascending. /userId is always supplied as
PartitionKey as well as the query predicate. No ordinary cross-partition listing exists; Admin detail
also remains own-partition scoped.

## Bounded pagination and protected cursors

The pinned emulator ignored MaxItemCount (a strict two-item test returned seven). Rather than relaxing
the assertion or silently discarding results, the application uses TOP(pageSize+1) plus immutable
(departureUtc DESC,id ASC) keyset filters. SDK page buffering and application response counts are bounded
to21, four SDK response pages and the API15s budget. An exhausted budget yields503.
This is Cosmos keyset continuation, not a claim that native SDK continuation behavior was verified.
Equal departure dates use ordinal canonical GUID strings as deterministic ascending tie breakers.
Concurrent inserts/updates are eventually consistent; this is not a frozen cross-request snapshot.

The sort position is wrapped in ASP.NET Data Protection, scoped by user, query version, page size
and15minute expiry, with a4KiB outer limit and2KiB internal payload limit. Tampering, wrong owner/query/
size/expiry=>400. It is an informational cursor, never an authentication credential. No raw cursor data
is logged or persisted in browser storage. Reservations owns its independent key ring and certificate,
separate from Gateway Identity keys. The local private directory inherits restricted ACLs; keys are
certificate encrypted and survive Reservations restart. Cloud durable key storage remains later work.

## Dedicated broker settlement

DashboardWorker alone consumes dashboard-events using manual PeekLock settlement, two concurrent calls,
no prefetch, bounded renewal and per-message scope/budget. Business command and Reservations workflow
subscriptions do not also consume BookingSnapshotChanged. Actual processor metadata/type validation
dead-letters poison safely to the subscription's built-in DLQ; transient failures remain retryable.
Complete happens only after Cosmos applied or a verified duplicate/stale no-op.
No SQL Inbox can hide a failed write. Cosmos success followed by lost Complete redelivers safely.
When retries exhaust the broker's MaxDeliveryCount, retained DLQ plus explicit rebuild enable repair;
no queue/container/Inbox purge. Dashboard DLQ inspection is currently an operator SDK diagnostic,
not silently added to the existing commands/events Admin replay selector.

Worker health is visible via existing owning-service diagnostics. Logs classify failure type without
message bodies/keys/SQL strings. W3C context is restored at the projection consumer. Shutdown stops only
this processor with a bounded budget; it does not stop the broker/Cosmos emulator.

## Rebuild ordering

Admin POST creates or resumes a15minute owner/query/batch-bound protected cursor with run ID,
last ProjectionSequence and captured upper bound. It reads at most50 Reservations-owned SQL snapshots,
ordered by that indexed sequence. A short SQL transaction/app lock deduplicates a repeated run/position/
businessVersion and persists normal BookingSnapshotChanged Outbox work plus actor audit.
The UI explicitly asks for each25-row batch; there is no unbounded synchronous scan.

Rebuild sends through the same topic/consumer/ETag path as live events. A row changing after it was read
may produce a stale rebuild snapshot, which cannot overwrite the newer live business version.
Writes committed after the high-water capture are covered by their normal transactional Outbox;
the scan is not a claim of global snapshot isolation. Existing partition/container/Inbox/data are retained.

## Angular and verification

DashboardApi is typed transport; DashboardState owns shared loading/data/errors/cursors/rebuild state.
Generation/request guards, cancellation and cross-list/detail invalidation suppress stale navigation
and account responses. UserSessionChanged resets all private state; no persistence/DevTools auth store.
Components dispatch actions, show eventual consistency/projection time and safe traceId on outage,
and link to existing authoritative details. They never synthesize SQL fallback or cancellation from
a stale projection. Rebuild state serializes mutation and retains the cursor after a failed batch.

Actual SQL tests prove snapshot transaction/version behavior, rollback and bounded/audited rebuild.
Actual Cosmos tests prove ETags412, create409 races, stale/equal/newer ordering, immutable fields,
partitioned bounded paging/tie ordering and live changes racing old rebuild data.
Actual broker tests retain redelivery after failed SDK write and after Cosmos success/lost Complete.
Host tests exercise booking/cancel, owner/cursor/CSRF/Admin, real poison DLQ and key-ring restart.
Browser test discovery is separate from Chromium execution; no security bypass fills that gap.

## Official references and emulator limits

[Cosmos emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/emulator-linux):
gateway NoSQL, HTTPS for .NET, no authentication enforcement, and composite-index enforcement unavailable.
Local ordering/declaration tests do not prove cloud index enforcement, RBAC, RU or persistence behavior.
We retain its existing pinned image digest and use the lab certificate/custom private emulator key.
[ETag request options](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.requestoptions.ifmatchetag)
and [SDK guidance](https://learn.microsoft.com/en-us/azure/cosmos-db/best-practice-dotnet)
inform the conditional-write and singleton client design. Cosmos3.63.2 requires explicit
Newtonsoft.Json13.0.4 even with System.Text.Json document serialization; pinned lockfiles retain both.


Preserved source input/read guards: the original booking list maximum20 and outer cursor maximum4KiB
remain (the larger general pagination ceiling is not used to relax this extension). Source validates
returned owner/key/snapshot; Cosmos responses also validate owner, canonical ID/UTC, status, amount,
participants and version before exposure. Invalid stored projection=>safe503.
The Azure spec explicitly changes ordering from the source's creation key to departure DESC/id ASC;
that provider-independent product choice is recorded rather than silently retaining the old order.
