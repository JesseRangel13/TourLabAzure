# Service Bus routing inventory — version 1 (updated AZ-008)

Generated from MessageRoutes in src/Shared.Infrastructure/Messaging/MessageCodec.cs and config/servicebus.json on 2026-10-24. Names and SQL filters below are the implemented local topology.

| Logical types | Destination | Active effect handler |
| --- | --- | --- |
| HoldSeats, ConfirmSeats, ReleaseSeats | tourlab-azure-catalog-commands | CatalogConsumer / InventoryHandlers |
| ProcessPayment, ReconcilePayment, RefundPayment | tourlab-azure-payments-commands | PaymentConsumer / durable workers |
| GenerateBookingReceipt | tourlab-azure-receipt-jobs | Receipts.Functions GenerateBookingReceipt (Notifications owner) |
| SeatsHeld, SeatsHoldRejected, SeatsConfirmed, SeatsConfirmationRejected, SeatsHoldExpired, SeatsReleased, TourSessionChanged, PaymentSucceeded, PaymentDeclined, PaymentOutcomeUnknown, PaymentRefunded, RefundNeedsReview, ReservationConfirmed, ReservationFailed, ReservationCancelled, BookingSnapshotChanged | tourlab-azure-domain-events (one publish per intent) | Filtered subscriptions below |

| Subscription | Exact SQL filter | Consumer |
| --- | --- | --- |
| catalog-events | 1=0 | Inactive; no types accepted |
| reservations-events | type = 'SeatsHeld' OR type = 'SeatsHoldRejected' OR type = 'SeatsConfirmed' OR type = 'SeatsConfirmationRejected' OR type = 'SeatsHoldExpired' OR type = 'SeatsReleased' OR type = 'TourSessionChanged' OR type = 'PaymentSucceeded' OR type = 'PaymentDeclined' OR type = 'PaymentOutcomeUnknown' OR type = 'PaymentRefunded' OR type = 'RefundNeedsReview' | ReservationConsumer |
| payments-events | 1=0 | Inactive; no types accepted |
| notifications-events | type = 'ReservationConfirmed' OR type = 'ReservationFailed' OR type = 'ReservationCancelled' | NotificationConsumer |
| dashboard-events | type = 'BookingSnapshotChanged' | Reservations dashboard projection worker |

Reserved queues: tourlab-azure-reservations-commands and tourlab-azure-notifications-commands have no active business commands. AZ-007 publishes BookingSnapshotChanged; AZ-008 activates only the receipt-jobs Functions trigger. No competing API worker consumes receipt-jobs.

Every entity has a built-in DLQ; no parallel fake DLQ queue. MaxDeliveryCount=5, lock=1 minute, explicit local TTL=1 hour, duplicate detection disabled. SQL deduplication/tombstones/provider effects have no purge and retain at least 14 days. Manual PeekLock settlement follows SQL commit; consumers permit 4 concurrent callbacks, no prefetch, 2 minute renewal and a 40 second SQL-work budget. Outbox lease=60 seconds, one item per scope, 25 second dispatch budget; stable transport MessageId across resend. SDK retries=2, attempt timeout=5 seconds. Poison reasons contain no message body or secrets.

Receipt trigger: no prefetch,4concurrent calls,2minute renewal,90second bounded SQL-lease wait and30second Blob budget. Receipt SQL lease60seconds; manual Complete only after Ready/verified duplicate. See receipts-execution.md.
