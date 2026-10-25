-- AZ-009 supersedes the previous display-name-only AZ-002 draft.
-- This file intentionally cannot provision users with guessed or implicit identities.
-- Prepare an idempotent Azure SQL artifact using explicit reviewed managed-identity OBJECT IDs:
-- scripts/Prepare-AzureSqlPrincipals.ps1 -Plan <private/public-plan.json>
-- See config/cloud/sql-principals.json.example and docs/security-execution.md.
-- The generator checks all IDs/database names and never connects to SQL or Azure.
-- Execution is a separate explicitly authorized AZ-012 operation.
THROW 51000,'Generate the reviewed Azure SQL principal artifact with explicit object IDs first.',1;