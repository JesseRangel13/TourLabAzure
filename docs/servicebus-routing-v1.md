# Service Bus routing inventory — version 1 (AZ-004)

Generated from MessageRoutes in src/Shared.Infrastructure/Messaging/MessageCodec.cs and config/servicebus.json on 2026-10-24. Names and SQL filters below are the implemented local topology.

| Logical types | Destination | Active effect handler |
| --- | --- | --- |
| HoldSeats, ConfirmSeats, ReleaseSeats | tourlab-azure-catalog-commands | CatalogConsumer / InventoryHandlers |
| ProcessPayment, ReconcilePayment, RefundPayment | tourlab-azure-payments-commands | PaymentConsumer / durable workers |
| SeatsHeld, SeatsHoldRejected, SeatsConfirmed, SeatsConfirmationRejected, SeatsHoldExpired, SeatsReleased, TourSessionChanged, PaymentSucceeded, PaymentDeclined, PaymentOutcomeUnknown, PaymentRefunded, RefundNeedsReview, ReservationConfirmed, ReservationFailed, ReservationCancelled | tourlab-azure-domain-events (one publish per intent) | Filtered subscriptions below |

| Subscription | Exact SQL filter | Consumer |
| --- | --- | --- |
| catalog-events | 1=0 | Inactive; no types accepted |
| reservations-events | type = 'SeatsHeld' OR type = 'SeatsHoldRejected' OR type = 'SeatsConfirmed' OR type = 'SeatsConfirmationRejected' OR type = 'SeatsHoldExpired' OR type = 'SeatsReleased' OR type = 'TourSessionChanged' OR type = 'PaymentSucceeded' OR type = 'PaymentDeclined' OR type = 'PaymentOutcomeUnknown' OR type = 'PaymentRefunded' OR type = 'RefundNeedsReview' | ReservationConsumer |
| payments-events | 1=0 | Inactive; no types accepted |
| notifications-events | type = 'ReservationConfirmed' OR type = 'ReservationFailed' OR type = 'ReservationCancelled' | NotificationConsumer |
| dashboard-events | type = 'BookingSnapshotChanged' | Reserved for AZ-007; no active worker |

Reserved queues: tourlab-azure-reservations-commands, tourlab-azure-notifications-commands, tourlab-azure-receipt-jobs. No competing business handler is attached to these queues in AZ-004. BookingSnapshotChanged is reserved by the dashboard filter, not produced by AZ-004.

Every entity has a built-in DLQ; no parallel fake DLQ queue. MaxDeliveryCount=5, lock=1 minute, explicit local TTL=1 hour, duplicate detection disabled. SQL deduplication/tombstones/provider effects have no purge and retain at least 14 days. Manual PeekLock settlement follows SQL commit; consumers permit 4 concurrent callbacks, no prefetch, 2 minute renewal and a 40 second SQL-work budget. Outbox lease=60 seconds, one item per scope, 25 second dispatch budget; stable transport MessageId across resend. SDK retries=2, attempt timeout=5 seconds. Poison reasons contain no message body or secrets.
