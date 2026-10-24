# Current task

AZ-006 — **Verified** (2026-10-24).
Branch: codex/az006-images.
Baseline: dda226bed182e792a093e09ff031b607bc6ff8e4.
Prerequisite: actual independent Antigravity AZ-005 PASS in docs/reviews/AZ-005.md; independent Antigravity AZ-006 PASS in docs/reviews/AZ-006.md.

Delivered: private Blob tour images with preserved2MiB/three-image/2048x2048 validation;
service-owned SQL intents/metadata/history and guarded leases; immutable conditional upload,
success-before-SQL recovery, delete races/tombstones, bounded scoped recovery and authorized
no-store downloads. Angular/NGXS Admin upload/remove/reload/recover and user placeholders,
state reset/stale-response/mutation guards. No storage credentials in browser.

Executed: Windows PowerShell5.1 foundation31 .NET tests,56Angular tests,0buildwarnings/errors,
0npm vulnerabilities,403.99kB SPA. Real SQL/Azurite suite104passed/13separate live skips.
Six actual HTTPS/staging/restart assertions passed,0skips, including restarted hosted image recovery.
Catalog model snapshot/actionlint/NuGet audit pass; eight browser tests discovered.
Source HEAD/status/all416tracked hashes unchanged, including pre-existing dirty source CI.
Private SQL profiles, test objects and existing broker/Azurite data retained; helper API trees stopped.

NOT VERIFIED: Chromium (known CA gap), application Docker SQL/images, remote CI, actual Azure
Blob/MI/RBAC, Cosmos/Functions receipts and emulator restart durability. No cloud deployment,
source mutation, data purge, commit/push or independent AZ-006 review claim.

Read docs/evidence/AZ-006.md, docs/images-execution.md, docs/images-learning.md,
docs/parity-matrix.md and docs/decisions.md(D40..43).
Next: independent Antigravity AZ-006 review, resolve findings, then AZ-007 Cosmos dashboard.
