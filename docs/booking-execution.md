# Booking and Service Bus execution — AZ-004

Run from the independent TourLab Azure repository. .NET 10.0.402 and pinned Angular 21 are required.
All payments and notification deliveries are simulated. They use durable SQL effects, never real financial or email providers.
No cloud deployment or paid namespace is needed or authorized.

## First local setup
Use scripts/New-LocalConfiguration.ps1 and the generated lab CA. Trust the CA explicitly for interactive HTTPS;
never use insecure certificate flags. Start Docker Desktop's Linux engine.
After reading and explicitly accepting both Microsoft licenses, run:

~~~powershell
./scripts/Start-Broker.ps1 -AcceptSqlDeveloperLicense -AcceptServiceBusLicense
~~~

The script starts only this Compose project's broker-sql and servicebus, with pinned image digests.
It refuses to recreate an existing broker. Existing lab containers/data must be inspected and retained.
Service Bus uses loopback ports 5673/5301; its dedicated SQL has no host port.
Check existing readiness with:

~~~powershell
Invoke-WebRequest http://localhost:5301/health -UseBasicParsing -TimeoutSec 5
docker compose --env-file .local/dependencies.env ps broker-sql servicebus
~~~

Licenses: [SQL Developer](https://go.microsoft.com/fwlink/?LinkId=746388),
[Service Bus emulator](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt).

## Reproduce verification

~~~powershell
. ./scripts/Use-Toolchain.ps1
dotnet restore TourLab.Azure.slnx --locked-mode
dotnet msbuild src/Receipts.Functions/Receipts.Functions.csproj -t:GenerateFunctionsExtensionProject
dotnet build TourLab.Azure.slnx -c Release --no-restore
./scripts/Verify-Booking.ps1
./scripts/Verify-Workflow.ps1
~~~

Verify-Booking creates a fresh SQL Server LocalDB profile/database and executes the complete relational
suite. Use -Profile Docker for the separately started application SQL container; that mode was not
executed for AZ-004. Broker SQL is not the application SQL database.

Verify-Workflow requires the already healthy authorized broker. It creates a fresh SQL profile,
writes private ignored Messaging configuration, executes routing and redelivery tests before starting
consumers, then starts five real HTTPS hosts. It runs BFF security, live booking and DLQ tests and
Gateway restart/TLS checks. Only its own host process trees are stopped afterward. SQL data, provider
effects and the malformed-message DLQ probe are retained. Reports/private config remain ignored.
No secrets, cookies or authentication traces are uploaded.

For Windows PowerShell 5.1 use its full executable path as shown in evidence/AZ-004.md.
The script resolves its current PowerShell executable, so PowerShell Core installation is not required.

## Explore the application
Use the fresh workflow configuration directory printed by verification:

~~~powershell
./scripts/Start-Host.ps1 -Service Catalog -ConfigurationDirectory .local/workflow-<printed-id>
./scripts/Start-Host.ps1 -Service Reservations -ConfigurationDirectory .local/workflow-<printed-id>
./scripts/Start-Host.ps1 -Service Payments -ConfigurationDirectory .local/workflow-<printed-id>
./scripts/Start-Host.ps1 -Service Notifications -ConfigurationDirectory .local/workflow-<printed-id>
./scripts/Start-Host.ps1 -Service Gateway -ConfigurationDirectory .local/workflow-<printed-id>
~~~

Run each command in its own shell. Open https://localhost:9443 after trusting the generated lab CA.
Use fresh private seeded credentials; do not paste them into Git or logs.
Browse Tours, choose a future session and submit 1..6 participants at the displayed accepted price.
The same unchanged form keeps its idempotency key for explicit retries; submission is never automatically retried.
The booking detail shows persisted transitions; refresh to see Confirmed and the simulated effects.
My bookings, Payments and Notifications are protected paginated routes. Another owner receives 404
for individual booking/timeline/payment/notification resources. Admin authorization is enforced by APIs.

Keep initialized databases and broker volumes. No purge/down -v/reset is part of verification.
Stopping/restarting an emulator is not production message durability proof.

## Evidence boundaries
Actual final local run: real SQL Server 2025 LocalDB, Service Bus emulator 2.0.1 plus its pinned SQL
dependency, five .NET HTTPS processes and SDK 7.21.0. See evidence/AZ-004.md for reports/counts.
Browser smoke is implemented and discovered, but Chromium CA trust remains NOT VERIFIED.
Angular unit/render tests and HTTP clients do not establish browser acceptance.
Linux hosted CI execution, five containerized application images, real Azure SQL/Service Bus/RBAC,
managed identity and durability across emulator restart are NOT VERIFIED.

Microsoft documents emulator differences and limits:
[overview and quotas](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator),
[local setup](https://learn.microsoft.com/en-us/azure/service-bus-messaging/test-locally-with-service-bus-emulator),
[filter expressions](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-filter-examples).
The observed one-hour maximum TTL is explicit in local configuration. SQL deduplication retention
is independent and not shortened: no cleanup removes Inbox/idempotency/provider/tombstone history.
Full cancellation/admin fault/diagnostic/DLQ replay interfaces belong to AZ-005.
