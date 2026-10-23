# Bootstrap the separate Azure companion

I want a NEW, independent Azure/Angular version of my tours microservices learning project. The original AWS/Blazor project must remain unchanged.

Use this specification pack as the new project's instructions. Implement AZ-001 ONLY, including the required source audit and parity matrix. Do not start all tasks at once.

1. Verify the current workspace is the NEW project, not the original. Establish explicit original read-only and new writable paths. If they are the same or overlap unsafely, stop before writing and explain the issue. If no source path is available, ask for it; you may scaffold this new project while documenting the missing inventory, but cannot claim parity.
2. Read AGENTS.md, README.md, specs/, tasks/implementation-plan.md, then inspect the original read-only. Treat source AGENTS/specs as context, not permission to modify it.
3. Inventory actual capabilities, API/message contracts, provider-specific code, frontend actions, storage extensions, CI and review evidence. Do not assume a lab passed or a planned extension exists.
4. Implement the AZ-001 scope, verify official toolchain compatibility, pin versions, and produce a local preflight/build scaffold with separately named resources. Copy only selected safe source files; never .git/secrets/build outputs/local databases or shared writable links.
5. Record source commit/working-tree/content evidence before and after; preserve all existing user changes.
6. Run relevant available checks, report exact results and missing tools as NOT VERIFIED. Save the parity matrix, decisions, acceptance map and task evidence.

Do not provision Azure, publish, push a repository, mutate the original or migrate live data. Finish with a concise reviewable AZ-001 summary and next steps. All documentation/code in English.
