# 01 — Architecture and technology mapping

## Processes and ownership
- Gateway.Api: ASP.NET Core Identity, same-origin Angular hosting, BFF cookie boundary, YARP/API routing.
- Catalog.Api: tours/sessions/quotes/inventory; image metadata and Blob object management.
- Reservations.Api: reservation API, durable Saga, versioned Cosmos projection publisher/worker and rebuild.
- Payments.Api: payment API, fake provider and reconciliation workers.
- Notifications.Api: fake notifications and sender; booking receipt status/download API.
- Receipts.Functions: one .NET 10 isolated-worker Functions application processing receipt commands, owned by Notifications. Its code shares only Notifications-owned application logic; it does not query other service schemas.
- web/tours-angular: Angular 21 SPA, NGXS state and typed HTTP clients. Production assets served by Gateway to keep one origin; local development uses Angular's proxy.

Use folders within services or a modest layer split; avoid assembly proliferation. Shared.Contracts contains transport DTOs/envelopes; Shared.Infrastructure contains technical plumbing. Each domain owns its DbContext, migrations, runtime principal, and business types.

## Mapping
| Original | Azure companion |
|---|---|
| .NET 10 APIs | .NET 10 ASP.NET Core Web APIs |
| Blazor UI | Angular 21 + NGXS |
| PostgreSQL/Npgsql | SQL Server + EF Core SqlServer; Azure SQL Database in cloud |
| SQS/ElasticMQ | Azure Service Bus queues + topic/subscriptions; official emulator locally |
| S3 | Blob Storage; Azurite locally |
| DynamoDB | Cosmos DB for NoSQL; verified supported emulator locally |
| Fargate/Terraform | App Service + Functions; Bicep |
| AWS secret delivery | Managed identity + Key Vault for actual secrets |
| Existing browser tests | Playwright Test TypeScript targeting Angular |

## Local profile
Use Docker Compose for application SQL Server, Service Bus emulator (with its OWN documented SQL dependency), Azurite, and Cosmos emulator. The emulator's infrastructure database is not a business database. Pin image versions/digests after availability verification. Require explicit SQL developer-license acceptance. SQL Server Developer is development/test only; Azure SQL is the cloud target.

Prefer native dotnet/Angular/Functions host processes during debugging; also provide buildable container images for the five APIs. Use gateway mode when required by Cosmos emulator. Bind insecure emulators to loopback/private Docker network, never public interfaces. Local-only endpoint/credential overrides must fail closed in Azure configuration.

Emulators are not production replicas: Service Bus emulator restart can lose queued data; Cosmos vNext has limited APIs and may not enforce authentication. Test app-process restarts with broker running. Do not claim broker durability across emulator restart, or IAM/RBAC proof from emulator tests. Do not republish all already-sent Outbox entries automatically to conceal a broker reset. Provide an explicit reset/reseed/rebuild procedure.

## Cloud target
Five distinct Web Apps on App Service, initially allowed to share one paid App Service plan for study costs, plus a separate isolated Functions app on an appropriate plan. Sharing a plan shares CPU/memory and failure domains; it is not independent compute isolation. Measure capacity and use Always On for durable workers. Free App Service is not the reference for unattended workers.

Choose Linux .NET 10 code hosting when supported by the selected region/runtime; if unavailable, record an ADR and use verified supported .NET 10 container hosting rather than downgrading. For Linux .NET 10 Functions use a supported plan such as Flex Consumption, not classic Linux Consumption. Recheck platform support before implementation/deployment.

Only Gateway is a human entry point. Backend Web Apps still have backend JWT validation and ownership checks. Use unique short-lived gateway service credentials per backend request where needed; protect worker-only/admin routes. App Service system ingress exists: configure forwarded headers only for the documented trusted platform topology and reject untrusted claims. If low-cost connectivity cannot network-isolate backend apps, document their public network exposure with mandatory authentication; do not call them private. Private endpoints/VNet integration are an explicitly priced optional hardened profile, not hidden defaults.

Do not add Kubernetes, API Management, Azure Container Apps, Redis, or Entra end-user login to the required implementation. Explain these as future comparisons only.
