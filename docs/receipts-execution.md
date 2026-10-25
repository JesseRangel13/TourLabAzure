# AZ-008 local receipt execution

The document is a **SIMULATED EDUCATIONAL BOOKING CONFIRMATION**. It is neither a legal tax invoice
nor a receipt for an actual payment. Only new successful confirmations carry the immutable snapshot.
Cancellation keeps that historical document; the booking detail page shows the current authoritative
Reservations status separately.

## Prerequisites and reproducible commands

Run from this Azure repository only. Use the existing isolated SQL2025 LocalDB instance,
Service Bus emulator and Azurite. The broker's SQL container is never an application database.
Do not restart/recreate emulators to repair a failed check, and do not purge queues, documents or SQL.
The SQL Developer and Service Bus emulator licenses were accepted explicitly in this chat.

Windows PowerShell5.1 is supported. The private .NET10.0.402 toolchain is selected by the scripts.
Core Tools4.16.0 is installed only under ignored .local/tools; no global installation is required.
The installer pins the official Windows x64 release archive and its SHA256.

```powershell
. .\scripts\Use-Toolchain.ps1
dotnet restore TourLab.Azure.slnx --locked-mode
dotnet msbuild src/Receipts.Functions/Receipts.Functions.csproj -t:GenerateFunctionsExtensionProject -verbosity:minimal
dotnet build TourLab.Azure.slnx -c Release --no-restore
.\scripts\Verify-Foundation.ps1
.\scripts\Verify-Receipts.ps1 -IncludeHttps
```

Verify-Receipts creates and retains separate fresh SQL/private profiles for relational checks and
actual Functions/live checks. It provisions only the private receipt container in the existing local
Azurite account. It never starts, recreates or destroys Docker services. Install-Functions downloads
the pinned official tool if absent; a failed download or integrity check stops verification.

The host test stages commit a Notifications receipt job/Outbox, publish its command to the real queue,
start the actual isolated Functions host, and pause that invocation after Blob verification using a
private Development-only barrier. The helper kills only its own child process tree, verifies Pending
SQL plus the existing object, removes the barrier, and starts a new host. Redelivery after lock/lease
expiry must mark Ready without changing the Blob ETag. It also sends a duplicate, malformed JSON and a
contradictory pre-existing object, verifies built-in DLQ reasons and retained conflict audit, and leaves
those DLQ messages/data in place. A minute of broker lock expiry is expected; test waits are bounded.

-IncludeHttps starts only helper-owned API processes on9443..9447, completes real cookie/CSRF booking,
waits for the actual Function, downloads the attachment through Gateway, tests Alice/Bob/Admin, and
cancels the booking while retaining identical receipt bytes. Both terminal simulated notifications
must still reach Sent. Gateway restart/TLS rejection checks also execute. All owned process trees stop
in finally; databases, profiles, containers, Blobs and DLQ evidence remain.

-SkipRelational reruns the host/live categories using another fresh retained profile; it does not
claim the SQL regression category ran. Ordinary SQL scripts intentionally skip receipt emulator/host
categories unless their explicit environment flags are supplied.

## Manual local use

To use an existing retained profile after verification, substitute its repository-relative directory:

```powershell
$profile = '.local/receiptshost-REPLACE_WITH_RETAINED_RUN_ID'
dotnet run --project tools/Lab.Provisioner -c Release --no-build -- receipts-local $profile
.\scripts\Start-Receipts.ps1 -ConfigurationDirectory $profile
# In separate terminals, use Start-Host.ps1 with the same profile for the five APIs.
```

Start-Receipts explicitly binds Core Tools to127.0.0.1:9450. Its HTTP health route is process liveness,
not proof a receipt is Ready. Generation is a Service Bus trigger, not an HTTP generation endpoint.
The Functions host gets AzureWebJobsStorage and ReceiptBus before startup from a restricted ignored
receipts-host.json. The worker gets only Notifications Runtime SQL plus receipt storage from
receipts.json; it gets no migrator, Identity signing key or other service's SQL credentials.

Local host storage uses standard azure-webjobs host coordination containers; the business documents
use the separate private tourlab-azure-booking-receipts container. Both use the lab's existing dedicated
Azurite account, whose local account key cannot enforce per-container RBAC. This is not proof of cloud
identity isolation. Metadata and ownership remain Notifications SQL, not host storage. No browser gets
the account key, a SAS, a connection string or a Blob endpoint.

## HTTP and UI

- GET /api/notifications/v1/receipts/reservation/{reservationId}: at most10 historical receipt statuses,
  own stored owner or Admin; another owner/absent returns404.
- GET /api/notifications/v1/receipts/{jobId}/content: authorized bounded attachment, no-store/nosniff.
  Pending/Conflict returns409; unavailable or failed-integrity Ready content returns503.
- Open an authoritative booking at /reservations/{id}. Refresh confirmation status and download Ready
  documents. Pending, absence, integrity conflict, storage error, loading and retry are explicit.
  The panel shows the current SQL booking status separately from the historical confirmation.

NGXS holds status metadata only. It clears on logout/account change/401 and ignores stale loads and
downloads. File bytes remain transient; the browser object URL is revoked after the download click.
No tokens or storage credentials are persisted.

## Recovery and evidence boundaries

Pending SQL jobs and their leases survive process death. Retry uses the same job ID/key/content hash.
A matching existing object is verified by bounded download and SHA256 before Ready; metadata alone is
insufficient. A conflicting object is never overwritten: Conflict and a safe SQL audit are persisted,
and the command reaches the built-in receipt queue DLQ. Do not clear Inbox or create a new business ID
to retry. Preserve the object/audit and investigate; cloud repair/replay operations require their own
review. This task does not expose a receipt overwrite or destructive cleanup operation.

The existing API Operations DLQ UI addresses the original command/topic entities; receipt poison
inspection in AZ-008 is through the actual host verification SDK/retained private logs and SQL audit.
A cloud receipt-specific operational replay UI is not claimed.

Read evidence/AZ-008.md for actual run counts/profiles and limitations. Chromium remains NOT VERIFIED
because of the established browser CA trust gap; never disable TLS validation to obtain a green run.
Real Azure Functions/SQL/Blob/Service Bus identity, permissions, TLS/quota/scale/restart durability and
cloud deployment remain NOT VERIFIED and separately authorized in later tasks.

## Official compatibility references checked 2026-10-24

- [.NET isolated worker guide](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide):
  .NET10 uses Worker2.50+ and Azure.Functions.Sdk1.0+; this repo retains2.52.0/1.0.1.
- [Service Bus SDK binding and manual settlement](https://learn.microsoft.com/azure/azure-functions/functions-bindings-service-bus):
  ServiceBusMessageActions requires AutoCompleteMessages=false; extension5.24.0 is pinned and locked.
- [Core Tools4.16.0 release](https://github.com/Azure/azure-functions-core-tools/releases/tag/4.16.0):
  worker-indexed output is supported; a separate functions.metadata file is not required.
- [Blob conditional concurrency](https://learn.microsoft.com/azure/storage/blobs/concurrency-manage):
  If-None-Match prevents overwrite; If-Match binds verification download to the inspected ETag.