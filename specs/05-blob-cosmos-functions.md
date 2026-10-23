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
