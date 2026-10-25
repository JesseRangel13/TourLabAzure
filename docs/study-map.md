# Study map: follow one booking through the code

Use short sessions and explain each invariant before running its test. A task status records delivery;
it does not establish mastery or real Azure deployment. All provider effects in this lab are fictional
and durable. Run only the Azure companion. See testing-execution.md for exact execution commands.

| Session | Start in this repository | Explain and demonstrate | Evidence/practice |
|---|---|---|---|
| 1: boundaries and HTTP | AGENTS.md, main.ts, Gateway Program, auth/ | Same-origin Secure HttpOnly cookie, CSRF, backend JWT versus workload identity; why route guards are insufficient | auth/browser navigation; IdentityHttpTests; change user and prove no previous-owner state |
| 2: Angular shared state | catalog.state.ts, reservations.state.ts, auth/session-boundary.spec.ts | Typed actions, observable completion, selector signals, immutable updates, stale response generation and cancellation | Unit tests with delayed/failed HTTP; compare docs/angular19-to21.md |
| 3: accepted price and seats | Catalog Application/Persistence, Reservations booking handlers | Quote outside transaction; accepted price snapshot; atomic final seat; stable HTTP idempotency; SQL rowversion | CatalogTests, BookingSqlTests; explain one-winner assertions and denied cross-schema access |
| 4: asynchronous success | MessageRoutes, SqlOutboxLeases, OutboxDispatcher, InboxWorker, domain consumers | One local transaction; publish outside SQL; commit before Complete; duplicates/reordering; commands versus filtered topic events | BrokerIntegrationTests; stop after send and explain why duplicate delivery is safe |
| 5: uncertain effects | Payments provider/reconciliation, Reservations Saga, Admin pages | Unknown differs from decline; independent provider commit; same operation ID; refund/release flags; late success cannot resurrect | RecoverySqlTests/LiveRecoveryTests; explain ManualReview and provider-only Admin resolution |
| 6: private Blob images | Catalog Images, images.state.ts | Intent/lease before I/O, bounded decode, conditional writes, tombstone after deletion and restart recovery | ImageIntegrationTests; browser upload/display/remove; no browser storage account key |
| 7: Cosmos read model | Reservations Dashboard, dashboard.state.ts | User partition, protected pagination, ETag newer-version guard, eventual consistency and rebuild | CosmosIntegrationTests/BrokerDashboardTests; explain why dashboard cannot authorize cancellation |
| 8: actual Functions | Notifications Receipts, Receipts.Functions, receipts.state.ts | Notifications-owned job/snapshot, manual settlement, SQL lease and deterministic Blob recovery | ReceiptHostTests three stages; browser historical download before/after cancellation and owner404 |
| 9: workload security | Shared.Infrastructure/Security, config/cloud/permissions.json | System MI without developer fallback; pinned vault versions and SPKI; key-ring storage versus wrapping; explicit SQL principal IDs | CloudSecurityTests/ProtectionSqlTests; separate local proof from cloud-security-checklist.md |
| 10: delivery evidence | scripts/Verify-Integration.ps1, Verify-FullStack.ps1, CI workflow | Unit versus actual SQL/emulators versus browser versus cloud; unique profiles; raw evidence privacy; fail on skips | Read AZ-010 evidence, inspect count-only report and identify every unexecuted category |

Paths above are under src/ or web/tours-angular/src/ as appropriate. Use `rg --files` to find the
named concrete classes; do not read or run original AWS resources to demonstrate this companion.
Existing topic/queue traffic is retained. Broker tests temporarily lock and abandon unrelated messages;
they assert only their unique logical probes and never clear queues to make an assertion pass.

For each session write five sentences: trigger, owner, durable state transition, crash window and
recovery assertion. Then change one injected failure in the existing deterministic test and predict the
result before running it. Restore the intended assertion; do not turn a failing invariant into success.

Interview prompts:

1. Why can one confirmed booking have repeated transport deliveries but only one logical fake charge?
2. What proves atomic seat contention on SQL Server rather than merely testing a C# branch?
3. Which operation is safe to retry after timeout, with which unchanged identifier, and which is not?
4. What remains after Blob succeeds and SQL completion fails? Who owns recovery and authorization?
5. Why does a projection rebuild need a business version and conditional ETag write?
6. Why does a Key Vault secret version not automatically revoke an already cached signing key?
7. Which suite proves real Functions invocation, and which cloud permission still has no evidence?
8. Why does stopping App Service not imply a zero bill? AZ-011 must produce an actual sourced estimate.

See interview-demo.md for a bounded demo and recovery-learning.md/security-learning.md for deeper
worked examples. CI is the concrete implementation; Jenkins stages and Azure Pipelines jobs map to
checkout, locked build, test, package and gated delivery conceptually. No second pipeline or automatic
cloud deployment is introduced without actual assignment requirements and explicit authorization.
