# Blob tour images — local execution (AZ-006)

Prerequisite: independent AZ-005 PASS, .NET10 pinned toolchain, real SQL Server and Docker Linux.
Use only this Azure companion directory. Original AWS/Blazor resources are read-only.
No Azure resources, paid plans, browser storage credentials, public container or SAS are used.

## Execute

From Windows PowerShell5.1 at the Azure repository root:

    .\scripts\Start-Images.ps1
    .\scripts\Verify-Foundation.ps1
    .\scripts\Verify-Images.ps1 -IncludeHttps

Start-Images starts only the pinned Azurite service if absent. An existing container is retained;
a stopped/unavailable container requires inspection, never recreation/purge. It uses the dedicated
tourlabazure account generated under ignored .local/dependencies.env, loopback ports10010..10012,
and the dedicated tourlab-azure_blob-data volume. It does not start SQL/broker/Cosmos.
The default SQL test profile is isolated SQL2025 LocalDB tourlab-azure. -Profile Docker requires
the separately initialized application SQL service; broker SQL is never reused.

Verify-Images creates a fresh private .local/images-<guid> profile/database, applies owned migrations,
verifies/creates only private tourlab-azure-tour-images through the SDK, enables that Catalog profile,
and runs SQL/Azurite checks. Config, keys, test objects and database are retained.
It excludes staged LiveImageTests from its relational phase; these require -IncludeHttps.
The HTTPS helper checks ports9443..9447 are free, starts its own five hosts, stops/restarts only
its own Catalog for persisted crash fixtures, and stops only its own host trees on exit.
The existing broker is neither required nor restarted for this image-specific gate.
The test fixture advances only its own image lease deadlines to simulate elapsed downtime.

For an existing lab SQL profile, explicitly enable images:

    . .\scripts\Use-Toolchain.ps1
    dotnet run --project tools/Lab.Provisioner -c Release --no-build -- images-local .local/sql
    .\scripts\Start-Host.ps1 -Service Catalog -ConfigurationDirectory .local/sql

Start Gateway and the other APIs with that same private profile for the full UI. Build-Web.ps1
copies production Angular assets to ignored Gateway wwwroot. Navigate https://localhost:9443,
sign in as a privately seeded Admin, open a tour and use JPEG or PNG image, Upload image,
Remove image, Reload images and Recover image controls. Tourist sees ready images and placeholders.
Seed credentials remain private in that profile's provisioner.json; never paste/log/version them.

## API

All routes below have the /api/catalog proxy prefix in the browser, /v1 directly on Catalog.
Catalog independently validates human JWTs; every Admin route independently requires Admin.
Gateway mutations retain cookie authentication, Origin/antiforgery and existing login rate limits.

| Method/path after /v1 | Purpose |
|---|---|
| GET /tours/{tour}/images | Tourist ready images; Admin bounded active/history metadata |
| GET /tours/{tour}/images/{id}/content | Authorized bounded proxy, no-store/nosniff, decoded MIME |
| PUT /admin/tours/{tour}/images/{id} | Raw bounded JPEG/PNG body; stable upload intention ID |
| DELETE /admin/tours/{tour}/images/{id} | 202 after durable DeletePending; immediate read hiding |
| POST /admin/tours/{tour}/images/{id}/recover | One due owned intent; false while leased/not due |
| GET /admin/tours/{tour}/images/{id}/history | Latest30 safe persisted recovery outcomes |

Unsupported bytes=>415; corrupt/oversize dimensions=>400; over2MiB=>413; lease/count/content conflict=>409;
busy upload/read=>429; dependency outage=>503; hidden/mismatched tour/image=>404; missing identity=>401,
non-Admin mutation=>403. Safe ProblemDetails includes code/traceId. No arbitrary object-key API exists.

## Evidence boundaries

Real SQL/Azurite and HTTPS tests are separate from Chromium browser execution and Azure cloud proof.
The image Playwright test is implemented/discovered; known Chromium CA trust gap remains NOT VERIFIED.
Never use ignoreHTTPSErrors or bypass TLS. See evidence/AZ-006.md for actual executed commands/results.
No receipts/Functions or Cosmos functionality is implemented by AZ-006.
