> AZ-002 supersedes the foundation-only host instructions below. Use [SQL/Identity execution](sql-identity-execution.md) for SDK10.0.402, provisioning and authenticated hosts. The remaining text records AZ-001's historical scaffold.

# AZ-001 local execution and review

Run all commands in C:\Users\JesseRangel\Documents\ChatGPT\TourLab Azure.
The original Tour Microservices path is read-only context. This repository is independent and has no remote.

## Restore, build and test

PowerShell7, .NET10.0.401, Node24.18.0/npm12.0.1 and Docker Compose CLI are required for the
full foundation script. Docker engine and Functions Core Tools are checked separately.
Do not use dotnet --info alone as proof: in the sandbox its workload diagnostic hit a Service Control Manager permission error;
dotnet --version and actual build are the effective evidence.

    ./scripts/New-LocalConfiguration.ps1
    ./scripts/Verify-Foundation.ps1
    ./scripts/Test-Preflight.ps1

New-LocalConfiguration refuses to overwrite .local; skip it on later sessions. Credentials and
server key/PFX are newly generated, private, ignored and never copied from the original.
The CA key is discarded after signing. Existing .local is reused, not deleted to repeat tests.
Foundation verification performs locked restore, explicit Functions extension restore, .NET build/tests,
npm ci/tests/audit, production SPA packaging and Compose structure validation.
Full preflight intentionally exits1 when Docker or Functions tools are missing; this is an environment gap.

## HTTPS shell

    ./scripts/Trust-LocalCa.ps1
    ./scripts/Start-Host.ps1 -Service Gateway

Trust-LocalCa imports only the new CA into the Windows current-user root store. Windows may require
an interactive confirmation; run it in your own terminal if the automation session cannot finish.
Verify the certificate subject/fingerprint before accepting; trust is not claimed merely from generation.
On Linux/macOS import the CA into your user/browser trust store using platform instructions.
Never disable TLS verification. Start-Host runs in the foreground; Ctrl+C stops only that host.

Open https://localhost:9443 after building web assets. /health/live reports real process liveness;
readiness is503 ScaffoldNotReady because AZ-002+ persistence/business dependencies are absent.
No login, booking, payment, receipt or Admin success endpoint is fabricated.
Other hosts can be started in separate terminals by passing Catalog, Reservations, Payments or Notifications.

For Angular development, run Gateway first and then:

    $env:NODE_EXTRA_CA_CERTS = (Resolve-Path .local/certificates/ca.crt).Path
    Set-Location web/tours-angular
    npm start

Angular serves https://localhost:4201 and validates Gateway's CA through NODE_EXTRA_CA_CERTS.
Production assets are served from Gateway's single origin; proxy secure=true remains enabled.
Unknown /api, /auth, /health and file paths return404, rather than the SPA shell.
Only normal client navigation gets the shell.

## Browser smoke

Keep Gateway running, trust the CA in the browser/OS, and from another terminal run:

    ./scripts/Verify-Browser.ps1 -InstallBrowser

Chromium is installed under Azure .local/playwright; browser garbage collection is disabled.
Playwright Test1.64.1 supplies its matching browser revision. Tests exercise real Angular/Gateway
and require successful certificate validation. API request trust through NODE_EXTRA_CA_CERTS is separate
from Chromium's certificate store. Traces/screenshots remain local/ignored; no auth state is uploaded.
This is foundational smoke only; full user workflows belong to later tasks.

## Dependencies: explicit licenses and preserved state

Read [SQL Server license](https://go.microsoft.com/fwlink/?LinkId=746388) and
[Service Bus emulator license](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt).
SQL Server Developer is development/test only.

    ./scripts/Start-Dependencies.ps1 -AcceptSqlDeveloperLicense -AcceptServiceBusLicense
    docker compose --env-file .local/dependencies.env ps
    ./scripts/Stop-Dependencies.ps1

Start only checks engine availability and starts isolated dependencies. It does not certify readiness
or create business schemas/entities. Each later task provides actual integration checks.
No original containers are stopped/restarted, no shared volumes mounted, and no data is deleted.
Stop preserves volumes. Do not use down -v, database deletion or queue purge as test cleanup.
Service Bus emulator2.0.1 has its own SQL engine and may lose broker data on restart.
Cosmos preview supports only a documented subset/gateway mode and lacks cloud authentication.
App-process restart with running broker differs from emulator reset; no broker durability claim is made.

## Containers and Functions

After Build-Web, build each API independently:

    docker build --build-arg SERVICE=Gateway -t tourlab-azure-gateway:az001 .
    docker build --build-arg SERVICE=Catalog -t tourlab-azure-catalog:az001 .
    docker build --build-arg SERVICE=Reservations -t tourlab-azure-reservations:az001 .
    docker build --build-arg SERVICE=Payments -t tourlab-azure-payments:az001 .
    docker build --build-arg SERVICE=Notifications -t tourlab-azure-notifications:az001 .

Images run as non-root, use pinned MCR digests and do not publish host endpoints.
Local native HTTPS is the supported debugging profile.
Functions scaffold builds with Azure.Functions.Sdk1.0.1/Worker2.52 and generates worker configuration
with worker indexing and extension payload. Install compatible Functions Core Tools4.x using
https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local before invoking the host.
A verified Core Tools patch pin remains a recorded prerequisite, not an invented installed version.
Configure ignored local.settings.json/AzureWebJobsStorage for this lab's Azurite account only, set
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated, then invoke func start --port 7072 in src/Receipts.Functions.
There is only a Function-authorized liveness function; receipt trigger/business behavior is AZ-008.
No real Functions trigger evidence is claimed from compilation.

## Source immutability and independent review

    ./scripts/Record-SourceSnapshot.ps1 -SourcePath 'C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices' -Phase after

Do not overwrite the initial before snapshot during this task. Compare commit/status/hashes and flag
external changes without resetting them. Relevant source evidence includes the pre-existing dirty CI workflow.
Read docs/evidence/AZ-001.md and prompts/REVIEW-ANTIGRAVITY.md. Independent review is pending;
local passing checks cannot mark it Verified. No cloud deployment, push, paid resource or remote repository
is created. Next implementation is AZ-002 after the review gate, with real SQL Server prerequisites.
