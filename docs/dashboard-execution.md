# Cosmos booking dashboard — execution (AZ-007)

Use only the Azure companion root. Prerequisite: actual independent AZ-005/AZ-006 PASS.
Requirements: pinned .NET10/Angular21 toolchain, real SQL Server2025 LocalDB, Docker Linux,
the existing isolated Service Bus broker, and the dedicated Cosmos emulator.

## Run in Windows PowerShell5.1

    .\scripts\Start-Cosmos.ps1
    .\scripts\Verify-Foundation.ps1
    .\scripts\Verify-Dashboard.ps1 -IncludeHttps

Start-Cosmos starts only this lab's Cosmos service if absent. It creates a fresh private account key
under ignored .local, mounts the existing lab TLS certificate read-only, supplies its password from
private configuration and uses HTTPS on loopback8089 with retained tourlab-azure_cosmos-data.
It refuses to recreate existing containers. No original resource, broker restart or data purge.
The Azure root .local directory already has a restricted ACL from initial setup.

Verify-Dashboard creates .local/dashboard-<guid> and a fresh owned SQL database, applies migrations,
then invokes the provisioner dashboard-local command. It initializes only database
tourlab-azure-dashboard / container bookings, /userId partition, required composite index declaration,
no TTL, and an isolated Reservations cursor encryption certificate/key ring. Repeated setup preserves
that profile's credentials, keys and data. No browser credentials or public cloud account is used.

The relational/Cosmos phase intentionally excludes staged live/broker dashboard tests.
-IncludeHttps additionally requires existing healthy broker; it never restarts/recreates it.
It creates a separate .local/dashboardlive-<guid> SQL profile to avoid publishing the relational suite's
staged workflow commands. It runs real SDK/broker/Cosmos settlement checks before starting API hosts,
then the HTTPS helper's BFF, booking/cancel/dashboard/poison and Reservations/Gateway restart checks.
Only the helper's own API trees are stopped on exit. Config, SQL, Cosmos documents and DLQ are retained.
No source test data is used. -Profile Docker requires separately initialized application SQL;
broker SQL never hosts business schemas.

To use an existing private profile:

    . .\scripts\Use-Toolchain.ps1
    dotnet run --project tools/Lab.Provisioner -c Release --no-build -- dashboard-local .local/sql

Enable this profile's Messaging with the existing local broker configuration for live projection.
For a prepared dashboardlive verification profile, start all five APIs with Start-Host.ps1 using that
same -ConfigurationDirectory; Build-Web.ps1 serves the production Angular bundle from Gateway.
Never copy/paste private keys, passwords, certificates or connection strings into tracked files/logs.

## Human APIs and UI

All browser paths use /api/reservations; backend routes below start /v1.
Backend validates its own human JWT; Gateway retains cookie/CSRF/Origin and credential throttling.

| Method/path | Meaning |
|---|---|
| GET /v1/dashboard/bookings?pageSize=10&cursor=... | Own partition only,1..20 rows, protected continuation |
| GET /v1/dashboard/bookings/{reservationId} | Own partition point read; other owner=>404, including Admin |
| POST /v1/admin/dashboard/rebuild | Admin only; {batchSize:1..50,cursor:null-or-protected};202 after SQL Outbox commit |

userId is always derived from sub. A supplied userId query parameter cannot change partition.
Invalid/tampered/expired/wrong-user/wrong-size cursors=>400. Cosmos outage/disabled=>503 with safe
code/traceId and no SQL fallback. Success data responses are no-store. Rebuild never waits for Cosmos.

Angular /dashboard shows My booking dashboard, eventual consistency, projection times/versions and
links to /reservations/{id} for authoritative status/actions. First page / Refresh and Next dashboard
page use the typed NGXS client. /dashboard/{id} displays a point read and authoritative link.
Tourist cannot see Admin recovery controls. Admin Start projection rebuild / Continue rebuild batch
enqueue at most25 snapshots per explicit click, reporting scan completion separately from projection lag.
Rebuild is not a delete/reset; existing payment/refund/Inbox/data remain.

## Evidence boundary

Use evidence/AZ-007.md for exact results. Cosmos SDK/emulator tests, broker tests, host tests, browser
discovery and actual Azure proof are separate. Chromium's known CA trust gap remains NOT VERIFIED;
never bypass certificate checks. vNext does not enforce Azure authentication or composite indexes.
The declared index and query ordering can be tested locally, but actual index enforcement, RU cost,
cloud RBAC/managed identity and emulator restart durability remain NOT VERIFIED.
Receipts/Functions belong to AZ-008; this task does not deploy or provision cloud resources.
