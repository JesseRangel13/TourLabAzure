# AZ-003 walkthrough

Read Application/Contracts.cs and CatalogEndpoints.cs first. HTTP DTOs belong to Catalog;
Shared.Contracts contains only integration messages and their envelope. CatalogQueries handles reads,
CatalogCommands explicit mutations, QuoteHandler quote creation, InventoryHandlers internal typed commands.
No reflection dispatcher, generic repository or interface-per-class is needed.

Trace UpdateCapacity from Angular ChangeSessionCapacity through CatalogApi, the secured endpoint and
CatalogCommands.CapacityAsync. expectedVersion prevents stale edits; occupied = capacity - available.
The same SQL transaction checks and updates the session. A 409 stays visible until the user reloads.
Angular guards do not authorize a database mutation.

Trace HoldAsync next. Logical operation identity is durable. Locking QuoteId prevents two holds using
the same quote; Tour locking serializes activation changes; Session locking coordinates capacity and
release. The conditional UPDATE guards available inventory at SQL itself. The Hold and Outbox share
the transaction. Inspect OuterInboxTransactionRollbackRevertsInventoryHoldAndResponseIntentTogether
to see how the future AZ-004 Inbox owner can roll everything back together.

Compare ConfirmAsync, ReleaseAsync and ExpireAsync. Confirmation changes the hold state without
deducting again. Expiry and release test state under locks before restoration. A release-first
tombstone refuses late recreation; no timer or message ordering assumption substitutes for durable state.
Study the concurrent tests and the actual two-host expiry worker test; no broker delivery is involved yet.

Angular CatalogApi owns HTTP, CatalogState owns shared data/loading/errors and request identity, and
components own transient typed forms. NGXS actions return observables so completion and failure reach
the component. Selectors derive reads; selectSignal adapts a selector for template rendering.
Signals do not replace the required NGXS actions or RxJS cancellation. cancelUncompleted replaces
obsolete filters/details; generation guards prevent cross-account late responses. Neither API
credentials nor browser tokens are persisted.

Follow the source migration snapshot versus CatalogModel. SQL Server needs explicit filtered uniqueness
for nullable QuoteId tombstones, datetime2 conversion and transaction lock APIs. SQL rowversion is not
a business time/version. The original PostgreSQL files remain unchanged.

References:
- [SQL Server application locks](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql?view=sql-server-ver17)
- [SQL Server table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17)
- [ASP.NET Core OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0)
- [NGXS action lifecycle/cancellation](https://www.ngxs.io/concepts/actions/actions-life-cycle)