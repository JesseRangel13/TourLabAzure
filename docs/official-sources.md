# Official sources and verification notes

Checked 2026-10-23. These are references, not proof of deployment. Recheck versions, region capabilities, limits and pricing when implementing.

- Angular version compatibility: https://angular.dev/reference/versions
  Angular21 requires a compatible Node/TypeScript/RxJS combination; do not use newest Angular by default.
- NGXS21 documentation: https://www.ngxs.io/v21.0
  Verify actual package peer dependencies and available versions before pinning.
- SQL Server EF provider: https://learn.microsoft.com/en-us/ef/core/providers/sql-server/
- Service Bus emulator: https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator
  Emulator loses data/entities on container restart and lacks cloud identity/network features.
- Service Bus messaging: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview
- Topics and SKU limitations: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-tutorial-topics-subscriptions-portal
- Service Bus pricing: https://azure.microsoft.com/en-us/pricing/details/service-bus/
- Azurite: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite
- Cosmos local Linux emulator: https://learn.microsoft.com/en-us/azure/cosmos-db/emulator-linux
  Subset of features, gateway mode; the documented vNext emulator does not enforce authentication. Do not claim RBAC coverage.
- Cosmos concurrency/transactions: https://learn.microsoft.com/en-us/azure/cosmos-db/database-transactions-optimistic-concurrency
  ETag conditional writes; transactional boundary is logical partition, not SQL+Cosmos.
- Functions isolated .NET guide: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide
  .NET10 uses isolated worker; classic Linux Consumption is not the .NET10 target. Verify Flex Consumption support and current tooling.
- Managed identity connections: https://learn.microsoft.com/en-us/azure/azure-functions/manage-connections
- Key Vault: https://learn.microsoft.com/en-us/azure/key-vault/general/overview
- App Service plan costs: https://learn.microsoft.com/en-us/azure/app-service/overview-manage-costs
  Stopped apps do not stop paid plan charges.
- Azure SQL free offer: https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier
  Eligibility/limits and exhaustion choice require review; polling affects active compute usage.
- Cosmos free tier: https://learn.microsoft.com/en-us/azure/cosmos-db/free-tier
  Explicit creation-time choice; not equivalent to universally free serverless.
- Playwright best practices: https://playwright.dev/docs/best-practices
- Azure calculator: https://azure.microsoft.com/en-us/pricing/calculator/

No fixed currency estimate is asserted in this specification because subscription, region, free-offer eligibility and measured task capacity are not yet known. AZ-011 must produce a sourced estimate before AZ-012 can be authorized.
