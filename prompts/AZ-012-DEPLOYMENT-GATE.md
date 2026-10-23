# AZ-012 is not automatically authorized

Use only AFTER the user explicitly authorizes deployment to an identified Azure subscription/resource group with a reviewed budget and estimate. This file alone is NOT deployment authorization.

Before any mutation: read AGENTS/specs/review evidence; resolve blockers; verify subscription/tenant/region/resources/tags and current official SKUs/runtime support; show exact resource plan, fixed/variable costs and deletion/retention behavior. Obtain any outstanding required cloud authorization for that concrete scope. Do not touch original AWS resources or use its database/secrets.

Then provision through the reviewed Bicep/scripts, run one-shot SQL provisioning/migrations separately from runtime identities, deploy versioned API/Angular/Functions artifacts, verify managed identity/RBAC/Key Vault/TLS and genuine Service Bus/Blob/Cosmos behavior, and run isolated full-stack smoke. Record failures and cost exposure, do not repeatedly provision resources to guess. Verify stop and cleanup procedures truthfully, including App Service plan charges and retained data. Record actual acceptance evidence, URLs without secrets, artifact versions, monitoring and rollback limitations. No automatic-main deployment until separately enabled.
