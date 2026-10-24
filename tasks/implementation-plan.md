# Azure companion implementation plan

Status as of 2026-10-24: AZ-001 Verified (independent review: docs/reviews/AZ-001.md); AZ-002 Verified (independent review: docs/reviews/AZ-002.md); AZ-003 Verified (independent review: docs/reviews/AZ-003.md); AZ-004 Verified (independent review: docs/reviews/AZ-004.md); AZ-005 Verified (independent review: docs/reviews/AZ-005.md); AZ-006 through AZ-012 Not Started. See docs/evidence/AZ-005.md for actual recovery/SQL/broker/UI evidence. See docs/evidence/AZ-004.md for actual SQL and broker booking evidence. See docs/evidence/AZ-003.md for Catalog checks and docs/evidence/AZ-002.md for prerequisite evidence and environment gaps. Work on ONE task per implementation turn; each has an independent Antigravity review gate. Task completion is not mastery or proof of Azure deployment.

| ID | Scope | Prerequisites | Exit evidence |
|---|---|---|---|
| AZ-001 | Read-only source audit, parity matrix, isolated new repo, toolchain/process scaffold and local dependency preflight | None | AC-A01/02; runnable build scaffold, resource map and blockers |
| AZ-002 | SQL Server provider port, schema/roles/migrations, Identity/BFF/CSRF and minimal Angular login | 001 | AC-A03..06; SQL integration and login smoke |
| AZ-003 | Catalog/quotes/inventory, Angular shell and NGXS foundation | 002 | AC-A07/08/20; tours/Admin visible, atomic inventory tests |
| AZ-004 | Service Bus command/topic routing, Outbox/Inbox, reservation success, durable fake charge/notification | 003 | AC-A09..12/17; working end-to-end vertical slice |
| AZ-005 | Full Saga faults/recovery, admin/resilience/observability and Angular baseline parity | 004 | AC-A13..21/34; parity matrix completed except designated extensions |
| AZ-006 | Blob tour images + Angular + recovery tests | 005 | AC-A22/23 |
| AZ-007 | Cosmos booking projection, rebuild and NGXS dashboard | 005 | AC-A24..26 |
| AZ-008 | Notifications-owned receipt Functions, Blob output and Angular download | 006 and 007 | AC-A27..29; actual local Functions trigger evidence |
| AZ-009 | Key Vault/managed identity wiring and secure cloud configuration preparation | 008 | AC-A30; local secret path and cloud prerequisite matrix |
| AZ-010 | Full Playwright suite, CI, Angular19/21 notes and study map | 009 | AC-A19..21/32..34; executed full-stack evidence |
| AZ-011 | App Service/Functions Bicep, migration/deployment/lifecycle scripts, cost estimate | 010 | AC-A31; no deployment; ready for independent review |
| AZ-012 | Explicitly authorized Azure provisioning/CD smoke, managed identity/RBAC verification and cost teardown exercise | 011 + user authorizes cloud actions | AC-A35/36 plus cloud gaps from AC-A30; actual evidence |

CI scaffolding and small Playwright smoke tests begin earlier (001/002); AZ-010 completes and hardens them, rather than postponing all tests until the end.

## Preparation before the assignment
Prioritize 001..005 first for a useful Angular + SQL Server + Service Bus system. Then 006..009 expose the remaining named Azure services. 010 reinforces automation; 011/012 address hosting. If time runs short, explicitly record partial scope instead of declaring parity. Do not automatically deploy to meet a calendar date.

## Each delivery
Update this plan/current task and evidence with actual state: Not Started, In Progress, Implemented Awaiting Review, Verified, Blocked. Include source immutability evidence, exact checks, acceptance IDs, remaining limitations and next task. Only an actual independent review can advance to Verified. Do not invent historic findings or mark unexecuted tests passed.
