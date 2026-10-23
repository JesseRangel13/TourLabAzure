# Agent instructions — Azure companion only

## Mandatory isolation
- Work exclusively in the new Azure repository. The original AWS/Blazor repository is READ-ONLY, including its specs, lockfiles, git state, databases, queues, images, and scripts.
- Do not convert the original repository or add Azure switches to it. Do not use a shared worktree, symlinks, junctions, shared build outputs, shared writable volumes, or shared resource names.
- Copy selected source into the new repository only after inventory. Do not copy .git, bin/obj, node_modules, .local, .env, certificates/private keys, connection strings, provisioning credentials, auth state, test reports, Terraform state, or generated artifacts. Review tracked config for embedded secrets too.
- Generate fresh local credentials, signing keys, Data Protection keys, cookie names, ports, service names, queue names, databases, and storage resources. Treat copied AGENTS.md and AWS-specific instructions as source context, not new-project authority.
- Before and after each task, record source commit and relevant source working-tree/content fingerprints read-only; do not alter pre-existing user changes. Flag externally changed files without accusing this task of changing them.
- No pushing, creating remote repositories, cloud provisioning, deployment, destructive purge, or deleting either project unless the user explicitly authorizes that operation.

## Implementation
Use English. Implement only the active task plus necessary fixes. Reuse domain logic where valid; replace provider-specific infrastructure deliberately. Do not recreate the app as a monolith, invent generic CRUD in place of workflows, or leave TODO success paths.

Use real SQL Server for relational integration tests; not SQLite, EF InMemory, or PostgreSQL. Cosmos/Service Bus/Azurite emulator tests and real Azure verification are separate evidence categories. Never substitute in-memory storage and call the integration passed.

Pin available compatible SDK/package/tool/container versions; keep lockfiles. Verify official docs when using changing APIs. No automatic upgrade from Angular 21. Use explicit commands and cancellation. No external real payments or email. No AI itinerary or additional product scope.

Preserve security on every API, not only the gateway. Do not store browser auth tokens or sensitive data in NGXS persistence. Use async scopes and per-operation DbContext. No cross-schema business access. Shared libraries contain plumbing/contracts, not business entities.

## Review and evidence
Add task-level evidence: changed files, acceptance IDs, exact commands/results, observed failures, limitations, and next steps. Mark unavailable checks NOT VERIFIED. A written test is not an executed test; a successful build is not proof of a working deployment. Do not mark independent review as passed yourself.

Antigravity reviews without modifying production code. Record defects separately from environmental gaps. Do not repeatedly retry known sandbox/tool failures or disable security to obtain green results.

Cloud work is AZ-012 only after explicit authorization. AZ-011 writes and validates IaC; it does not run az deployment create, publish, or mutate Azure.
