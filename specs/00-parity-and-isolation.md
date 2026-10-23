# 00 — Source inventory and parity

## Required inventory
Before porting, inspect the original repository and produce docs/source-inventory.md and docs/parity-matrix.md. Source path is a real user-supplied/observed path; do not guess or recursively inspect unrelated directories. If unavailable, scaffold the new repo from this pack, document the blocked inventory, and ask for the source path before claiming a faithful port.

Capture commit, dirty-state summary, specs/tasks/evidence paths, actual services, HTTP contracts, message contracts, SQL provider-specific code, UI actions, tests, fault controls, CI, S3 and DynamoDB implementations. Preserve pre-existing dirty source code as read-only context; never clean/reset it.

Parity matrix columns: original capability and file/contract, Azure task, new endpoint/UI/code, compatibility decision, acceptance ID, implemented status, locally verified status, Azure verified status, reviewer evidence. Enumerate every human action, not only happy-path CRUD. Explicitly classify original planned/absent/unverified features rather than claiming they existed.

## Baseline minimum
- Login/logout/current user; Alice/Bob Tourist and Admin; ownership and role enforcement; TLS and antiforgery.
- Tour/session CRUD, activation, prices/capacity, quote snapshots, atomic holds, expiry, confirmation/release.
- Reservation creation/idempotency, list/detail/timeline, cancellation; persistent orchestration and concurrency.
- Fake charge/refund operations with independent durable provider effects; reconciliation and admin resolution.
- Fake notifications, persisted receipts, retry and queries.
- Outbox/Inbox, duplicate/out-of-order processing, failure modes, circuit breaker/resilience, tracing, health, diagnostics, DLQ inspection/replay.
- Tour images, upload/removal/recovery; user booking read projection and rebuild.
- Full human UI coverage and meaningful automation; restart/multi-instance behavior.

Do not migrate original database contents or reuse its live storage. Seed new synthetic data with fake identities only. Contracts may retain original resource shapes where possible; document necessary Azure/SPA route differences.

## Isolation acceptance
A fresh checkout must run independently of the original. Compose project defaults to tourlab-azure with AZURELAB_-prefixed configuration. Allocate and document a checked port map, e.g. gateway 9443 and Angular development 4201, adjusting only after checking conflicts. Avoid fixed container_name. Use different volumes, networks, SQL database names, emulated entities, cookie names, project ID tags, and secret files. Both projects should be runnable concurrently where host RAM permits; document heavy emulator requirements honestly.
