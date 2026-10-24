# Booking workflow learning walkthrough — AZ-004

Start with Reservations/Application/ReservationHandlers.cs, then follow one accepted booking.

1. The API derives the user from its independently validated JWT, checks an idempotency key and
   validates 1..6 participants and the accepted MXN price. Catalog's five-minute quote is obtained
   outside the reservation transaction using a separate service-only token and bounded HTTPS client.
   Quote failure returns 503 before any hidden booking/Saga is persisted.
2. A SQL transaction writes the booking, original 202 response, Saga/history and HoldSeats intent.
   Concurrent identical keys converge on the one persisted response; changed content returns 409.
   A 202 is acceptance for processing, not confirmation.
3. OutboxDispatcher commits a finite owned lease, sends outside SQL, then marks publication with an
   owner fence. A crash after send can send again. Stable logical IDs and business deduplication make
   repeated delivery safe; there is no exactly-once transport promise.
4. Catalog consumes HoldSeats through its command queue using PeekLock. Inbox, atomic inventory
   changes and outcome Outbox commit together, followed by Complete. Confirmation never deducts stock
   twice. Release tombstones and version guards prevent late delivery from resurrecting inventory.
5. Seat events are published once to domain-events. Explicit subscription SQL filters deliver them
   to Reservations; inactive subscriptions use 1=0. Read servicebus-routing-v1.md beside BusTopology.
6. Reservations emits ProcessPayment with a stable operation ID. Payments owns its SQL models.
   DurableFakeProvider commits its fictional effect independently before the application result.
   If the worker crashes or sees TimeoutAfterCharge, lookup/reconciliation uses the same operation ID
   and captured mode. It cannot solve an unknown outcome by inventing another charge.
7. PaymentSucceeded leads to ConfirmSeats, then SeatsConfirmed leads to Confirmed and the terminal
   reservation event. All transitions use owned SQL transactions, guarded state and stable effect keys.
8. Notifications' Inbox creates durable Pending work; a separate sender receipt commits independently.
   If a sender succeeds before worker failure, retry discovers that receipt and marks one delivery Sent.
   A processed Inbox row never hides unfinished notification work.
9. Angular's typed clients and NGXS state keep shared loading/errors and per-user data. Reactive forms
   are local. Explicit retries retain the same unchanged request key. Sequence/account guards discard
   stale results, and logout/401/account changes clear data. Payment/notification outages do not replace
   the authoritative booking detail with a guessed failure.

Read tests/Scaffold.Tests/BookingSqlTests.cs for transaction/crash barriers with real restricted SQL
principals. Those tests invoke consumers directly and are clearly separate from
BrokerIntegrationTests (real routing/redelivery) and LiveBookingTests (real HTTPS + broker + SQL).
The latter also peeks and retains an actual built-in DLQ poison message.

SQL entities, business rules and DbContexts remain service-owned. Shared.Infrastructure contains
transport/locking/lease primitives, and Shared.Contracts only integration messages/envelopes.
Every singleton worker creates async scopes; provider/sender operations use separate contexts.
Background timers/deadlines are durable SQL state rather than emulator scheduled messages.

The emulator's one-hour TTL is a transport quota, not a safe deduplication deletion time.
At least 14 days of SQL deduplication/tombstone/provider history remains required, with no purge here.
Actual Azure Entra/RBAC/restart durability and Chromium UI acceptance are separate unverified claims.
Next study AZ-005 for full faults, cancellation, recovery, diagnostics and audited replay controls.
