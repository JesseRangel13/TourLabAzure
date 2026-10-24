# SQL Server provider port — AZ-002

The source-provider-inventory.md records actual PostgreSQL sites. None of the original migrations,
entities, DbContexts or private configuration were copied. Separate owned .NET models now target
Microsoft.EntityFrameworkCore.SqlServer10.0.13. Each of five schemas has its own fresh migration/history.

| Source concern | Azure implementation/evidence | Remaining task |
|---|---|---|
| Npgsql context/options | Each service owns its DbContext; Identity uses factory; RuntimeDatabase validates exact restricted user/pool/TLS profile | Domain tables added with their owning tasks |
| PostgreSQL migrator/runtime roles | Tagged contained local database; no-login schema owners; separate migration/runtime users and runtime role per schema | Azure external users artifact is NOT VERIFIED in Azure |
| Cross-schema access/inherited grants | Explicit per-schema denials, no cross-schema FKs; runtime history denied; real five-principal and inherited-public-grant tests | Repeat against Docker/Azure profiles |
| SKIP LOCKED/RETURNING Outbox claims | SqlOutboxLeases single UPDATE OUTPUT; UPDLOCK/READPAST/READCOMMITTEDLOCK; bounded lease and publication-owner/expiry fence | Actual send-before-mark/inbox settlement crash windows in004 |
| PostgreSQL ON CONFLICT Inbox | Owned unique(ConsumerName,MessageId); tested2601/2627 recognition, rollback and fresh winner read | Consumer handlers/idempotent business transactions in004 |
| jsonb | nvarchar(max) plus ISJSON check; bounded indexed type/destination/effect keys | Typed envelope contracts/filter routing in004 |
| timestamptz | UTC DateTime in datetime2 with converter rejecting unspecified/local writes and restoring UTC kind | Domain dates/amounts in003 onward |
| Version semantics | Native8-byte SQL rowversion concurrency token; tested stale-writer rejection | Separate businessVersion remains per-reservation in007 |
| Case-sensitive identifiers/keys | Explicit binary collation; Identity normalized-name keys remain framework-controlled | Validate future Catalog searches/HTTP idempotency hashes in003/004 |
| GUID ordering | Lease query orders SQL-owned OccurredAtUtc/DeliveryId; no claim that .NET GUID order is SQL order | Specify/test keyset ordering for future rebuilds |
| PostgreSQL error codes | SqlRules.IsUnique handles2601/2627; no use of an aborted transaction after failure | Business-specific conflicts in003/004 |
| UPDATE capacity/hold/refund/Saga SQL | Not implemented or asserted passed in002 | Catalog003; Saga/provider004/005 |
| Source image recovery SQL | Not imported into a placeholder implementation | Blob006 |
| Source booking projection SQL | No DynamoDB/SQL fallback is disguised as Cosmos | Cosmos007 |

Runtime users get owned-schema DML, not DDL, migration history, other schemas or administrative roles.
Migration users get CREATE TABLE plus ALTER/REFERENCES/DML only for the owned schema; the privileged
provisioner creates principals/schemas first. Schema ownership remains a distinct no-login principal.
Separate owners prevent cross-schema ownership chaining. Default guest access is revoked.
Explicit public schema grants are removed on bootstrap; effective denied access is tested using actual
connections and a temporary inherited public SELECT grant, then that permission change is reverted.

Read-committed snapshot is enabled on the new database to exercise the Azure SQL locking constraint.
The tested native engine is SQL Server2025 Express LocalDB17.0.4025.3 at compatibility level160.
Application code uses bounded command/connection timeouts and no EF automatic transaction retries.
No network send occurs inside a SQL transaction; no sender/consumer is activated before AZ-004.
Tests retain synthetic verification rows/databases. Transport permits duplicates/reordering; these
SQL guarantees are not exactly-once message delivery or proof of cloud behavior.

Official references checked2026-10-23:
- https://learn.microsoft.com/en-us/ef/core/providers/sql-server/
- https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb
- https://learn.microsoft.com/en-us/sql/t-sql/statements/create-user-transact-sql
- https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table
- https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview
- https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery
