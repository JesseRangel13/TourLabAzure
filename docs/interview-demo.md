# Interview demo: local, simulated and inspectable

Target duration:12–15minutes after dependencies are healthy. Present only the independent Azure
companion. All payments, delivery and confirmation documents are SIMULATED; this demo moves no real
money, sends no real email and makes no Azure deployment or RBAC claim. Prepare with
scripts/Verify-Foundation.ps1, Verify-Integration.ps1 and Verify-FullStack.ps1. Read the actual evidence
first; do not present an unexecuted test as passed.

Use the private run profile printed by Verify-FullStack. Its child processes have stopped safely after
the test; start five hosts with Start-Host.ps1 -ConfigurationDirectory <profile> and the Functions
listener with Start-Receipts.ps1 using that same profile, in dedicated terminals. Do not show private
JSON, passwords, cookies, DevTools auth headers, signed material or raw authentication logs on screen.
Use the generated lab users privately. Stop only these owned process trees afterwards; retain data.

| Time | Human demonstration | Explain the implementation/evidence |
|---|---|---|
| 0–2min | Show architecture and sign in as Admin over trusted HTTPS | Five API processes, Notifications-owned Functions, Angular same-origin BFF; every backend validates its credential |
| 2–4min | Create a unique synthetic tour and future session; update price/capacity | Version conflicts, integer MXN cents, accepted quote and atomic capacity; point to real SQL tests |
| 4–7min | Sign out, sign in as Alice, book two seats and refresh until Confirmed | 202 durable intent; Outbox/Inbox; fake provider receipt; one logical effect despite duplicate delivery |
| 7–9min | Download historical simulated confirmation; open the Cosmos dashboard then authoritative details | Actual Functions trigger and private Blob; eventual projection cannot authorize a cancellation |
| 9–11min | Open independent Bob context and try Alice's booking/document | Backend owner404, no shared cookie/state; tests use two real contexts and secure cookies |
| 11–13min | Cancel Alice's confirmed booking, refresh to Cancelled, show one simulated refund and the same historical document | Stable refund ID plus seat release; immutable confirmation remains historical rather than a current-status promise |
| 13–15min | Show Admin diagnostics, bounded fault mode controls and private count summary | No raw payload dump; bounded DLQ peek versus audited replay; no cloud claim from emulators |

For a follow-up recovery demo, use the existing real broker/SQL/HTTPS workflow tests rather than
changing business timeouts or editing Saga rows. Explain the deterministic Functions crash barrier
and original-queue/topic replay lineage. Never replay arbitrary retained DLQ entries just to stage a
presentation. A failed provider is resolved only through authorized fictional-provider controls with
the same payment operation ID, followed by reconciliation.

Show the CI file as a reviewable artifact. Describe PR foundation/image/package checks and the manual
full-stack job's TWO explicit license gates. Say whether the remote job actually ran; a local green
suite and actionlint cannot substitute for a hosted runner execution. Show evidence/AZ-010.md and
cloud-security-checklist.md at the end, including every NOT VERIFIED limitation.
