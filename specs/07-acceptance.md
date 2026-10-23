# 07 — Acceptance and evidence

Every row requires code paths and executed evidence, or an explicit NOT VERIFIED status. Original baseline parity remains required even when not individually repeated below.

| ID | Required behavior / evidence |
|---|---|
| AC-A01 | Original repo untouched; source/target paths, commit and before/after evidence; independent names/ports/resources |
| AC-A02 | .NET10 build; Angular21/compatible NGXS/Node lockfiles; five APIs + isolated Functions scaffold |
| AC-A03 | SQL Server bootstrap/migrations/seed repeat; runtime roles cannot read/write other schemas or run DDL |
| AC-A04 | Cookie login/logout/me/CSRF; missing token denied; durable encrypted key persistence; restart login behavior |
| AC-A05 | Direct backend rejects invalid signature/issuer/audience/expired JWT; internal quote route service-only |
| AC-A06 | Alice/Bob/Admin isolation; forged owner IDs denied;404 for other user's resources; backend Admin checks |
| AC-A07 | Catalog CRUD/session price/capacity/version checks and Angular controls |
| AC-A08 | Concurrent last-seat contention gives one winner; expiry/confirm/release/tombstone restore capacity once |
| AC-A09 | Concurrent same idempotency key => one reservation; different body=>409; no hidden Saga on quote503 |
| AC-A10 | Hold -> fake charge -> confirm -> simulated notification works through real local broker and SQL |
| AC-A11 | Outbox commit-before-send; send-before-mark; Inbox commit-before-Complete crash tests |
| AC-A12 | Provider independent charge commit then app crash => reconciliation, one charge |
| AC-A13 | Decline, confirmation rejection, refund/release ordering and repeated cancellation remain correct |
| AC-A14 | Timeout-after-charge, unknown/ManualReview/admin resolve, late charge/hold events recover safely |
| AC-A15 | Two worker instances race same Saga/lease and business IDs without double effects |
| AC-A16 | Bounded resilience/breaker, dependency failure, user cancellation and graceful shutdown |
| AC-A17 | Service Bus routing filters deliver intended events once logically to each consumer; no competing command consumers |
| AC-A18 | Malformed message -> built-in DLQ; audited replay preserves logical IDs and handles broker dedup window |
| AC-A19 | All original human API actions have Angular equivalents or explicit approved exceptions |
| AC-A20 | NGXS selectors/actions/loading/errors/cancelled or stale loads; user state reset on logout401/account change |
| AC-A21 | Concurrent Alice/Bob browsers never share tokens/state; refresh/deep link/back navigation correct |
| AC-A22 | Blob bounded valid upload/display/remove; unsupported/oversize rejected; backend ownership/Admin enforced |
| AC-A23 | Blob success+SQL failure, restart reconciliation and late upload vs deletion cannot leak ready images/resurrect |
| AC-A24 | Cosmos user-partition list/point read/pagination/indexes; no SQL fallback or normal full scan |
| AC-A25 | Duplicate/out-of-order/concurrent projection events use ETag version guard; no lost update |
| AC-A26 | Cosmos success-before-ACK crash, outage isolation and rebuild racing newer event |
| AC-A27 | Service Bus-triggered receipt via real Functions host -> private Blob -> authorized UI download |
| AC-A28 | Duplicate receipt/Blob success+SQL failure/Function restart yields one immutable logical receipt |
| AC-A29 | Function ownership/404, clear simulated document, notification flow independent of receipt failure |
| AC-A30 | Managed-identity/RBAC/Key Vault design verified structurally; local vs actual cloud proof separated |
| AC-A31 | Bicep validation, secure non-secret config, measured hosting settings and sourced cost estimate |
| AC-A32 | CI runs actual .NET/Angular tests and image/package builds with isolated SQL/broker/storage/Cosmos |
| AC-A33 | Playwright success/cancel/Admin/ownership/image/dashboard/receipt/NGXS navigation flows execute |
| AC-A34 | JSON logs correlate HTTP/messages/Functions, no secrets, bounded diagnostic metrics and health |
| AC-A35 | Explicit cloud provision/deploy/migration/smoke/rollback proof; source AWS untouched (AZ-012 only) |
| AC-A36 | Lifecycle cost behavior and retained data verified in Azure; stop does not falsely claim zero billing |

## Test levels
- Unit: transitions, input rules, money, cursors, version comparison, NGXS reducers/selectors/error paths.
- Relational integration: real SQL Server, restricted principals, concurrent writes, provider-specific SQL, durability.
- Broker/storage/document integration: real supported local emulators + actual SDKs, not mock-only evidence.
- Host integration: Functions Core Tools + broker/Azurite; invoke the trigger path.
- E2E: Playwright Test TypeScript on real Angular+APIs+workers, synthetic isolated users/data.
- Cloud: separate opt-in suite validates managed identity, TLS, Key Vault, Service Bus cloud behavior, quotas, App Service/Functions health and costs.

Use deterministic barriers/fault hooks for crash tests, not production unauthenticated crash endpoints. Don't assert network exactly-once delivery. Test business effects and recovery.

## Playwright practice
Use role/label locators or deliberate test IDs, web-first assertions, unique per-test data and separate browser contexts. No fixed waitForTimeout to await eventual consistency; use bounded polling with diagnostic evidence. API fixtures may arrange data through authorized test-safe APIs; browser acceptance still exercises the actual UI. Avoid direct SQL/broker mutation to fake outcomes.

Store trace/screenshots on failure only where useful; short retention; auth state files ignored/private, never uploaded as artifacts. Pin Playwright package/browser image together. Chromium required initially, Firefox/WebKit optional extra coverage. Distinguish small mocked UI tests from full-stack smoke. Sandbox unavailable => NOT VERIFIED, not bypass security or pretend SSR checks validate Angular behavior.
