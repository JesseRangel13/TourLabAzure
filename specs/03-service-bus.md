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
