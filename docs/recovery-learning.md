# Recovery learning walkthrough — AZ-005

Read source W-01..W-06 and Azure specs/02-sql-and-domain.md, then open these code paths.

## Cancellation and compensation
Reservations/Application/SagaCommands.cs serializes the cancellation key, then locks the Saga row.
The key lock hashes a canonical user/key pair to fit SQL Server's bounded application-lock resource.
Authorization/ownership is checked independently at the API and resource query; another owner gets404.
A first valid cancellation requires Confirmed and future departure. It sets CancellationRequested,
keeps the predefined RefundOperationId and creates durable refund/release intents in one local transaction.
A repeated key replays its original202; different keys still cannot create a second full refund.

SagaRecovery.CompleteAsync checks both durable completion flags and payment certainty before finishing.
Refund and release can arrive in either order. Failed/Cancelled is never guessed from a timer.
Deadline handlers and consumers lock the same Saga row and use business versions; two workers can race.
The losing action reloads state or becomes a compatible no-op. Late confirmations do not resurrect a
booking whose seats were abandoned. Release-before-hold retains a tombstone, so a late hold cannot
take stock again.

Confirmation failure after payment triggers a full simulated refund and release. A late discovered
charge after expiry/abandonment reopens repair, never Confirmed. The existing failure-notice effect key
prevents another terminal notice when repair finishes. One SQL test explicitly seeds an earlier Failed
decision as a recovery fixture; the later charge uses the actual durable provider. It is not E2E proof.

## Uncertain effects
Payments/Application/PaymentProcessing.cs distinguishes an unverified result from a decline.
The durable provider commits separately from the application outcome. TimeoutAfterCharge therefore
becomes Unknown. A worker first looks up the stable operation ID, then saves a definitive result.
Five bounded reconciliation queries use 5/10/20/30/30-second intervals; unresolved work stops automatic
queries and the Saga reaches ManualReview. Global mode changes do not alter the captured mode.

PaymentAdmin resolves only an UnknownUntilAdminResolution provider effect, with auditing. Reconciliation
then discovers that effect through the normal worker and emits a message. It never edits a Saga.
Compensation retry reuses operation IDs/flags and a monotonic retry version, so duplicate/stale requests
cannot reopen an exhausted refund endlessly or issue another full refund.

RefundTransientFailure persists two failed provider attempts, then succeeds. A provider-commit/app-crash
boundary leaves a recoverable lease; lookup discovers the same durable receipt. Notification failures
similarly leave Pending work behind a processed Inbox. FailureThenSuccess retries twice and sends once.
A notification failure does not refund a Confirmed booking: W-01 and specs/02 remain the authority.

## Transport and operations
Shared.Infrastructure/Operations contains technical SQL/broker plumbing, not shared business models.
Each service owns its OperationsAudit, DlqReplays and fault entities/migrations. Restricted credentials
cannot use another schema, DDL or migration history. SQL values are parameters and identifiers come
from fixed schema/table selections. No generic repository or reflection dispatcher was introduced.

Compare DeadLetterOperations with MessageCodec, ServiceBusTransport and SqlTransactions. SQL and broker
settlement cannot share an atomic transaction here. The owner lease fences concurrent Admin requests;
accepted-send-before-Complete can duplicate a delivery. Fresh transport IDs avoid broker suppression of
an intentional replay while unchanged logical IDs keep Inbox/business deduplication effective.
Replaying a subscription DLQ publishes through the original topic, so other matching subscribers can
see it. Peek doesn't lock; replay's bounded receive does. Unsafe probes remain in the real built-in DLQ.

Fault controls require backend Admin and explicit Development enablement. Shared operational selection
is technical configuration; payment/sender modes are still captured in service-owned work records.
Reset changes future fault selection, never deletes history or reclassifies an old charge.

## Tracing and state
Optional W3C metadata is persisted on each owned Saga/payment/refund/notification operation.
Workers restore an activity even after the originating HTTP request ends; Outbox/Inbox propagate it.
Scheduled inventory expiry starts its own trace and retains Saga correlation. JSON HTTP/message logs
include route patterns/status/trace metadata, never request bodies, credentials, cookies or connection strings.
Low-cardinality meters use fixed operation/outcome labels. Diagnostics distinguish durable counts from
process-local duplicate counters and worker observations.

Readiness validates the owned SQL principal and, when enabled, peeks the real owned broker with a
three-second budget. Liveness remains independent. An explicitly disabled transport is reported as
Disabled, not claimed as an executed workflow. Graceful processor shutdown is bounded and real-SDK tested.

Angular AdminApi/EffectsApi are typed transport clients. NGXS AdminState, EffectsState and ReservationsState
coordinate shared data/loading/errors, return completion, serialize mutations and reset per user.
Request identities/cancellation prevent old service/page/account results from replacing new state.
Typed reactive Admin forms own transient fields; signals render selectors. Backend authorization and
state checks remain necessary even when a guard/button hides an invalid action.

Read RecoverySqlTests, RecoveryHttpTests, QuoteResilienceTests, BrokerRecoveryTests and LiveRecoveryTests
beside evidence/AZ-005.md. Distinguish real SQL consumer tests, actual SDK replay, actual HTTPS/broker
workflow, Angular render/unit tests and unexecuted Chromium/cloud checks. Full baseline API/UI actions
are mapped in parity-matrix.md; Blob/Cosmos/receipt extensions retain their assigned tasks.
