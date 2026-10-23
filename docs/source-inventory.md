# AZ-001 source inventory

Inspected 2026-10-23. Source: C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices.
Target: C:\Users\JesseRangel\Documents\ChatGPT\TourLab Azure.
They are distinct sibling directories with no shared worktree, root link or writable volume.
Every implementation command explicitly targets the Azure directory; the chat's initial working directory remains the original.

Source commit: 41ad79265ea7a7ec66af20aa3f8a7342b77d910d.
Initial dirty state: M .github/workflows/ci.yml. Preserve that user change.
AZ-001-source-before.json contains SHA-256 hashes of all 416 tracked files, commit and status.
The after snapshot compares the same evidence. These hashes prove tracked-content comparison, not immutable OS access enforcement or unchanged running databases.

## Observed implementation

| Area | Actual source paths and interpretation |
|---|---|
| Hosts | src/Gateway.Web (Identity/Blazor/YARP), Catalog.Api, Reservations.Api, Payments.Api, Notifications.Api; TourMicroservicesLab.slnx |
| Catalog | CatalogEndpoints.cs; Application/CatalogOperations.cs and InventoryHandlers.cs; Persistence/CatalogDb.cs; owned tours/sessions/quotes/holds and Outbox |
| Reservations | ReservationEndpoints.cs; Application/ReservationHandlers.cs, SagaRecovery.cs, CatalogQuoteClient.cs; persistent Saga/deadlines/history/idempotency/compensation |
| Payments | PaymentEndpoints.cs; Application/PaymentProcessing.cs, RefundProcessing.cs; independent durable fictional provider effects, reconciliation/refunds |
| Notifications | NotificationEndpoints.cs; Application/NotificationProcessing.cs; persistent Pending work and independently committed simulated sender receipts |
| Messaging | Shared.Contracts/*Contracts.cs; Shared.Infrastructure/Messaging/MessageCodec.cs, LocalTransactions.cs, OutboxDispatch.cs, SqsTransport.cs, Workers.cs; SQS per-destination deliveries |
| Diagnostics | Shared.Infrastructure/Operations/ServiceOperations.cs; protected redacted diagnostics, durable faults and receive/lease/replay DLQ handling |
| Blazor UI | Gateway.Web/Components/Pages/*.razor; all declarations in source-http-inventory.md; image controls under Components/Catalog/TourImages.razor |
| Additional Angular UI | frontend/angular/src/app; observed Angular 22.2 dependencies, login/tours/booking/detail/cancellation; this is a partial parallel source frontend, not the required Angular 21/NGXS companion |
| S3 images | Catalog.Api/Images/ImageStorage.cs, TourImages.cs, ValidatedImage.cs, ImageEndpoints.cs; actual S3 SDK, durable SQL intent/lease/delete tombstones; 2 MiB, 2048x2048, 3 slots, 2 concurrent validations; signed URLs in source replaced with proxy downloads |
| DynamoDB bookings | Reservations.Api/Bookings/BookingStore.cs, BookingProjection.cs; owned user partition/GSI, conditional version writes, protected cursors and bounded rebuild; SQL remains authoritative |
| Persistence | Five owned PostgreSQL schemas with independent migrations, runtime/migrator principals; provider sites listed separately; no source database copied |
| Security | Gateway Identity/AuthEndpoints.cs, Browser/BrowserEndpoints.cs and Shared.Infrastructure/LabHosting.cs; cookie/CSRF, backend JWT and separate Reservations service credential |
| Local lifecycle | compose.yaml, scripts/*.ps1, tools/Lab.Provisioner; existing source ports/resources are context only |
| CI | .github/workflows/ci.yml includes actual .NET, Angular, SQL/broker/storage suites and five images; dirty extension adds BFF/browser workflow. Do not treat this dirty file as remotely executed |
| Cloud | infra/terraform, scripts/aws; reviewable AWS artifacts; LAB-009 cloud deployment remains pending in source task evidence |
| New receipts | No original Receipts.Functions host or downloadable immutable booking document. Durable sender receipts are a different existing feature; Azure receipt feature is intentionally new |

## Contracts and invariants

Source authority read: README.md, specs/03-contracts.md, specs/04-workflows.md, extension specs,
tasks/implementation-plan.md and relevant evidence/reviews. Actual contracts were inspected under Shared.Contracts.
HTTP resource declarations and every observed UI route are recorded in source-http-inventory.md.
Message routing inventory and Azure delivery decisions are in message-routing.md.
The parity matrix lists human actions individually, including Admin/failure/rebuild paths.

Preserve participants 1..6, integer MXN cents/checked arithmetic, future departure, 5-minute quote,
10-minute hold, 30-second availability/confirmation deadlines, bounded 2-minute payment resolution,
stable charge/refund/hold IDs, release tombstones, ownership404 and at least14-day dedup retention.
Do not infer exactly-once delivery from logical effect uniqueness.
CatalogQuoteClient.cs defaults total timeout10s and per-attempt3s. An old source review says40s;
current code/config and Azure spec require10s (D12). No money or inventory rule is changed in AZ-001.

## Verification history is reported, not rerun

LAB-007-review.md records independent PASS and two actual Chromium tests; older LAB-007.md still has historical browser gaps.
CI-review.md records PASS for its reviewed snapshot. EXT-001-002-review.md records PASS for storage extensions.
EXT-001-002.md records real local emulator integration and regressions. These are source reports,
not evidence that the Azure port passes or that the current source dirty state was reviewed.
No source test, provisioner, container lifecycle, database operation or cloud deployment was run here.
Later Angular22/CI changes have no new independent review assumed by AZ-001.

## Selective reuse decision

No source production files, lockfiles, EF migrations, .git, secrets, generated artifacts or test reports were copied.
The small provider-neutral host foundation is new; domain behavior is inventoried for selective reuse in later tasks.
This avoids bringing source PostgreSQL/AWS infrastructure or Angular22 into an empty Azure scaffold.
Shared.Contracts currently contains only the new liveness DTO; business message contracts are mapped, not falsely implemented.
