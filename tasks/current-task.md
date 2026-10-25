# Current task

AZ-008 — **Verified** (2026-10-24).
Branch: codex/az008-receipts.
Baseline: 9c8fcc9ff77a7bfeedbdd3841e78dd0b2f9fc799.
Prerequisites: actual independent Antigravity AZ-006/AZ-007 PASS in docs/reviews/; independent Antigravity AZ-008 PASS in docs/reviews/AZ-008.md.

Delivered: immutable Notifications-owned confirmation snapshot/job/Outbox acceptance; actual .NET10
isolated Service Bus-triggered Function; owned SQL lease/version and private conditional Blob content
verification; durable crash/duplicate/conflict recovery and manual settlement; owner/Admin status and
attachment via Gateway; Angular/NGXS historical confirmation panel, current authoritative status,
loading/errors/retry/download and session reset. Normal simulated notifications remain independent.
Legacy version1 confirmation delivery is retained and audited without inventing receipt snapshots.

Executed: Windows PowerShell5.1 foundation39 .NET tests/0skips,72Angular tests/13files,0build
warnings/errors,0npm vulnerabilities,418.29kB SPA. Real SQL/Azurite regression115passed/34separate
category skips;3actual Functions process-crash/restart/DLQ stages and4HTTPS tests passed with0skips.
Final affected host/HTTPS run additionally asserts the real booking trace in the Function log.
Notifications model consistency, NuGet audit, actionlint and diff whitespace checks pass.
Ten browser tests discovered; Chromium remains NOT VERIFIED.
Source HEAD/status/all416tracked hashes unchanged, including pre-existing source CI modification.

Retained: all original resources and existing Azure broker/Azurite/Cosmos data, fresh SQL profiles,
private receipt/host storage, immutable objects and built-in DLQ evidence. Only helper-owned process
trees stopped. No source mutation, purge, cloud deployment, commit/push or self-assigned review PASS.

NOT VERIFIED: Chromium, real cloud Functions/SQL/Blob/Service Bus, managed identity/RBAC/Key Vault,
cloud scale/quota/TLS behavior, emulator restart durability, application Docker SQL/images and remote CI.

Read docs/evidence/AZ-008.md, docs/receipts-execution.md, docs/receipts-learning.md,
docs/parity-matrix.md and docs/decisions.md(D50..55).
Next: independent Antigravity AZ-008 review, resolve findings, then AZ-009 readiness.