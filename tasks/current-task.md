# Current task

AZ-004 — **Verified** (2026-10-24).
Branch: codex/az004-booking.
Baseline: fb222a5fd21f47b3664d9df110a84c34f7894883.
Prerequisite: actual independent Antigravity AZ-003 PASS in docs/reviews/AZ-003.md; independent Antigravity AZ-004 PASS in docs/reviews/AZ-004.md.

Delivered: Service Bus SDK command queues and filtered single-topic events; leased SQL Outbox,
transactional Inbox/manual settlement; idempotent booking and success Saga; independent durable
simulated charge/reconciliation and notification receipts; basic Angular booking/detail/timeline,
paginated payment/notification pages and per-user NGXS state/reset/error/cancellation guards.

Executed: Windows PowerShell5.1 foundation (0 warnings/errors,18 .NET tests,32 Angular tests,
0 npm vulnerabilities, production build361.14kB, Compose validation); real SQL suite60 passed,
7 separate live skips; actual broker/HTTPS workflow7 passed (routing, real redelivery, complete booking,
owned404, poison built-in DLQ, BFF/CSRF/roles and Gateway restart/TLS).
Four migration/model comparisons, actionlint, NuGet audit and PowerShell5.1 parsing passed.
Source HEAD/status and all416 tracked content hashes unchanged, including pre-existing dirty CI file.

Docker Desktop started by user; both Microsoft licenses explicitly accepted.
Only Azure lab broker-sql/servicebus containers started; they remain running and data/DLQ probes retained.
Five verification HTTPS host process trees stopped; ports9443..9447 free.
No data deletion, source mutation, commit/push or cloud operation.

NOT VERIFIED: Chromium UI (known CA trust gap; six smoke tests discovered), Docker application SQL/
application images, remote CI, actual Azure/RBAC/managed identity and emulator restart durability.

Read docs/evidence/AZ-004.md, docs/booking-execution.md, docs/booking-learning.md,
docs/servicebus-routing-v1.md and docs/parity-matrix.md.
Next: independent Antigravity AZ-004 review; resolve findings before AZ-005 full Saga/admin/API/UI parity.
