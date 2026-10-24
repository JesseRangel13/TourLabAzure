# Human-action parity matrix

Source paths below are relative to the original repository (Gateway UI paths abbreviate src/Gateway.Web/Components). Source observed means code inspected; source execution/review evidence is summarized in source-inventory.md. Future routes are plans, not implemented APIs. No user-approved functional exclusions have been granted.

| Original capability/file or contract | Azure task | New endpoint/UI/code | Compatibility decision | Acceptance ID | Implemented | Locally verified | Azure verified | Reviewer evidence |
|---|---|---|---|---|---|---|---|---|
| Login: Gateway.Web/Identity/AuthEndpoints.cs; Pages/Login.razor | AZ-002 | POST /auth/login; /login | Cookie/CSRF BFF; fresh identities | AC-A04/06 | Yes; code paths in Gateway/Identity, Browser and web/src/auth | SQL HTTP/BFF/NGXS verified; Chromium NOT VERIFIED | NOT VERIFIED | Pending AZ-002 review |
| Logout: Identity/AuthEndpoints.cs; Pages/Logout.razor | AZ-002 | POST /auth/logout; header logout | Reset all user state | AC-A04/20 | Yes; code paths in Gateway/Identity, Browser and web/src/auth | SQL HTTP/BFF/NGXS verified; Chromium NOT VERIFIED | NOT VERIFIED | Pending AZ-002 review |
| Current user/account: Identity/AuthEndpoints.cs; Pages/Account.razor | AZ-002 | GET /auth/me; /account | HTTP session replaces Blazor circuit | AC-A04/06 | Yes; code paths in Gateway/Identity, Browser and web/src/auth | SQL HTTP/BFF/NGXS verified; Chromium NOT VERIFIED | NOT VERIFIED | Pending AZ-002 review |
| API-tool credential token issuance: Identity/AuthEndpoints.cs /auth/token | AZ-002 | POST /auth/token (tool only) | Retain credential/rate-limit boundary; no browser export | AC-A05 | Yes; code paths in Gateway/Identity, Browser and web/src/auth | SQL HTTP/BFF/NGXS verified; Chromium NOT VERIFIED | NOT VERIFIED | Pending AZ-002 review |
| CSRF token acquisition: Browser/BrowserEndpoints.cs /bff/session | AZ-002 | GET /auth/csrf | New specified SPA token contract | AC-A04 | Yes; code paths in Gateway/Identity, Browser and web/src/auth | SQL HTTP/BFF/NGXS verified; Chromium NOT VERIFIED | NOT VERIFIED | Pending AZ-002 review |
| Tour search/list/page: CatalogEndpoints.cs; Pages/Tours.razor | AZ-003 | GET /api/catalog/v1/tours; /tours | Typed HTTP/NGXS | AC-A07/20 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Tour detail: CatalogEndpoints.cs; Pages/TourDetail.razor | AZ-003 | GET /api/catalog/v1/tours/{id}; /tours/:id | Preserve active/Admin visibility | AC-A07 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Session prices/availability: CatalogEndpoints.cs; TourDetail.razor | AZ-003 | GET /api/catalog/v1/tours/{id}/sessions | SQL Server atomic inventory | AC-A07/08 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Create tour: CatalogEndpoints.cs; CatalogAdmin.razor | AZ-003 | POST /api/catalog/v1/admin/tours; /admin/catalog | Backend Admin enforcement | AC-A07 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Edit/activate/inactivate tour: CatalogEndpoints.cs; CatalogEdit.razor | AZ-003 | PUT /api/catalog/v1/admin/tours/{id}; /admin/catalog/:id | Expected version; inactive prevents holds | AC-A07/08 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Create session: CatalogEndpoints.cs; Components/Catalog/SessionEditor.razor | AZ-003 | POST /api/catalog/v1/admin/tours/{id}/sessions | Future departure/capacity | AC-A07 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Change session price: CatalogEndpoints.cs; SessionEditor.razor | AZ-003 | PUT /api/catalog/v1/admin/sessions/{id}/price | Snapshot and price version | AC-A07/08 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Change session capacity: CatalogEndpoints.cs; SessionEditor.razor | AZ-003 | PUT /api/catalog/v1/admin/sessions/{id}/capacity | Never below occupied | AC-A07/08 | Yes; Catalog/Application and web/src/catalog | SQL/HTTP and Angular tests executed; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-003 review |
| Create reservation: ReservationEndpoints.cs; Reservations/ReservationSubmission.razor | AZ-004 | POST /api/reservations/v1/reservations; tour booking form | Original idempotency/accepted price | AC-A09/10 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| List/filter reservations: ReservationEndpoints.cs; Pages/Reservations.razor | AZ-004 | GET /api/reservations/v1/reservations; /reservations | Owner claims; Admin filter | AC-A06/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Reservation detail/status: ReservationEndpoints.cs; ReservationDetail.razor | AZ-004 | GET /api/reservations/v1/reservations/{id}; /reservations/:id | Authoritative SQL detail | AC-A10/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Reservation timeline: ReservationEndpoints.cs; ReservationDetail.razor | AZ-004 | GET /api/reservations/v1/reservations/{id}/timeline | Persisted transitions | AC-A10/34 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Cancel reservation: ReservationEndpoints.cs; ReservationDetail.razor | AZ-005 | POST /api/reservations/v1/reservations/{id}/cancel | Full refund once; release confirmation | AC-A13/14 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Saga diagnostics/filter: ReservationEndpoints.cs; Pages/Sagas.razor | AZ-005 | GET /api/reservations/v1/admin/sagas; /admin/sagas | Backend Admin; durable flags | AC-A13/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Retry compensation: ReservationEndpoints.cs; Pages/Sagas.razor | AZ-005 | POST /api/reservations/v1/admin/sagas/{id}/retry-compensation | Stable operation IDs | AC-A13/15 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Informational price projection: ReservationEndpoints.cs; TourProjections.razor | AZ-005 | GET /api/reservations/v1/projections/tour-sessions; /tour-projections | Retain separately from Cosmos dashboard | AC-A17/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Payment list/filter: PaymentEndpoints.cs; Pages/Payments.razor | AZ-004 | GET /api/payments/v1/payments; /payments | Simulated; owner/Admin | AC-A06/12 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Payment/refund detail: PaymentEndpoints.cs; Pages/Payments.razor | AZ-005 | GET /api/payments/v1/payments/{id}; /payments/:id | Independent durable fictional provider | AC-A12/13 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Reconcile payment: PaymentEndpoints.cs; Pages/Simulations.razor | AZ-005 | POST /api/payments/v1/admin/payments/{id}/reconcile | Same charge ID | AC-A14 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Resolve fictional provider: PaymentEndpoints.cs; Pages/Simulations.razor | AZ-005 | POST /api/payments/v1/admin/fake-provider/{id}/resolve | Resolve provider then reconcile; never edit Saga | AC-A14 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Notification list/filter: NotificationEndpoints.cs; Pages/Notifications.razor | AZ-004 | GET /api/notifications/v1/notifications; /notifications | Simulated delivery labels | AC-A10/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Notification detail/status: NotificationEndpoints.cs; Pages/Notifications.razor | AZ-004 | GET /api/notifications/v1/notifications/{id}; /notifications/:id | Durable sender receipts preserved | AC-A10/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Catalog read fault selection: CatalogEndpoints.cs; Pages/Simulations.razor | AZ-005 | GET faults under /api/catalog/v1/admin | Bounded/audited lab-only modes | AC-A16/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Catalog set fault selection: CatalogEndpoints.cs; Pages/Simulations.razor | AZ-005 | PUT faults under /api/catalog/v1/admin | Bounded/audited lab-only modes | AC-A16/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Catalog reset fault selection: CatalogEndpoints.cs; Pages/Simulations.razor | AZ-005 | POST faults/reset under /api/catalog/v1/admin | Bounded/audited lab-only modes | AC-A16/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Reservations read fault selection: Shared.Infrastructure/Operations/ServiceOperations.cs; Simulations.razor | AZ-005 | GET faults under /api/reservations/v1/admin | Pause preserves durable work | AC-A16/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Reservations set fault selection: Shared.Infrastructure/Operations/ServiceOperations.cs; Simulations.razor | AZ-005 | PUT faults under /api/reservations/v1/admin | Pause preserves durable work | AC-A16/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Reservations reset fault selection: Shared.Infrastructure/Operations/ServiceOperations.cs; Simulations.razor | AZ-005 | POST faults/reset under /api/reservations/v1/admin | Pause preserves durable work | AC-A16/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Payments read fault selection: PaymentEndpoints.cs; Simulations.razor | AZ-005 | GET faults under /api/payments/v1/admin | Capture mode per operation | AC-A14/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Payments set fault selection: PaymentEndpoints.cs; Simulations.razor | AZ-005 | PUT faults under /api/payments/v1/admin | Capture mode per operation | AC-A14/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Payments reset fault selection: PaymentEndpoints.cs; Simulations.razor | AZ-005 | POST faults/reset under /api/payments/v1/admin | Capture mode per operation | AC-A14/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Notifications read fault selection: NotificationEndpoints.cs; Simulations.razor | AZ-005 | GET faults under /api/notifications/v1/admin | Pending retry survives Inbox | AC-A19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Notifications set fault selection: NotificationEndpoints.cs; Simulations.razor | AZ-005 | PUT faults under /api/notifications/v1/admin | Pending retry survives Inbox | AC-A19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Notifications reset fault selection: NotificationEndpoints.cs; Simulations.razor | AZ-005 | POST faults/reset under /api/notifications/v1/admin | Pending retry survives Inbox | AC-A19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Per-service diagnostics/page: Shared.Infrastructure/Operations/ServiceOperations.cs; Operations.razor | AZ-005 | GET /api/{service}/v1/admin/diagnostics; /operations | All 4 domains; redact and bound | AC-A19/34 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Per-service DLQ inspect: Shared.Infrastructure/Operations/ServiceOperations.cs; Operations.razor | AZ-005 | GET /api/{service}/v1/admin/dead-letters; /operations | Document peek versus receive/lock | AC-A18/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Per-service DLQ replay: Shared.Infrastructure/Operations/ServiceOperations.cs; Operations.razor | AZ-005 | POST /api/{service}/v1/admin/dead-letters/{id}/replay | Built-in DLQ; topic replay semantics | AC-A18/19 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| View ready tour images: Catalog.Api/Images/ImageEndpoints.cs; Components/Catalog/TourImages.razor | AZ-006 | GET /api/catalog/v1/tours/{id}/images; /tours/:id | Private Blob proxy replaces signed URL | AC-A22 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Admin image status list: Images/ImageEndpoints.cs; TourImages.razor | AZ-006 | GET /api/catalog/v1/admin/tours/{id}/images | Pending/ready/delete states | AC-A22/23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Upload image: Images/ImageEndpoints.cs; TourImages.razor | AZ-006 | PUT /api/catalog/v1/admin/tours/{id}/images/{imageId} | Keep source2MiB/3 slots; bounded JPEG/PNG | AC-A22/23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Delete image: Images/ImageEndpoints.cs; TourImages.razor | AZ-006 | DELETE /api/catalog/v1/admin/tours/{id}/images/{imageId} | Intent/recovery/tombstone prevents resurrection | AC-A23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| My bookings page/pagination: Reservations.Api/Bookings/BookingProjection.cs; MyBookings.razor | AZ-007 | GET /api/reservations/v1/my-bookings; /my-bookings | Cosmos user partition; departure ordering | AC-A24/25 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| My booking projection point read: Bookings/BookingProjection.cs; MyBookings.razor | AZ-007 | GET /api/reservations/v1/my-bookings/{id} | Eventually consistent; authoritative detail link | AC-A24/26 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Admin projection rebuild: Bookings/BookingProjection.cs; MyBookings.razor | AZ-007 | POST /api/reservations/v1/admin/my-bookings/rebuild | Bounded SQL snapshots; ETag guard | AC-A26 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Image recovery diagnostics: Images/TourImages.cs (worker; no dedicated source HTTP recovery action) | AZ-006 | Admin image status/recovery control design in006 | Do not invent source endpoint; exact-key worker recovery | AC-A23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Receipt status (new): Absent; source sender receipts are internal durable effects | AZ-008 | Notifications owned receipt status; reservation detail control | New immutable educational confirmation | AC-A27/29 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Receipt download (new): Absent | AZ-008 | Authorized Notifications download; reservation detail control | Private Blob; owner404; simulated text | AC-A27/28/29 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Session expiry/account change/navigation: Gateway Identity and Blazor; frontend/angular/src/app | AZ-002/010 | Angular auth state and routes | No cross-user state or stored JWT | AC-A20/21 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
## Foundation delivered now

| Capability | Code | Acceptance | Implemented | Local evidence | Azure evidence | Independent review |
|---|---|---|---|---|---|---|
| Five process hosts; unavailable dependency readiness | src/*.Api, Shared.Infrastructure | AC-A02 | Yes | Release build;11 host/routing tests | NOT VERIFIED | Pending |
| Angular21/NGXS shell/status/error/cancellation | web/tours-angular | AC-A02; foundation subset of20 | Yes | Production build;3 NGXS tests | NOT VERIFIED | Pending |
| Isolated Functions scaffold | src/Receipts.Functions | AC-A02 | Yes | net10 build/metadata; host NOT VERIFIED | NOT VERIFIED | Pending |
| Source audit/isolation evidence | docs/source-*.md/json, scripts/Record-SourceSnapshot.ps1 | AC-A01 | Yes | Before/after comparison in task evidence | Not applicable | Pending |

Later-task business acceptance IDs are mappings, not passes. Foundational browser smoke does not establish
booking/login/Admin parity. Source planned/absent features are explicitly identified above.
## AZ-002 evidence boundary

Foundation auth/SQL capability is implemented. AC-A03 real SQL permission/concurrency/migration evidence
and AC-A04/05 HTTP/JWT/key-restart evidence are in evidence/AZ-002.md.
AC-A06 account/claim/role isolation is exercised; reservation/payment ownership404 remains its actual
resource tasks. Angular auth/guard unit evidence is distinct from unexecuted Chromium UI verification.
No later business capability row is advanced by an auth test.

## AZ-003 evidence boundary

AC-A07 Catalog actions now have secured HTTP handlers and Angular reactive controls.
AC-A08 includes real SQL Server final-seat contention, quote reuse/price snapshots, hold/confirm/
expiry/release/tombstone, rollback and actual hosted expiry worker checks. AC-A20 includes Catalog
selectors, stale/cancelled loads, account resets, mutation conflict/errors and typed forms.
See evidence/AZ-003.md and catalog-execution.md for exact executed results.
Playwright Catalog smoke is implemented and discovered, but Chromium TLS trust still blocks
browser execution. No AC-A21/33 browser pass, broker delivery, booking, images or cloud proof is implied.