# Independent Azure companion task review

Act as an independent reviewer of the active AZ task implemented by Codex. Task ID is supplied with this prompt; if omitted, inspect the new repository's current-task/implementation plan and identify the implementation awaiting review. Review ONE coherent task, not the entire roadmap by default.

Read AGENTS.md, task scope/specifications, parity matrix, Codex evidence, relevant original business contracts (read-only), the diff and actual code. Do not assume generated evidence or previous lab status proves correctness. Do not fix code. You may save a review report in the NEW repo's review directory.

## Boundaries
The original AWS/Blazor project is immutable. Compare recorded before/after evidence and flag changes attributable to this task without overwriting pre-existing user changes. Do not provision/deploy Azure, publish, push, mutate cloud data, delete shared volumes, purge queues or access unrelated resources. Use isolated local resources. No tool/security bypass to obtain passing browser tests.

## Validate within the task's scope
1. Source/target separation, independent ports/secrets/resource names; no copied keys/.git/state/live data.
2. .NET10/EF SqlServer and Angular21/NGXS compatible pinned toolchain; genuine implementation, no placeholders.
3. SQL Server schema permissions and fresh migrations; provider-specific SQL, atomic claims/inventory, rowversion/businessVersion differences, transaction retry and idempotency. Actual restricted-principal tests.
4. Service Bus routing/filters, command versus event paths, manual PeekLock settlement and Outbox/Inbox crash windows. DLQ/replay preserves logical identity and accounts for broker duplicate detection. Emulator reset limitations explicitly separated.
5. Saga/fake provider/notification parity: timeouts unknown, compensation flags, late results, stable IDs, duplicate effects, concurrency and restart recovery. No distributed transaction claims.
6. Angular UI covers task actions, real typed clients/NGXS state, loading/errors/stale requests, logout reset, BFF cookie/antiforgery and direct backend JWT/ownership/Admin checks. Guards alone are insufficient.
7. Blob metadata/storage crash recovery and upload/delete races; file validation/private access/object ownership; browser can actually display/download.
8. Cosmos partition queries/indexes/cursors, owner isolation, conditional ETag/version comparison under races. No unconditional stale overwrite, SQL Inbox-before-Cosmos loss, SQL fallback masked as success, or rebuild resetting dedup.
9. Real Functions trigger/host path and storage config; duplicate receipt/Blob-before-SQL recovery; no cross-schema access, simulated document clearly labeled.
10. Cloud identity/Key Vault/IaC/cost correctness where relevant; no emulator auth/RBAC proof. Paid App Service plan bills when stopped; Service Bus Standard for topics; Functions runtime/plan support and free-offer assumptions verified.
11. Meaningful unit/integration/E2E tests actually execute. CI provisions the expected dependencies, propagates failures and protects secrets/auth artifacts. Browser sandbox unavailable => NOT VERIFIED, not PASS based on markup.

Inspect prerequisites and regressions selectively. Do not reject an early task because future roadmap work is not implemented; identify which acceptance criteria are due now. Do not flag intentionally excluded paid production infrastructure as a defect merely because it is absent.

Run targeted local checks and inspect actual output. Record exact commands, test counts, workflow/run URLs if already available, commit and scope. No pushing solely to manufacture evidence. Classify local, emulator, actual host, actual browser and Azure evidence separately.

## Report
- Verdict PASS / FAIL / INCOMPLETE VERIFICATION.
- Scope and acceptance-to-code/evidence table.
- Findings by severity with concrete path/line, impact, reproducible evidence and suggested fix.
- Actual checks/results and source immutability evidence.
- Unverified checks, environmental blockers and cloud-only deferred items.
- Prioritized Codex fix list and next-task readiness.

A test file is not an executed test. terraform/bicep validation is not deployment proof. Do not mark future tasks or cloud behavior complete. Preserve the implementation evidence and add independent review evidence instead of overwriting it.
