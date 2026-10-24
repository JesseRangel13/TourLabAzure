# Current task

AZ-007 — **Verified** (2026-10-24).
Branch: codex/az007-dashboard.
Baseline: 5ddddd5928cd8848b0ff398910ec865b332c641e.
Prerequisites: actual independent Antigravity AZ-005/AZ-006 PASS in docs/reviews/; independent Antigravity AZ-007 PASS in docs/reviews/AZ-007.md.

Delivered: transactional versioned booking snapshots; dedicated filtered Service Bus dashboard
subscription; Cosmos SDK user partitions, immutable snapshot/ETag guards and bounded protected
pagination; audited keyset SQL rebuild using the same version-safe event path; Angular/NGXS dashboard,
point detail, eventual-consistency/outage states, authoritative links and Admin bounded rebuild UI.
No Cosmos-dependent booking transaction, cross-schema business read or SQL fallback.

Executed: Windows PowerShell5.1 foundation35 .NET tests/0skips,66Angular tests,0buildwarnings/errors,
0npm vulnerabilities,412.88kB SPA. Real SQL/Cosmos regression110passed/24separate category skips.
Two actual broker/Cosmos checks plus five HTTPS/restart checks passed with0skips.
Focused SDK outage check additionally proves booking/charge/cancel/refund/release.
Reservations model snapshot/actionlint/NuGet audit pass; nine browser tests discovered.
Source HEAD/status/all416tracked hashes unchanged, including pre-existing source CI modification.

Retained: all original resources, existing Azure broker/Azurite, dedicated Cosmos volume, SQL profiles,
document/DLQ evidence and private encrypted cursor keys. Only helper-owned host trees stopped.
NOT VERIFIED: Chromium (known CA gap), real Cosmos composite-index/auth/RBAC/managed-identity/RU
enforcement, emulator restart durability, application Docker SQL/images, remote CI and receipt Functions.
No source mutation, data purge, cloud deployment, commit/push or independent AZ-007 review claim.

Read docs/evidence/AZ-007.md, docs/dashboard-execution.md, docs/dashboard-learning.md,
docs/parity-matrix.md and docs/decisions.md(D44..49).
Next: independent Antigravity AZ-007 review, resolve findings, then AZ-008 receipt Functions.
