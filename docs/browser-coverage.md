# Browser acceptance coverage

The Playwright suite targets real Angular production assets, Gateway HTTPS, five actual APIs,
durable workers, real SQL/emulators and the actual Functions listener. The table maps assertions;
executed results and any blocked category are recorded separately in evidence/AZ-010.md. Test
discovery is not an execution pass. Authentication traces, screenshots and saved auth state are off.

| File | Browser assertions | Acceptance |
|---|---|---|
| auth.spec.ts | Actual sign-in/current account, refresh and sign-out, protected deep link after logout | AC-A04/20/21/33 |
| booking.spec.ts | Admin creates synthetic availability; Alice books through workers, confirms, sees simulated notification, refreshes, cancels and reads one simulated refund; Bob cannot open it | AC-A06/09/10/13/19/33 |
| catalog.spec.ts | Admin creates tour/session, changes price/capacity and activation; tourist sees availability and no Admin controls; refresh/logout navigation | AC-A07/19/21/33 |
| dashboard.spec.ts | Real Cosmos eventual booking appears, point-read deep link/refresh, authoritative action link and Bob isolation | AC-A06/24/33 |
| images.spec.ts | Valid private PNG upload, actual decoded image width, refresh and removal through Angular | AC-A22/33 |
| navigation.spec.ts | Simultaneous Alice/Bob contexts, Secure/HttpOnly/Strict cookie, empty browser persistence, back/refresh, same-context account change, real401 reset and missing-CSRF denial | AC-A04/06/20/21/33 |
| receipts.spec.ts | Actual Function makes downloadable simulated confirmation; expected accepted amount; identical bytes after cancellation; Bob content404; trace ID required in Functions log | AC-A06/27/29/33/34 |
| recovery.spec.ts | Bounded Admin simulation form, save/reset, diagnostics, bounded DLQ peek, Saga filter and tourist Admin-route denial | AC-A19/33 |
| scaffold.spec.ts | Actual DOM/liveness, client deep link and genuine404 for unknown API/auth/health/static paths | AC-A02/21/33 |

There are12 tests in9 files. The suite fails on exclusive tests, a failed assertion or skipped coverage;
there are no automatic retries. Tests use unique GUID data and web-first assertions/bounded polling.
Credential pacing is separate from workflow polling and respects the unchanged security rate limit.

The suite exercises the named acceptance flows; it does not claim exhaustive UI permutations of every
fault/crash ordering. Atomic final-seat contention, HTTP-idempotency races, provider independent commit,
late events, compensation, DLQ replay leases/settlement, Blob recovery, ETag ordering and Functions
crash/restart are established by the corresponding real SQL/broker/storage/host tests. NGXS unit tests
cover controlled stale/cancelled HTTP responses, loading/error reset and cross-feature user boundaries.
Neither jsdom nor a manually constructed API response is presented as Chromium evidence.

All baseline human controls remain mapped in parity-matrix.md. A browser test does not replace
independent backend role/ownership/CSRF checks, and emulator success does not establish Azure RBAC,
cloud quotas, public network isolation, production rotation or hosted CI execution.
