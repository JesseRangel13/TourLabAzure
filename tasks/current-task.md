# Current task

AZ-009 — **Verified** (2026-10-24).
Branch: codex/az009-security.
Baseline: b9640fb1a14cc69095b3c48afd8bf5f66e1631ee.
Prerequisite: actual independent Antigravity AZ-008 PASS in docs/reviews/AZ-008.md; independent Antigravity AZ-009 PASS in docs/reviews/AZ-009.md.

Delivered: explicit system managed-identity cloud paths and strict configuration guards for five APIs/
Functions; bounded versioned Key Vault signing secrets with public fingerprint matching; bounded JWT
public-key rollover; Gateway SQL key ring with Key Vault encryption, separate protected dashboard Blob
ring, local active/previous certificate support; exact proxy/origin/TLS guards; offline config validator,
explicit object-ID Azure SQL principal generator, limited receipt job/audit grants and immutable-column
denials; public templates, role map and real-cloud prerequisite checklist. No new user-facing secret UI.

Executed: Windows PowerShell5.1 foundation82 .NET tests/0skips,72Angular tests/13files,0warnings/errors,
0npm vulnerabilities,418.29kB SPA. Real SQL/Azurite/Cosmos regression174passed/20separate-category skips.
Actual Functions crash/restart/DLQ plus HTTPS booking/download/owner/cancel/cookie/TLS/trace checks:
7passed/0skips. Final policy/protection follow-up38passed and immutable-column SQL follow-up2passed.
Offline synthetic config/generator smoke passes; unresolved templates correctly rejected.
Identity/Reservations models have no pending changes; NuGet audit/actionlint/whitespace pass.
Ten browser tests discovered; Chromium remains NOT VERIFIED.
Original HEAD/status/all416tracked hashes unchanged, including pre-existing source CI modification.

Retained: all original resources, existing Azure containers/data, fresh SQL/protection profiles,
certificates/encrypted rings and diagnostic evidence. Only helper-owned process trees stopped.
No cloud authentication, provisioning, role assignment, deployment, purge, commit/push or review PASS.

NOT VERIFIED: actual Azure managed identity/RBAC/Key Vault/SQL/Functions/broker/storage/Cosmos,
App Service ingress topology, cloud rotation/scale/TLS/quota/lifecycle, Chromium, Docker SQL/images,
remote CI. Local SDK mocks and SQL impersonation are explicitly separate from cloud proof.

Read docs/evidence/AZ-009.md, docs/security-execution.md, docs/cloud-security-checklist.md,
docs/security-learning.md, config/cloud/permissions.json and decisions D56..63.
Next: independent Antigravity AZ-009 review, resolve findings, then AZ-010.