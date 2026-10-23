# Version 1 routing plan from actual source contracts

Source: Shared.Contracts/CatalogContracts.cs, ReservationContracts.cs, PaymentContracts.cs,
ExtensionContracts.cs and Shared.Infrastructure/Messaging/MessageCodec.cs (actual MessageRoutes).
This is a reviewed implementation input for AZ-004/007/008, not running Azure consumers.
All names below use the independent tourlab-azure prefix.

| Kind and actual type | Publisher | Azure destination | Intended handler/subscription | Task |
|---|---|---|---|---|
| HoldSeats | Reservations | catalog-commands queue | Catalog | 004 |
| ConfirmSeats | Reservations | catalog-commands queue | Catalog | 004 |
| ReleaseSeats | Reservations | catalog-commands queue | Catalog | 004 |
| ProcessPayment | Reservations | payments-commands queue | Payments | 004 |
| ReconcilePayment | Reservations | payments-commands queue | Payments | 004 |
| RefundPayment | Reservations | payments-commands queue | Payments | 005 |
| SeatsHeld | Catalog | domain-events topic | reservations-events | 004 |
| SeatsHoldRejected | Catalog | domain-events topic | reservations-events | 004 |
| SeatsConfirmed | Catalog | domain-events topic | reservations-events | 004 |
| SeatsConfirmationRejected | Catalog | domain-events topic | reservations-events | 005 |
| SeatsHoldExpired | Catalog | domain-events topic | reservations-events | 005 |
| SeatsReleased | Catalog | domain-events topic | reservations-events | 004 |
| PaymentSucceeded | Payments | domain-events topic | reservations-events | 004 |
| PaymentDeclined | Payments | domain-events topic | reservations-events | 005 |
| PaymentOutcomeUnknown | Payments | domain-events topic | reservations-events | 005 |
| PaymentRefunded | Payments | domain-events topic | reservations-events | 005 |
| RefundNeedsReview | Payments | domain-events topic | reservations-events | 005 |
| ReservationConfirmed | Reservations | domain-events topic | notifications-events | 004 |
| ReservationFailed | Reservations | domain-events topic | notifications-events | 005 |
| ReservationCancelled | Reservations | domain-events topic | notifications-events | 005 |
| TourSessionChanged | Catalog | domain-events topic | reservations-events (informational projection) | 005 |
| BookingSnapshotChanged | Reservations | domain-events topic | dashboard-events | 007 |
| GenerateBookingReceipt (new) | Notifications | receipt-jobs queue | Receipts.Functions | 008 |

Physical destination = tourlab-azure- plus the table destination. Each subscription is independently prefixed too.
Reserve reservations-commands and notifications-commands queues per spec, but do not invent commands for them.
catalog-events/payments-events have no observed event-handler need; create only with explicit false filters until assigned.
Do not leave default match-all rules. config/servicebus.json intentionally starts with no entities in AZ-001;
AZ-004 adds the actual typed filters and tests, not dummy consumers.

Source messageId is logical ID and deliveryId identifies SQS fan-out. Azure EventId maps to logical messageId;
schemaVersion maps from Version; preserve correlation/causation, traceparent/tracestate and UTC timestamps.
Initial Service Bus MessageId equals EventId; publication retry keeps it stable.
One topic publish is one Outbox delivery. Do not retain per-subscriber SQS Outbox rows and multiply topic publication.
Envelope changes require explicit serialization compatibility tests in AZ-004.
Source ReservationConfirmed currently lacks the immutable detail required for receipts;
AZ-008 must version/enrich the Notifications-owned snapshot contract deliberately (no cross-schema lookup).

Use manual PeekLock settlement after durable local commit, leases outside send transactions,
logical Inbox/effect idempotency, MaxDeliveryCount5 and bounded renewal.
Each queue/subscription uses its built-in DLQ. Replay preserves logical IDs, may use a fresh transport ID,
and topic replay can affect other subscribers. Emulator reset and IAM are separate limitations.
