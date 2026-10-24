# AZ-005 human action and API map

Browser requests pass through /api/{service}; each backend independently validates its JWT and policies.
All browser mutations also require the BFF cookie's antiforgery/origin checks. Guards are UX only.
Participants, prices and stable operation IDs retain the authoritative source/Azure contracts.

| Action | Backend path (after proxy prefix) | Angular route/control |
| --- | --- | --- |
| Cancel confirmed future booking | POST reservations /v1/reservations/{id}/cancel; Idempotency-Key required | /reservations/:id; Cancel confirmed booking |
| Filter/page authoritative bookings | GET reservations /v1/reservations?status=&userId=&page=&pageSize= | /reservations; status/owner filters and paging |
| Saga diagnostics/filter | GET reservations /v1/admin/sagas?state=&page= | /admin/sagas |
| Retry compensation | POST reservations /v1/admin/sagas/{id}/retry-compensation | Saga row; pending payment must first be reconciled |
| Informational price projection | GET reservations /v1/projections/tour-sessions?page=&pageSize= | /tour-projections; explicit stale/authoritative distinction |
| Payment/refund detail | GET payments /v1/payments/{id} | /payments/:id; simulated payment/refund fields |
| Payment reconcile | POST payments /v1/admin/payments/{id}/reconcile | /simulations; same operation ID |
| Fictional provider resolve | POST payments /v1/admin/fake-provider/{id}/resolve; {outcome: Succeeded or NotCharged} | /simulations; explicit resolve then reconcile |
| Fault read/select/reset, all four domains | GET/PUT /v1/admin/faults; POST /v1/admin/faults/reset | /simulations; service/mode/occurrences/reactive validation |
| Bounded redacted diagnostics/audit | GET each domain /v1/admin/diagnostics?page=&pageSize= | /operations; service and metadata paging |
| Built-in DLQ peek | GET each domain /v1/admin/dead-letters?source=commands or events&pageSize=&fromSequence= | /operations; first/next peek page |
| Audited DLQ replay | POST each domain /v1/admin/dead-letters/{inspectionId}/replay | /operations; safe inspected-item replay |
| Notification detail | GET notifications /v1/notifications/{id} | /notifications/:id; simulated status/content |
| Personal payment/notification list/filter | GET each owned /v1/payments or /v1/notifications?reservationId=&page=&pageSize= | /payments, /notifications; reservation filter/paging |

Human reads are owned or Admin; another owner's detail returns404. All admin mutation/diagnostic/
fault/DLQ paths require backend Admin; fault/DLQ features additionally require explicit local enablement.
Cancellation returns its stored202 acceptance, not a promised completed refund. Query pages default10,
max50 (Saga page fixed20). Diagnostic pages max50, DLQ peek max10 and replay search max50.
Invalid input400, unauthenticated401, role403, owned/not-found404, unsafe state/key409 and unavailable
dependency503 remain distinct. Safe ProblemDetails includes a code and traceId.

Baseline tour/Identity actions remain in their existing Angular features. Storage images/recovery,
Cosmos dashboard/rebuild and receipt downloads remain AZ-006/007/008, as explicitly scheduled.
No baseline human action was replaced by browser-direct SQL or Service Bus access.
