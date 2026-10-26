# Azure lifecycle — preparation only

AZ-011 executed none of these cloud operations. Every script defaults to a local plan. A command
with `-Execute -Authorization AZ-012` is an explicit future cloud action, still requiring the user's
AZ-012 authorization, independent review, correct credentials and reviewed ownership/cost gates.
Read [cost estimate](cost-estimate.md) first. The original AWS repository and resources are excluded.

| Command | Offline/default behavior | Future authorized behavior |
|---|---|---|
| Test-AzurePreflight.ps1 | validate exact private target metadata | `-Online`: read-only tenant/subscription/group tags, pinned tools, providers, regional runtimes and reviewed quotas |
| Invoke-AzureInfrastructure.ps1 | validate non-secret ARM parameters and show Plan | WhatIf or Provision; never deploys application packages or migrates SQL |
| Invoke-AzureMigration.ps1 | validate hashes and show one schema operation | one OIDC schema identity, all six apps stopped, SQL principal/SID/engine/ownership guards; idempotent migration and owned seed |
| Deploy-AzureRelease.ps1 | validate immutable package and exact release attestation | deploy existing apps only; start, strict HTTPS smoke; stop on startup/smoke failure |
| Stop-AzureLab.ps1 | describe six exact apps and continuing charges | stops exact tagged apps; retains all plans/data |
| Remove-AzureLab.ps1 | describes retention policy | requires private inventory hash, exact tags and per-resource ShouldProcess; preserves data by default |

Private metadata must be inside **this Azure repository's `.local`**; parent escapes and junctions/
symlinks are rejected. Scripts check current account rather than switching subscriptions, and inspect
actual resource-group region/Project/Environment/Owner plus resource prefix/tags. No script creates
an untagged resource group, registers providers, grants a subscription-wide Owner role or purges data.
Creating the exact study group, provider registration and quota approval are separate AZ-012
bootstrap operations by an authorized owner. Inspect actual provider SKU/quota availability before
setting review flags; a boolean is a recorded human attestation, not an automatic Azure proof.

## Stop and retention

```powershell
# Local plan only
./scripts/Stop-AzureLab.ps1 -Target .local/cloud-target.json
./scripts/Remove-AzureLab.ps1 -Target .local/cloud-target.json
```

Stopping preserves database rows, Service Bus messages, Cosmos documents, Blob objects,
certificates, identities and key rings. SQL polling workers must all stop, including Functions, before
expecting auto-pause. Service Bus message TTL/dead-letter expiry behavior still applies; do not
assume stopping APIs freezes retention clocks. Review rates rather than calling stop zero-cost.

Default removal allows only the six App Service/Functions site resources and two plans; destroying
sites removes system-assigned identities, so old role assignments and SQL SID mappings require
review before recreating them. Data, broker, migration/delivery identities, vault/keys, monitoring,
role definitions and resource group remain. Do not claim immediate restart after compute removal.

A future `Remove-AzureLab.ps1 -Execute -Authorization AZ-012` first fetches/validates actual inventory,
writes `.local/cloud-destroy-inventory.json`, emits SHA256 and refuses deletion without its matching
`-InventorySha256`. Review every ID and the retention choices, then rerun with the hash and explicit
confirmation. A changed inventory requires a new review. `-IncludeData -RetentionAcknowledged` also
selects the exact SQL server/database, broker, Cosmos and storage accounts; these deletes can lose
bookings, receipts and queued work. Vault purge, group deletion, AWS resources, generic wildcard
resource deletion and test-data cleanup are never implemented. Vault/keys, identity and monitoring
retention is deliberate and must be included in the remaining bill. Storage soft deletion and SQL
backup retention are recovery aids with limitations, not a verified restore or export.

## Delivery failure and rollback

A partial ZIP failure leaves the study environment stopped; inspect private deployment diagnostics
and rerun a reviewed immutable release. On startup/smoke failure the delivery script stops the six
apps and retains all data. There are no Basic-tier slots or transactional six-app rollout guarantees.
Migration scripts are one-shot, idempotent **forward** changes; do not automatically down-migrate,
restore a database or undo simulated money/inventory operations. Use a compatible previous code
release only after schema/contract review; otherwise apply a forward fix. Restore/backup rehearsal,
actual managed identity negative permission tests and authenticated end-to-end smoke belong to
AZ-012. Keep all versions needed by existing Data Protection rings/cursors during key rotation.
