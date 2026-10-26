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
| Create reservation: ReservationEndpoints.cs; Reservations/ReservationSubmission.razor | AZ-004 | POST /api/reservations/v1/reservations; tour booking form | Original idempotency/accepted price | AC-A09/10 | Yes; service-owned workflow and web/src/reservations | Real SQL, HTTPS + broker and Angular tests; Chromium NOT VERIFIED | NOT VERIFIED | Independent AZ-004 PASS; AZ-005 detail/control extensions awaiting review |
| List/filter reservations: ReservationEndpoints.cs; Pages/Reservations.razor | AZ-004 | GET /api/reservations/v1/reservations; /reservations | Owner claims; Admin filter | AC-A06/19 | Yes; service-owned workflow and web/src/reservations | Real SQL, HTTPS + broker and Angular tests; Chromium NOT VERIFIED | NOT VERIFIED | Independent AZ-004 PASS; AZ-005 detail/control extensions awaiting review |
| Reservation detail/status: ReservationEndpoints.cs; ReservationDetail.razor | AZ-004 | GET /api/reservations/v1/reservations/{id}; /reservations/:id | Authoritative SQL detail | AC-A10/19 | Yes; service-owned workflow and web/src/reservations | Real SQL, HTTPS + broker and Angular tests; Chromium NOT VERIFIED | NOT VERIFIED | Independent AZ-004 PASS; AZ-005 detail/control extensions awaiting review |
| Reservation timeline: ReservationEndpoints.cs; ReservationDetail.razor | AZ-004 | GET /api/reservations/v1/reservations/{id}/timeline | Persisted transitions | AC-A10/34 | Yes; service-owned workflow and web/src/reservations | Real SQL, HTTPS + broker and Angular tests; Chromium NOT VERIFIED | NOT VERIFIED | Independent AZ-004 PASS; AZ-005 detail/control extensions awaiting review |
| Cancel reservation: ReservationEndpoints.cs; ReservationDetail.razor | AZ-005 | POST /api/reservations/v1/reservations/{id}/cancel | Full refund once; release confirmation | AC-A13/14 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Saga diagnostics/filter: ReservationEndpoints.cs; Pages/Sagas.razor | AZ-005 | GET /api/reservations/v1/admin/sagas; /admin/sagas | Backend Admin; durable flags | AC-A13/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Retry compensation: ReservationEndpoints.cs; Pages/Sagas.razor | AZ-005 | POST /api/reservations/v1/admin/sagas/{id}/retry-compensation | Stable operation IDs | AC-A13/15 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Informational price projection: ReservationEndpoints.cs; TourProjections.razor | AZ-005 | GET /api/reservations/v1/projections/tour-sessions; /tour-projections | Retain separately from Cosmos dashboard | AC-A17/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Payment list/filter: PaymentEndpoints.cs; Pages/Payments.razor | AZ-004 | GET /api/payments/v1/payments; /payments | Simulated; owner/Admin | AC-A06/12 | Yes; service-owned workflow and web/src/reservations | Real SQL, HTTPS + broker and Angular tests; Chromium NOT VERIFIED | NOT VERIFIED | Independent AZ-004 PASS; AZ-005 detail/control extensions awaiting review |
| Payment/refund detail: PaymentEndpoints.cs; Pages/Payments.razor | AZ-005 | GET /api/payments/v1/payments/{id}; /payments/:id | Independent durable fictional provider | AC-A12/13 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Reconcile payment: PaymentEndpoints.cs; Pages/Simulations.razor | AZ-005 | POST /api/payments/v1/admin/payments/{id}/reconcile | Same charge ID | AC-A14 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Resolve fictional provider: PaymentEndpoints.cs; Pages/Simulations.razor | AZ-005 | POST /api/payments/v1/admin/fake-provider/{id}/resolve | Resolve provider then reconcile; never edit Saga | AC-A14 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Notification list/filter: NotificationEndpoints.cs; Pages/Notifications.razor | AZ-004 | GET /api/notifications/v1/notifications; /notifications | Simulated delivery labels | AC-A10/19 | Yes; service-owned workflow and web/src/reservations | Real SQL, HTTPS + broker and Angular tests; Chromium NOT VERIFIED | NOT VERIFIED | Independent AZ-004 PASS; AZ-005 detail/control extensions awaiting review |
| Notification detail/status: NotificationEndpoints.cs; Pages/Notifications.razor | AZ-004 | GET /api/notifications/v1/notifications/{id}; /notifications/:id | Durable sender receipts preserved | AC-A10/19 | Yes; service-owned workflow and web/src/reservations | Real SQL, HTTPS + broker and Angular tests; Chromium NOT VERIFIED | NOT VERIFIED | Independent AZ-004 PASS; AZ-005 detail/control extensions awaiting review |
| Catalog read fault selection: CatalogEndpoints.cs; Pages/Simulations.razor | AZ-005 | GET faults under /api/catalog/v1/admin | Bounded/audited lab-only modes | AC-A16/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Catalog set fault selection: CatalogEndpoints.cs; Pages/Simulations.razor | AZ-005 | PUT faults under /api/catalog/v1/admin | Bounded/audited lab-only modes | AC-A16/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Catalog reset fault selection: CatalogEndpoints.cs; Pages/Simulations.razor | AZ-005 | POST faults/reset under /api/catalog/v1/admin | Bounded/audited lab-only modes | AC-A16/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Reservations read fault selection: Shared.Infrastructure/Operations/ServiceOperations.cs; Simulations.razor | AZ-005 | GET faults under /api/reservations/v1/admin | Pause preserves durable work | AC-A16/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Reservations set fault selection: Shared.Infrastructure/Operations/ServiceOperations.cs; Simulations.razor | AZ-005 | PUT faults under /api/reservations/v1/admin | Pause preserves durable work | AC-A16/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Reservations reset fault selection: Shared.Infrastructure/Operations/ServiceOperations.cs; Simulations.razor | AZ-005 | POST faults/reset under /api/reservations/v1/admin | Pause preserves durable work | AC-A16/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Payments read fault selection: PaymentEndpoints.cs; Simulations.razor | AZ-005 | GET faults under /api/payments/v1/admin | Capture mode per operation | AC-A14/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Payments set fault selection: PaymentEndpoints.cs; Simulations.razor | AZ-005 | PUT faults under /api/payments/v1/admin | Capture mode per operation | AC-A14/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Payments reset fault selection: PaymentEndpoints.cs; Simulations.razor | AZ-005 | POST faults/reset under /api/payments/v1/admin | Capture mode per operation | AC-A14/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Notifications read fault selection: NotificationEndpoints.cs; Simulations.razor | AZ-005 | GET faults under /api/notifications/v1/admin | Pending retry survives Inbox | AC-A19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Notifications set fault selection: NotificationEndpoints.cs; Simulations.razor | AZ-005 | PUT faults under /api/notifications/v1/admin | Pending retry survives Inbox | AC-A19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Notifications reset fault selection: NotificationEndpoints.cs; Simulations.razor | AZ-005 | POST faults/reset under /api/notifications/v1/admin | Pending retry survives Inbox | AC-A19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Per-service diagnostics/page: Shared.Infrastructure/Operations/ServiceOperations.cs; Operations.razor | AZ-005 | GET /api/{service}/v1/admin/diagnostics; /operations | All 4 domains; redact and bound | AC-A19/34 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Per-service DLQ inspect: Shared.Infrastructure/Operations/ServiceOperations.cs; Operations.razor | AZ-005 | GET /api/{service}/v1/admin/dead-letters; /operations | Document peek versus receive/lock | AC-A18/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| Per-service DLQ replay: Shared.Infrastructure/Operations/ServiceOperations.cs; Operations.razor | AZ-005 | POST /api/{service}/v1/admin/dead-letters/{id}/replay | Built-in DLQ; topic replay semantics | AC-A18/19 | Yes; owned recovery/operations APIs and Angular Admin/effect controls | SQL/HTTP, actual broker/HTTPS and Angular checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-005 review |
| View ready tour images: Catalog.Api/Images/ImageEndpoints.cs; Components/Catalog/TourImages.razor | AZ-006 | GET /api/catalog/v1/tours/{id}/images; /tours/:id | Private Blob proxy replaces signed URL | AC-A22 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Admin image status list: Images/ImageEndpoints.cs; TourImages.razor | AZ-006 | GET /api/catalog/v1/admin/tours/{id}/images | Pending/ready/delete states | AC-A22/23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Upload image: Images/ImageEndpoints.cs; TourImages.razor | AZ-006 | PUT /api/catalog/v1/admin/tours/{id}/images/{imageId} | Keep source2MiB/3 slots; bounded JPEG/PNG | AC-A22/23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Delete image: Images/ImageEndpoints.cs; TourImages.razor | AZ-006 | DELETE /api/catalog/v1/admin/tours/{id}/images/{imageId} | Intent/recovery/tombstone prevents resurrection | AC-A23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| My bookings page/pagination: Reservations.Api/Bookings/BookingProjection.cs; MyBookings.razor | AZ-007 | GET /api/reservations/v1/my-bookings; /my-bookings | Cosmos user partition; departure ordering | AC-A24/25 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| My booking projection point read: Bookings/BookingProjection.cs; MyBookings.razor | AZ-007 | GET /api/reservations/v1/my-bookings/{id} | Eventually consistent; authoritative detail link | AC-A24/26 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Admin projection rebuild: Bookings/BookingProjection.cs; MyBookings.razor | AZ-007 | POST /api/reservations/v1/admin/my-bookings/rebuild | Bounded SQL snapshots; ETag guard | AC-A26 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Image recovery diagnostics: Images/TourImages.cs (worker; no dedicated source HTTP recovery action) | AZ-006 | Admin image status/recovery control design in006 | Do not invent source endpoint; exact-key worker recovery | AC-A23 | No; scheduled | NOT VERIFIED | NOT VERIFIED | Pending |
| Receipt status (new): Absent; source sender receipts are internal durable effects | AZ-008 | GET /api/notifications/v1/receipts/reservation/{id}; booking detail | Immutable historical simulated confirmation | AC-A27/29 | Yes | Real SQL/Functions/HTTPS and NGXS checks; Chromium NOT VERIFIED | NOT VERIFIED | Awaiting AZ-008 review |
| Receipt download (new): Absent | AZ-008 | GET /api/notifications/v1/receipts/{id}/content; booking detail | Private Blob; owner404; simulated attachment | AC-A27/28/29 | Yes | Actual Functions crash/restart, Blob ETag and HTTPS download | NOT VERIFIED | Awaiting AZ-008 review |
| Session expiry/account change/navigation: Gateway Identity and Blazor; frontend/angular/src/app | AZ-002/010 | Angular auth state and routes | No cross-user state or stored JWT | AC-A20/21 | Yes; baseline guards/reset/loading and navigation controls | HTTP/NGXS verified; full Chromium navigation NOT VERIFIED (AZ-010) | NOT VERIFIED | AZ-002 PASS; AZ-005 extension awaiting review |
## Foundation delivered now

| Capability | Code | Acceptance | Implemented | Local evidence | Azure evidence | Independent review |
|---|---|---|---|---|---|---|
| Five process hosts; unavailable dependency readiness | src/*.Api, Shared.Infrastructure | AC-A02 | Yes | Release build;11 host/routing tests | NOT VERIFIED | Pending |
| Angular21/NGXS shell/status/error/cancellation | web/tours-angular | AC-A02; foundation subset of20 | Yes | Production build;3 NGXS tests | NOT VERIFIED | Pending |
| Isolated Functions receipt worker | src/Receipts.Functions | AC-A02/27/28 | Yes | Actual Core Tools, broker/Azurite, crash/restart and DLQ | NOT VERIFIED | Awaiting AZ-008 review |
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

## AZ-004 evidence boundary

Booking creation/list/detail/timeline and personal payment/notification pages are implemented.
AC-A09..12/17 have real SQL and actual local broker/HTTPS evidence in evidence/AZ-004.md.
AC-A18 poison-to-built-in-DLQ is exercised; Admin audited replay remains AZ-005.
AC-A20 booking state is unit/render tested; Chromium and full AC-A21 remain NOT VERIFIED.
Internal safe compensation/reconciliation guards are preserved, but no AZ-005 parity row is advanced
by a success booking. The payment detail HTTP read is present for ownership checks; full refund/admin UI
and cancellation/fault/diagnostic controls remain scheduled. No cloud acceptance is inferred.

## AZ-005 evidence boundary

All original baseline human actions now have API/Angular controls, including cancellation, Saga recovery,
provider resolution/reconciliation, fault/reset, informational projections, personal effect/refund details
and per-service diagnostics/DLQ. See recovery-api-map.md and evidence/AZ-005.md.
Storage/Cosmos/receipt extensions remain explicitly scheduled AZ-006/007/008.
94 real SQL-suite tests,13 actual live broker/HTTPS checks,29 foundation tests and50 Angular tests passed.
Seven browser tests are discovered; Chromium and complete AC-A21 execution remain NOT VERIFIED.
AC-A34 evidence covers the current five API/message/worker paths; Functions/cloud claims remain future.
Independent Antigravity AZ-005 review passed on2026-10-24; see reviews/AZ-005.md.

## AZ-006 evidence boundary

Tour detail now includes ready image display and explicit pending/broken/empty placeholders.
Admin has typed Angular/NGXS raw upload, remove, reload and one-item recovery controls.
Backend independently enforces human/Admin identity, tour visibility, owned GUID lookup,
decode/size/dimension/count bounds and authenticated no-store content proxying.
AC-A22/23 are mapped to actual SQL/Azurite and HTTPS process checks in evidence/AZ-006.md.
SQL intents precede Blob I/O; guarded completion/deletion, retained tombstones, durable history,
competing leases and restarted hosted recovery are implemented. No broad purge or browser credentials.
AC-A20 image state/account/navigation/reset/error tests execute. The eighth browser smoke is
implemented/discovered; Chromium and complete AC-A21/33 remain NOT VERIFIED.
Cosmos and receipt Functions remain AZ-007/008. Independent AZ-006 review is pending.

## AZ-007 evidence boundary

Original Bookings/BookingProjection.cs and BookingStore.cs actions are covered by Cosmos SDK,
Reservations transactional snapshots and the typed Angular/NGXS dashboard:

| Original action/capability | Azure implementation | Acceptance/evidence |
|---|---|---|
| GET /v1/my-bookings | GET /v1/dashboard/bookings; /dashboard first/next page | AC-A24, protected own-partition keyset pages |
| GET /v1/my-bookings/{id} | GET /v1/dashboard/bookings/{id}; /dashboard/{id} | AC-A06/24, owner404 and authoritative link |
| POST /v1/admin/my-bookings/rebuild | POST /v1/admin/dashboard/rebuild; Admin start/continue controls | AC-A26, bounded/audited normal Outbox snapshots |
| Conditional newer-version projection | Cosmos create409 / IfMatchEtag412 re-read guard | AC-A25/26, actual emulator/SDK + broker redelivery |
| Dashboard stale/unavailable/user reset | DashboardState typed clients/selectors/actions/error/trace state | AC-A19/20, Angular unit/render checks |

Source maximum20 page rows and4KiB cursor limits remain. Departure DESC/id ASC follows the explicit
Azure specification instead of the source creation-key ordering. Original repo remains unchanged.
Cosmos/SQL, broker settlement, HTTPS booking/cancel/rebuild/poison/restart and browser discovery are
separate in evidence/AZ-007.md. Normal reads never cross partitions or silently fall back to SQL.
Actual Azure index enforcement/RBAC/authentication and Chromium remain NOT VERIFIED.
AZ-006 has independent PASS; independent AZ-007 review is pending. Receipts/Functions remain AZ-008.

## AZ-008 evidence boundary

| New capability | Azure implementation | Acceptance/evidence |
|---|---|---|
| Immutable confirmation job | Notifications Inbox + ReceiptJob/snapshot + Outbox in one SQL transaction | AC-A27/28; real SQL uniqueness, rollback, source-version checks |
| Receipt generation | .NET10 isolated GenerateBookingReceipt Service Bus trigger + private Blob | AC-A27; actual Core Tools/broker/Azurite |
| Crash/duplicate/concurrency recovery | Durable SQL lease/version + deterministic conditional Blob create/byte verification | AC-A28; actual host kill/restart, same ETag, duplicate and conflict DLQ |
| Status/download authorization | Notifications owner/Admin status and verified bounded attachment via Gateway | AC-A06/29; other owner404, anonymous401, real HTTPS download |
| Historical confirmation UI | Booking detail ReceiptsPanel, ReceiptsState, typed API/download actions | AC-A19/20/29; Angular stale/reset/error/render checks |
| Later cancellation | Current authoritative booking state shown separately; same historical document bytes retained | AC-A29; actual HTTPS booking/cancel plus normal simulated notifications |

AZ-006, AZ-007 and AZ-008 have actual independent PASS reviews; see docs/reviews/.
Legacy version1 events without accepted snapshot retain simulated notification behavior and a durable
legacy audit; no historical receipt is invented. All new confirmed booking events carry the snapshot.
Full Chromium execution and real Azure managed-identity/RBAC/Functions hosting remain NOT VERIFIED.

## AZ-009 security readiness boundary

| Capability | Implementation | Evidence boundary |
|---|---|---|
| Explicit workload identity | AzureSecurity + six host bootstraps + SDK/SQL/Functions identity paths | AC-A30; offline/framework tests; cloud NOT VERIFIED |
| Versioned signing secrets | StartupSecrets/VaultSecretReader + public fingerprint and two-key validation | AC-A05/30; real SDK with MOCKED HTTPS; no real vault |
| Durable protected keys | Gateway Identity SQL + vault key; separate dashboard Blob/vault ring; local previous certificate support | AC-A04/30; real SQL/provider rollover and actual local cookie restart |
| SQL least privilege | explicit object-ID artifact generator; separate migrators; receipt state/audit and immutable-column denials | AC-A03/30; actual SQL impersonation/receipt work; Entra authentication NOT VERIFIED |
| Cloud config/ingress | endpoint/mode/TLS/origin/proxy guards, offline helper and public templates | AC-A30; forged-header and negative-config checks |
| Local regression | existing human APIs, Angular state, images, Cosmos dashboard and receipt Functions |174regression passes;7actual host/HTTPS passes;72Angular passes |

No new human action or secret/configuration UI was introduced. Existing user/Admin features and browser
security boundaries remain intact. Actual independent AZ-008 PASS exists; AZ-009 is Implemented
Awaiting Review. Full Chromium and actual Azure identity/RBAC/Key Vault/hosting remain NOT VERIFIED.
## AZ-010 current verification boundary

Earlier NOT VERIFIED browser statements record the earlier task's execution state. Current named
browser flows are executed12/12 with strict TLS against actual APIs, SQL/emulators and Functions;
see browser-coverage.md and evidence/AZ-010.md for the exact action-to-test map. This does not
retroactively make every historic UI fault permutation or real Azure deployment verified.

| Capability | Current local proof | Independent review / cloud |
|---|---|---|
| Human baseline plus Blob/Cosmos/receipt extensions |190 selected .NET cases,74 Angular unit tests and12 actual Chromium cases | AZ001..009 actual PASS; AZ010 awaiting review; Azure NOT VERIFIED |
| Session/navigation and ownership | Concurrent Alice/Bob contexts, secure cookie, refresh/back/deep links, account change, real401 reset, owner404 | AC-A04/06/20/21/33; no browser JWT/state persistence |
| Full success/cancel and historical document | Actual broker workers/Functions, one simulated refund, immutable downloaded bytes after cancellation, Bob404 | AC-A10/13/27/29/33/34; trace confirmed in actual Function log |
| Repeatable isolated CI artifacts | Pinned build/test/image/package steps, fresh per-phase broker, explicit two-license gates, private reports/count summaries | AC-A32; actual local execution; remote hosted run NOT VERIFIED |
| Study preparation | Angular19/21 comparison, study map, interview demo and reproducible commands | Learning material, not proof of mastery or cloud readiness |

No original resource was used as a writable fixture. Before/after commit, status and all416tracked
source hashes match. No test assertion, business deadline, authorization policy or TLS validation
was relaxed. See decisionsD64..67 for fixture isolation and the observed corrections.


## AZ-011 hosting preparation boundary

AC-A31 artifacts now include Bicep/App Service/Flex modules, exact runtime/data roles, hashed code/SQL
packages, one-shot migration/seed and guarded lifecycle/manual OIDC procedures with official cost
model. Offline Bicep/lint,132selected .NET cases,74Angular tests and actual release validation passed.
Original business/API/UI parity remains as recorded above. Public authenticated network exposure,
unknown platform proxy topology, Linux/B2 capacity, real identity/permission and cloud lifecycle
proof are explicitly NOT VERIFIED. No prepared resource is represented as deployed. Independent
AZ-011 review is pending; AC-A35/36 remain AZ-012 after explicit authorization.
