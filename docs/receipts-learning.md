# AZ-008 receipt walkthrough

A booking confirmation is a historical fact. A cancellation is another fact; it cannot rewrite the
accepted tour name, departure, participants, accepted MXN cents or confirmation time printed earlier.
The Angular panel therefore shows both the historical simulated document and the current SQL status.

## Follow one successful booking

1. Reservations handles SeatsConfirmed under its existing Saga transaction. It includes the accepted
   snapshot and next numeric Reservation.Version in ReservationConfirmed. The same transaction records
   that version/status and its dashboard event. It does not wait for Notifications, Functions or Blob.
2. NotificationConsumer handles that event under its own Inbox transaction and reservation lock.
   ReceiptAcceptance adds a unique ReceiptJob and GenerateBookingReceipt Outbox along with normal
   Pending simulated notification work. It uses only the event's snapshot, never another schema.
3. The existing Notifications Outbox publisher commits its claim, sends to receipt-jobs, then records
   publication. Retries preserve the logical command/job IDs.
4. GenerateReceipt is discovered by the actual .NET10 isolated Functions worker. It has explicit
   manual Service Bus settlement and delegates to scoped ReceiptProcessor.
5. ReceiptProcessor commits an owner/version/expiry SQL lease and Claim audit. No Blob call happens
   inside that transaction. It renders deterministic bounded UTF-8 and conditionally creates the key.
6. BlobReceiptStorage verifies size, MIME, metadata hash AND actual bytes with an ETag-bound read.
   The processor then checks its lease owner/version/expiry in a second SQL transaction and marks Ready.
7. Only after that durable Ready does the Function Complete the broker message. Notifications serves
   the authorized attachment, and Gateway streams the response to the authenticated browser.

Read src/Notifications.Api/Receipts in that order: ReceiptAcceptance, ReceiptJob, ReceiptProcessor,
ReceiptStorage, ReceiptEndpoints. The Functions project references this Notifications-owned logic;
no independent service shares these business entities.

## Why multiple acknowledgements are safe

There is no transaction containing SQL, Service Bus and Blob. If the publisher sends then crashes,
a command can be duplicated. If the Function creates Blob then crashes, the object can exist while
SQL still says Pending. If SQL says Ready but broker Complete is lost, the command can run again.
The stable key, conditional creation, byte verification, unique SQL job and lease/version checks make
each retry converge without another logical document. This is not exactly-once message delivery.

A lease is permission for one owner to finalize for a bounded time. Expiry lets a restarted worker
recover; an old owner or version cannot finalize later. A matching Ready duplicate still verifies the
object before settlement. A mismatching object is a durable Conflict, not a successful duplicate.

The actual process test pauses after Blob, kills its own host, then starts another host. It checks SQL,
Blob ETag, broker redelivery and built-in DLQ, beyond what a direct method unit test establishes.
The SQL tests also race two contexts and reject finalization after ownership changes.

## Deliberate contract compatibility

Earlier version1 ReservationConfirmed messages carried only reservationId/userId/summary. They can
still produce their durable simulated notification, and acceptance records a legacy-snapshot audit.
They cannot create a truthful immutable receipt because the accepted details/time were never present.
No historical data is fabricated or fetched cross-schema. All new confirmations produced by this
implementation include the full snapshot. A same-version contradictory snapshot is rejected.

Tour names preserve the accepted original value, including existing permitted whitespace. The text
document prints a JSON-quoted tour-name line so embedded control characters cannot forge field lines.
Money stays integer MXN cents. Date formatting/newlines/hash/key are independent of machine culture.

## Questions to practice

- Which transaction prevents an Inbox dedup entry from hiding a missing ReceiptJob/Outbox?
- Why is metadata hash equality insufficient without checking actual existing bytes?
- What happens when a stale SQL lease owner returns after a replacement worker finishes?
- Why does a pending/failed receipt leave simulated notification delivery and cancellation operational?
- Which local account-key tests do not establish container-scoped managed-identity permissions?
- Why do two HTTP404 responses intentionally hide another user's receipt existence?