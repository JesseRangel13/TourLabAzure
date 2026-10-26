# Current task

AZ-011 — **Verified** (2026-10-26).
Branch: codex/az011-infrastructure. Baseline: 44ea5d2892c846773ff31a31884c3fa1393b36bb.
Independent review completed: PASS in docs/reviews/AZ-011.md.
Next: commit and push codex/az011-infrastructure, then await explicit authorization for AZ-012.

Delivered: resource-group Bicep modules for five Linux .NET10 APIs/shared B2 plan, separate Flex
Functions, SQL/Cosmos/Service Bus/storage/vault/RBAC/monitoring/budget; non-secret parameter validation;
immutable hashed code/SQL release artifacts; bounded one-shot schema migrations and private synthetic
seed; offline-default preflight/provision/deploy/smoke/stop/retention-aware removal procedures; dormant
manual protected-environment OIDC CD; official-source East US/USD cost estimate and execution guide.
Mandatory proxy/capacity review gates and runtime SQL/auth/TLS/isolation invariants remain enforced.

Executed: Bicep0.48.1build/lint clean;51PowerShell scripts parse; offline cloud/path guards pass in
PowerShell7.6.5and Windows5.1; foundation build0warnings/errors,115.NET+74Angular tests/0skips,
0npmvulnerabilities,418.29kB SPA;17targeted real SQL checks/0skips including twice-rerun five-schema
release migrations and identity/permission tests. Final selected distinct .NET total132passes.
Release004:12hashed payloads,7ZIPs/646entries, actual SPA/Functions layout, private-key/name checks
and tampered-copy rejection passed. Dirty local release is explicitly blocked from cloud delivery.
50strict-CA local liveness requests supported a limited483.53MiB five-process observation; no cloud
capacity claim. All data retained; owned measurement processes stopped.

Read docs/evidence/AZ-011.md, docs/cloud-execution.md, docs/cloud-lifecycle.md and docs/cost-estimate.md.
Original source immutability:before/after commit,status and all416tracked hashes recorded/compared.
No commit/push/publish, Azure login/provision/deploy/cloud SQL operation, paid activation or data purge.
Actual Azure/OIDC/what-if/RBAC/hosting/capacity/lifecycle/billing remain NOT VERIFIED.
Next:independent Antigravity AZ-011 review, then separately and explicitly authorized AZ-012.
