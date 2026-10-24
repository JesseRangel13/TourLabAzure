# Current task

AZ-003 — **Verified** (2026-10-23).
Branch: codex/az003-catalog. Baseline: f4a49fa050285f0909d0fedf7e9a7ad426547b71.
Antigravity independent PASS is recorded in docs/reviews/AZ-003.md.

Delivered: Catalog tour search/detail/create/edit/activation, future sessions, versioned price/capacity,
service-only five-minute quote snapshots, atomic SQL inventory, hold/confirm/release/tombstone,
scoped expiry workers, idempotent Azure-only synthetic seeds and durable single-topic Outbox intents.
Angular typed clients, shared NGXS selectors/actions, stale/cancelled-load/account reset handling,
tour search/pagination/detail and reactive Admin controls are implemented.
Authenticated Development-only OpenAPI describes the typed contract.

Executed: Windows PowerShell 5.1 foundation (0 build warnings/errors, 18 .NET tests,
22 Angular tests, 0 npm vulnerabilities, production build, Compose validation);
real SQL/security/Catalog 52 passed / 3 intentional separate live skips;
live HTTPS Catalog/CSRF/roles, bearer transformation and Gateway restart/TLS tests passed.
Migration/model consistency, PowerShell parsing, pinned actionlint and NuGet audit passed.

NOT VERIFIED: Chromium UI (known CA-trust gap; Catalog Playwright smoke is implemented and discovered),
Docker SQL/container image execution, actual Azure SQL/managed identity or cloud deployment.
The existing default .local/sql file collision is preserved; use the fresh profile printed by Verify-Catalog.
No source mutation, existing-data deletion, remote push or cloud operation.

Read docs/evidence/AZ-003.md, docs/catalog-execution.md and docs/catalog-learning.md.
Next: independent Antigravity AZ-003 review, then AZ-004 Service Bus and reservation success slice.