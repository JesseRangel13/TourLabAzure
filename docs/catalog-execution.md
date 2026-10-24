# AZ-003 Catalog execution

Work in C:\Users\JesseRangel\Documents\ChatGPT\TourLab Azure only.
Prerequisite: AZ-002 independent PASS in reviews/AZ-002.md.
Use .NET SDK 10.0.402, Node 24.18.0/npm 12.0.1, Angular 21, and the private toolchain helper.
Windows PowerShell 5.1 and Core 7 can invoke the verification commands below.

## Build and verify

~~~powershell
. ./scripts/Use-Toolchain.ps1
./scripts/Verify-Foundation.ps1
./scripts/Verify-Catalog.ps1
~~~

The Catalog verifier creates a unique tourlab_azure_catalog_<guid> database in the separate
tourlab-azure LocalDB instance, fresh restricted credentials and synthetic users, then applies
all five schemas' migrations, seeds two Azure-only synthetic tours/sessions without resetting existing data, and executes the full SQL/security/Catalog suite. It preserves
all data and prints only the private configuration directory, not secrets.
Use the printed directory for live tests and manual hosts:

~~~powershell
./scripts/Verify-HttpsBff.ps1 -ConfigurationDirectory .local/catalog-<guid>
./scripts/Start-Host.ps1 -Service Catalog -ConfigurationDirectory .local/catalog-<guid>
./scripts/Start-Host.ps1 -Service Gateway -ConfigurationDirectory .local/catalog-<guid>
~~~

The live verifier starts and stops its own five host process trees; it refuses occupied ports
and never kills an existing listener. Close only manually started lab hosts before running it.
The complete latest commands and run-specific directory are in evidence/AZ-003.md.

The existing default .local/sql database-file collision is preserved. Use a fresh directory/database;
do not delete or attach pre-existing SQL files to make this task pass. No original database is used.

## Human actions

Open https://localhost:9443 after explicitly trusting this lab's CA.
Get generated synthetic passwords privately from the selected directory's provisioner.json.
Never paste it into chat, commit it, or record authentication screenshots/traces.

- Sign in as Alice or Bob, open Tours, search, page, and follow a tour link.
  Detail shows UTC departure, MXN price per participant, and available/total seats.
- Sign in as Admin. Tours or Admin catalog exposes Create tour. Enter name/description.
  Open its detail to edit description/name/activation or create a future UTC session.
- Session controls update integer MXN cents or nonnegative capacity using its last observed version.
  A conflict requires Reload tour and an explicit new edit. The client never retries a mutation automatically.
- Inactive tours are visible only to Admin. They cannot produce new quotes or holds.
- Signed-out/expired/account-change state clears the Catalog feature. Route guards are UX;
  backend JWT, role, CSRF and service-quote boundaries remain authoritative.

Catalog create defaults to active as in the approved source. "CRUD" preserves source actions:
there is no hard-delete endpoint; inactivation preserves quote/hold/history references.
Images are AZ-006 and booking submission/broker delivery is AZ-004, not simulated successes here.

## Endpoints and contracts

External browser prefix: /api/catalog. Direct Catalog routes:

| Route | Policy / behavior |
| --- | --- |
| GET /v1/tours?search=&page=1&pageSize=10 | Human JWT; page >=1, size 1..50; case-insensitive literal substring search; stable name/ID order; active-only for Tourist |
| GET /v1/tours/{id} | Human; inactive hidden with 404 for Tourist |
| GET /v1/tours/{id}/sessions | Same visibility; departure/ID order |
| POST /v1/admin/tours | Admin; 201; bounded name/description |
| PUT /v1/admin/tours/{id} | Admin; expectedVersion; edit and activate/inactivate |
| POST /v1/admin/tours/{id}/sessions | Admin; future UTC departure, positive integer cents, nonnegative capacity; 201 |
| PUT /v1/admin/sessions/{id}/price | Admin; expectedVersion, positive MXN cents |
| PUT /v1/admin/sessions/{id}/capacity | Admin; expectedVersion; cannot go below held/confirmed occupancy |
| POST /v1/internal/quotes | Separate Reservations signer with quote:create; Gateway always blocks external internal routes |
| GET /v1/openapi/v1.json | Authenticated Human, Development only; generated DTO contract, no private configuration |

400 validation/required fields, 401 credential failure, 403 Admin failure, 404 visibility/not found,
409 version/price/quote/operation conflict, 503 database dependency failure. ProblemDetails
includes safe code and traceId. No raw parser exception or SQL is returned.

## Persistence and concurrent inventory

Catalog owns Tours, Sessions, Quotes, Holds, Outbox and Inbox under catalog.
Its runtime principal has no DDL/history or other-schema access; privileged credentials never enter hosts.
A new CatalogInventory migration adds only Catalog-owned tables/constraints/indexes.
DateTimeOffset domain values convert to UTC datetime2 in SQL; long business versions remain distinct
from transport rowversion. No PostgreSQL migrations or runtime resources were copied.

Logical quote-request/hold/quote-use locks use sys.sp_getapplock with Transaction owner, 5-second bound,
and catalog runtime role principal. Tour then Session uses UPDLOCK/HOLDLOCK; all operations that need
both use that order. Inventory operations first serialize HoldId, then quote-use/tour/session as needed.
Capacity and expiry/release use only their required Session lock. No whole-transaction automatic retry.

The last-seat allocation is an atomic SQL UPDATE WHERE AvailableSeats >= participants. It detaches the
stale EF session after SQL. Hold + response Outbox intent commit together, or join an existing owning
Inbox transaction. Confirm never decrements inventory again. Expiry/release restores once and records
an intent. Release before hold persists a nullable-quote tombstone; a filtered unique QuoteId index
permits many tombstones but one consumption per quote. Nothing purges them; at least 14-day retention
is preserved. Duplicate delivery is permitted; effects are idempotent, not exactly-once transport.

Quotes validate 1..6 participants, active future sessions and accepted price/currency, checked integer
MXN totals, a stable request fingerprint and five-minute expiry. Existing quotes keep accepted price
and tour-name snapshots across subsequent price/name changes. Hold expiry is ten minutes.
A rejected operation cannot turn successful after capacity increases.

HoldExpiryWorker scans at most 50 overdue Held rows every five seconds, using one async scope per
scan and a new scope/transaction per hold. Competing instances serialize expiry against confirmation
and release. It writes durable Outbox intents addressed only to tourlab-azure-domain-events, not broker messages. Each logical event has one topic delivery, without per-subscriber copies. No dispatcher/consumer or fault
selection is claimed before its assigned task.

## Browser smoke and evidence boundary

tests/browser/catalog.spec.ts exercises actual Admin create/edit/session/price/capacity and a separate
Tourist browser context, refresh, visibility and sign-out. It does not mock HTTP or mutate SQL.
Authentication trace/screenshots stay off; TLS validation stays on.

~~~powershell
./scripts/Verify-Browser.ps1 -ConfigurationDirectory .local/catalog-<guid>
~~~

Start Gateway and Catalog against that same directory first. The recorded Chromium CA-trust gap
remains NOT VERIFIED; test discovery is not a browser pass. Do not set ignoreHTTPSErrors or insecure flags.
SQL LocalDB evidence is not Docker SQL 2022, Azure SQL or cloud deployment evidence.