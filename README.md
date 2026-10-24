# TourLab Azure + Angular companion

Specification version 1.0 — 2026-10-23. This is a NEW, independent educational project, not an in-place migration of the AWS/Blazor project. Target: practical preparation for an assignment starting after November 5, 2026. No promise that all cloud verification will fit before that date.

## Start here
1. Create a new empty sibling directory and open it in Codex. Keep the original project available through an explicit READ-ONLY source path.
2. Extract this pack into that new directory, preserving AGENTS.md, specs/, tasks/, prompts/, and docs/.
3. Run prompts/00-BOOTSTRAP-CODEX.md. Codex inventories the original's actual features and establishes isolation; it must not edit the original.
4. Implement ONE task at a time using its prompt under prompts/implement/.
5. After each task, give Antigravity prompts/REVIEW-ANTIGRAVITY.md with the task ID. Resolve blocking findings before progressing through dependent work.
6. Use prompts/TEACH-ANTIGRAVITY.md to study the resulting code in short lessons.

## Stack
.NET 10 ASP.NET Core Web APIs; EF Core 10 with SQL Server; Angular 21 + TypeScript + RxJS + NGXS; Azure Service Bus; Azure Blob Storage; Azure Cosmos DB for NoSQL; Azure Functions isolated worker; Azure Key Vault and managed identity; Playwright Test (TypeScript); GitHub Actions; Bicep for Azure IaC.

Angular 21 is intentional to match the assignment, even if newer majors exist. Angular 19 is a comparison topic, not a second frontend implementation. Preserve compatible pinned versions after checking official sources.

## Authority
AGENTS.md defines working boundaries. specs/ define intended behavior. tasks/ define incremental scope. Codex must inventory the CURRENT original repository, including S3/DynamoDB and CI extensions if present. The original specification archive informed this pack but is not evidence that current code or cloud deployment passed.

Priority for baseline behavior: explicit user instructions > these Azure specifications > actual approved original business specifications. Record contradictions; never silently weaken invariants or copy AWS deployment rules into Azure.

## Delivery order
AZ-001 isolation/scaffold; AZ-002 SQL Server/Identity; AZ-003 Catalog + Angular/NGXS shell; AZ-004 Service Bus + success slice; AZ-005 full Saga/API/UI parity; AZ-006 Blob images; AZ-007 Cosmos dashboard; AZ-008 receipt Functions; AZ-009 managed identity/Key Vault readiness; AZ-010 Playwright/CI; AZ-011 App Service/Bicep preparation; AZ-012 explicit deployment (separately authorized).

Read specs/07-acceptance.md and tasks/implementation-plan.md for scope and gates. Local functionality comes before cloud spending. All payments, notification deliveries, and booking receipts are explicitly simulated.

## Current implementation — AZ-002 (2026-10-23)

SQL Server/Identity/BFF and Angular login/logout/account are implemented, awaiting independent review.
Start with [SQL/Identity execution](docs/sql-identity-execution.md) and [actual AZ-002 evidence](docs/evidence/AZ-002.md).
.NET SDK10.0.402 / runtime10.0.13 is pinned; use the private toolchain helper if needed.
The executed relational profile is a separate SQL Server2025 LocalDB instance; Docker/Azure verification is distinct.
Catalog and booking workflows remain AZ-003 onward. The source AWS/Blazor repository is unchanged.
## AZ-001 implementation status (2026-10-23)

The independent scaffold is now present: five API processes, isolated Functions, Angular21/NGXS,
locked dependencies, local HTTPS/preflight/dependency artifacts and build-only CI.
Start with [local execution](docs/local-execution.md), [source inventory](docs/source-inventory.md),
[parity matrix](docs/parity-matrix.md), [resource map](docs/resource-map.md) and
[actual AZ-001 evidence](docs/evidence/AZ-001.md).
Business workflows remain scheduled in AZ-002 onward; process liveness is not workflow readiness.
Do not re-extract/recreate the repository. No Azure resources have been deployed.
