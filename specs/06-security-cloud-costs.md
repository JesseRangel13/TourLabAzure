# 06 — Security, cloud preparation, costs

## Managed identity and Key Vault
Local: ASP.NET user-secrets/ignored env files and fresh lab key material. No Key Vault emulator is required or claimed. Cloud: managed identities for Service Bus, Blob, Cosmos, Key Vault and Azure SQL where feasible. Use Azure.Identity with a deliberate cloud credential selection; do not permit an unnoticed fallback to developer credentials in production. Distinguish end-user authentication from workload identity.

Key Vault holds real application secrets that cannot be replaced by managed identity, e.g. lab JWT signing/private certificate material. Do not store every public setting as a secret or introduce static access keys just to demonstrate Key Vault. Document whether Key Vault references or SDK retrieval is used, startup/refresh/failure behavior, required role assignments, and secret rotation. Give metadata/status not secret values in diagnostics.

Identity-owned Data Protection keys persist durably and are encrypted using supported certificate/Key Vault protection, accessible only to Gateway. Explain key-ring storage versus encryption key and restart behavior. Signing and Data Protection material must survive an app restart. Local tests cannot prove cloud managed identity/Key Vault references.

## IaC
Use Bicep modules and Azure CLI scripts: resource group-scoped plan/apps, Service Bus Standard (topics required), Blob/Functions host storage, Cosmos API NoSQL database/container/indexes, Azure SQL server/database, Key Vault, identities/RBAC and minimal monitoring. Bicep is deliberate Azure preparation, independent of original Terraform. Do not create a second Terraform Azure implementation.

Follow current official .NET10/App Service and Functions support. Five APIs may share one measured paid plan; Functions supported Flex Consumption profile is separate. Build deployment packages and serve Angular from Gateway; no mandatory ACR if code deployment suffices. Infrastructure provisioning and application delivery are separate operations.

Bicep parameters/templates/logs never contain real secrets. Document private provisioning outside committed parameter files; generated deployment artifacts and state-like exports stay ignored. Initial cloud tasks are not launched by local CI. Validate bicep build/lint; report real what-if only when actually executed with authorized read access. No unauthorized Azure mutation.

## Cost constraints
Local-first is the zero-cloud-cost path. A faithful always-running Azure profile is NOT promised free. Before any deployment, produce docs/cost-estimate.md using current official calculator/pricing, selected region/currency/SKUs, fixed and variable charges, daily/weekly/monthly study use and continuous operation.

Important: stopping Web Apps does NOT stop charges for their App Service plan. Five shared apps share paid compute capacity. Service Bus Standard has a base charge and cannot be paused like a container; topic/subscriptions need Standard/Premium, not Basic. Log ingestion/retention, Functions host storage, Blob requests/storage, Key Vault operations, bandwidth, SQL and Cosmos all need consideration. Budget alerts are not hard limits.

Assess Azure SQL free-offer eligibility and stop-at-free-limit option; do not assume credits/eligibility. Polling workers/health checks can prevent SQL auto-pause and consume the allowance. Cosmos free tier (where available) requires explicit enablement at creation, has subscription/account constraints and differs from serverless billing; do not label all serverless Cosmos usage free. Start with one region, minimal throughput/one shared database, no unplanned autoscale or multi-region. Verify defaults before deployment.

Default budget target is local-only until an actual estimate is reviewed. Do not provision resources with a silently selected expensive tier because a free offer is unavailable. Optional private endpoints/VNet, paid monitoring and staging slots require a separate price/design decision.

## Lifecycle and CI/CD
preflight verifies target subscription/tenant/region/resource group/tags, configuration, provider registration, quota and tool support. Provision is deliberate. deploy updates explicit versioned artifacts after migration job, waits for health and smoke tests. Stop documents remaining charges rather than claiming zero. Destroy is explicit and scoped to AzureLab tags/resource group, shows resources and retention choices, preserves data by default; never touches AWS. No unconditional resource-group deletion script without reviewed scope.

GitHub Actions CI first. Cloud CD initially workflow_dispatch after explicit deployment authorization, OIDC federated Azure identity, minimal deployment permissions, no long-lived client secret. Deploy app releases without implicitly reapplying infrastructure. Use concurrency controls, immutable artifacts, compatible one-shot migrations, secret redaction, smoke tests and rollback limitations. Automatic main deployment is a later explicit opt-in and must not silently activate a paused study environment. Do not add Azure DevOps pipelines until actual assignment requirements establish that need; include a conceptual Jenkins/GitHub/Azure Pipelines mapping.
