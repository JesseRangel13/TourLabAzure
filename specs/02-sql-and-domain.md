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
