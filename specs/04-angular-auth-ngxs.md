# 04 — Angular 21, NGXS, APIs, and authentication

## Hosting and identity
Implement a new Angular21 SPA; do not embed Blazor or modify the original frontend. Pin compatible Node (e.g. a verified Node22 LTS patch), TypeScript, RxJS, Angular CLI and NGXS using official peer dependencies; commit package-lock and use npm ci. Do not silently install Angular22 or NGXS latest. Use standalone components, typed reactive forms, dependency injection, typed HttpClient services, RxJS with correct cancellation/disposal and clear templates. No elaborate design system required.

Gateway hosts production SPA assets and API/auth routes on the same origin; local Angular proxy mirrors that topology. SPA fallback cannot swallow /api, /auth, health or static asset errors. HTTPS local, fresh dev credentials/certificates, no permissive production CORS.

Keep Identity Tourist/Admin seeded users with fresh passwords privately provisioned. Browser uses a Secure/HttpOnly appropriate SameSite cookie, not a stored JWT. Gateway issues/attaches backend JWTs per authenticated request. Backends validate signature/issuer/audience/expiry/claims independently. Catalog quote creation uses a separate Reservations service credential, not the browser session. Preserve durable encrypted Data Protection keys and expiry/revalidation behavior as appropriate to HTTP; Blazor circuit concerns do not apply to Angular.

Specify and implement GET /auth/csrf, POST /auth/login, POST /auth/logout, GET /auth/me. Use ASP.NET Core antiforgery on cookie-authenticated mutations including login/logout; align Angular XSRF cookie/header names (e.g. readable XSRF-TOKEN request token + X-XSRF-TOKEN header) with the server's separate antiforgery cookie. Readable CSRF token is not the authentication credential. Integration-test missing/invalid tokens and origin handling. Guards are UX only; backend enforces authorization. Clear user state on logout, expiry, account change and 401. Do not add public registration/recovery or Entra end-user login without separate scope.

## NGXS requirements
Use NGXS meaningfully for shared feature state, not every keystroke. Suggested AuthState, CatalogState, ReservationsState, DashboardState, AdminState. Actions LoadTours, LoadSessions, CreateReservation, LoadReservation, CancelReservation, LoadDashboardPage, Logout etc. return observable completion and propagate classified failures; loading flags reset on all paths. Use selectors and immutable updates, no hidden direct HTTP calls in templates.

Typed API clients perform transport; states orchestrate data/loading/errors; components dispatch and render. Scope feature state and avoid a single giant state file. Form-local transient state can remain in the component. Handle stale responses and concurrent dispatches so an old query cannot overwrite a new filter or another authenticated user's data. Prefer cancellation or request identity guards. Do not automatically retry non-idempotent mutations with new idempotency keys.

No JWT/cookie/password/signed URL persistence in NGXS/localStorage. Avoid persisting sensitive booking data; reset all per-user state. NGXS DevTools only in development with sensitive-state safeguards. Explain actions/selectors/RxJS versus Angular signals; signals do not replace the required NGXS exercise.

## Human functionality
| Area | Required actions |
|---|---|
| Auth | Login/logout/current user; session-expired behavior |
| Catalog | Tour list/detail, sessions/prices/availability, ready images |
| Admin catalog | Tour create/edit/activate; session create; price/capacity with version checks; image upload/delete |
| Booking | Create with participants/idempotency; authoritative list/detail/timeline; cancel |
| Read dashboard | Cosmos-backed paginated My bookings; stale/unavailable labels; link to authoritative details |
| Payments | Owned operation list/detail; fake state labels |
| Notifications | Owned notifications list/detail, simulated delivery status |
| Admin workflow | Saga diagnostics; retry compensation; payment reconcile; fake-provider resolve; supported fault controls/reset |
| Admin broker | Bounded DLQ inspect and audited replay |
| Storage recovery | Authorized projection rebuild/image recovery controls where specified; no arbitrary object paths |
| Receipt | Receipt status and authorized simulated confirmation download |

Every original human action must appear in the parity matrix with a UI route/control or an explicit approved exclusion. Admin SQL/broker operations cannot be replaced with browser-direct database or Service Bus access. All browser data goes through APIs; storage downloads may be proxied.

## API contracts
Preserve original /api/{service}/v1 resource shapes when feasible. Use OpenAPI and typed DTOs. ProblemDetails includes safe code and traceId; 400 validation, 401 unauthenticated, 403 role, 404 ownership/not found, 409 state/version/idempotency conflict, 503 dependency unavailable, 202 durable accepted work. No raw exception/SQL leakage. Define pagination and loading/empty/error/retry UI. Authoritative cancellation controls must tolerate projection lag and backend409.

## Angular19 comparison only
Provide docs/angular19-to21.md explaining verified differences in toolchain, standalone defaults, signals/RxJS/NGXS and testing relevant to the implemented code. Do not maintain two Angular versions. Do not invent employer-specific standards.
