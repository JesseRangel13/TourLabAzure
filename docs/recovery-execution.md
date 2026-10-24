# Recovery and Admin execution — AZ-005

All commands run from the NEW TourLab Azure root. The original AWS/Blazor project is read-only.
All payment, refund and notification effects are fictional and durably persisted in owned SQL schemas.
No Azure namespace, deployment or paid plan is needed or authorized.

## Reproduce the local checks

~~~powershell
. ./scripts/Use-Toolchain.ps1
dotnet restore TourLab.Azure.slnx --locked-mode
dotnet msbuild src/Receipts.Functions/Receipts.Functions.csproj -t:GenerateFunctionsExtensionProject
dotnet build TourLab.Azure.slnx -c Release --no-restore
./scripts/Verify-Recovery.ps1
./scripts/Verify-RecoveryWorkflow.ps1
./scripts/Verify-Foundation.ps1
~~~

Verify-Recovery creates a fresh retained SQL Server 2025 LocalDB database/profile. It runs all prior
permission, Identity, Catalog and booking tests plus recovery/HTTP/resilience/tracing cases.
-Profile Docker uses the separately started application SQL container; that mode is NOT VERIFIED
for AZ-005. The broker's dedicated SQL dependency is not the application's SQL database.

Verify-RecoveryWorkflow requires the existing healthy broker at http://localhost:5301/health.
It never restarts, recreates or purges that broker. For first setup only, follow booking-execution.md
and explicitly accept both Microsoft licenses before Start-Broker.ps1. The user already accepted
them and started this lab's containers during AZ-004; AZ-005 reuses those containers.

The workflow verifier creates another fresh SQL profile, enables only the loopback Development
emulator, runs actual routing/replay/shutdown checks before starting consumers, then starts five
HTTPS hosts. It runs BFF checks, booking, recovery/timeout/readiness/trace cases and Gateway restart.
It waits one unchanged 60-second credential-throttle window before the additional login phase.
It stops only its own five host process trees; all SQL/profile data and retained DLQ probes stay.
Reports and raw process logs are ignored under .local and TestResults.

Windows PowerShell 5.1 is supported; use:

~~~powershell
& "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -NonInteractive -File scripts/Verify-Recovery.ps1
& "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -NonInteractive -File scripts/Verify-RecoveryWorkflow.ps1
~~~

## Explore the five services and UI
Use the fresh .local/recoverylive-<id> directory printed by the live verifier. In five separate shells:

~~~powershell
./scripts/Start-Host.ps1 -Service Catalog -ConfigurationDirectory .local/recoverylive-<id>
./scripts/Start-Host.ps1 -Service Reservations -ConfigurationDirectory .local/recoverylive-<id>
./scripts/Start-Host.ps1 -Service Payments -ConfigurationDirectory .local/recoverylive-<id>
./scripts/Start-Host.ps1 -Service Notifications -ConfigurationDirectory .local/recoverylive-<id>
./scripts/Start-Host.ps1 -Service Gateway -ConfigurationDirectory .local/recoverylive-<id>
~~~

First run Build-Web.ps1 after changing the SPA. Open https://localhost:9443 using the explicitly
trusted generated lab CA and private seeded credentials. Never disable certificate validation.

- My bookings: filter/page, open authoritative details/timeline, cancel a Confirmed booking before
  departure. A full simulated refund and seat release are asynchronous; refresh until Cancelled.
  A repeated cancellation, even with a new request key, reuses the original refund operation.
- Payments and Notifications: filter by reservation, page through owned records and open details.
  Payment detail shows the captured mode, reconciliation count and refund state. All effects are labeled simulated.
- Informational prices: a versioned, eventually consistent SQL projection, distinct from the later
  Cosmos dashboard. Use Catalog for authoritative current price and availability.
- Admin Sagas: filter/page, inspect pending compensation flags and stable operation IDs.
  Retry compensation only after an uncertain payment is resolved. Backend guards remain authoritative.
- Simulations: choose a service, bounded mode/occurrences or reset. Modes are captured for new work,
  not retroactively assigned to old operations. A fake UnknownUntilAdminResolution effect can be
  resolved to Succeeded or NotCharged; then explicitly reconcile the same payment operation.
- Operations: choose a service, page through redacted owned SQL metadata, worker observations and
  audit. Peek commands/events DLQ pages separately. No body or receipt/lock token is returned.

Fault modes: Catalog None/QuoteTimeout/RejectNextConfirmation/PauseConsumption;
Reservations None/PauseOutbox; Payments Success/Decline/TimeoutAfterCharge/
UnknownUntilAdminResolution/RefundTransientFailure/PauseConsumption; Notifications None/FailureThenSuccess.
Selection requires backend Admin and explicit Development plus LabFeaturesEnabled. Occurrences are 0..100.
Pauses consume bounded fault occurrences and retain durable work; no queue/Inbox purge occurs.

## DLQ safety and recovery
Peek is non-locking and bounded to ten metadata records. It records a deterministic inspection ID,
source, sequence and body hash in the owning schema. Re-peeking refreshes a five-minute inspection
only when no replay owns its lease.

Replay claims a 60-second SQL lease, then uses PeekLock to locate the inspected item. The search
holds at most 50 broker messages, abandoning unrelated locks. A deep item may return409; inspect/
review the bounded window rather than deleting other messages. Replay rejects malformed/unsupported/
misrouted or older-than-14-day envelopes. No arbitrary entity URL or body editing is accepted.

The send goes to the ORIGINAL command queue or topic. Each deliberate send gets a fresh transport
MessageId, while the logical envelope/operation IDs stay unchanged and lineage headers identify the
inspection/logical message. Broker duplicate detection cannot substitute for SQL/business deduplication.
Only broker acceptance permits completing the original DLQ message. A crash after send can leave the
original for retry; audit records the uncertain boundary. A repeat after Completed returns the stored
outcome. Topic replay can reach other matching subscriptions, which must deduplicate logically.

The original malformed probes are retained; successful replay settlement is part of the requested
domain operation, not test cleanup. No delete/purge/down -v/reset of existing data is part of these scripts.

Microsoft references checked for this implementation:
[DLQ semantics](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues),
[SDK receiver](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusreceiver),
[duplicate detection](https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection).
Local emulator TTL remains one hour, independently of retained SQL deduplication/tombstones (>=14 days).

## Evidence boundaries
See evidence/AZ-005.md for actual counts, commands, profiles, reports and observed failures.
The live verifier uses real LocalDB, AMQP emulator and HTTPS processes. Direct SQL handler fixtures are
identified separately; a seeded earlier Failed decision is explicitly recovery-handler evidence, not E2E.

Chromium remains NOT VERIFIED because of the carried-forward CA trust gap. Seven smoke tests are
implemented/discovered, including booking cancellation/refund and Admin/ownership/navigation.
After resolving browser CA trust without insecure flags, run against the same started profile:

~~~powershell
./scripts/Verify-Browser.ps1 -ConfigurationDirectory .local/recoverylive-<id>
~~~

The verifier now declares that configuration parameter and loads the pinned toolchain.
Browser discovery and rendered Angular tests do not establish actual browser acceptance.
Remote CI, Docker application SQL/images, cloud Entra/RBAC/managed identity and emulator restart
durability remain separate NOT VERIFIED claims. Enabled readiness probes the real owned broker;
the task does not stop the shared emulator to manufacture an outage test.
Next gate: independent Antigravity AZ-005 review, then AZ-006 Blob tour images.
