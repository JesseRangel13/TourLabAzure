# AZ-011 cost estimate — no deployment authorized

Prepared 2026-10-25, **East US (`eastus`), USD, public pay-as-you-go retail**, one region.
The approved default remains local-only: **$0 Azure consumption while nothing is provisioned**.
These figures are assumptions for review, not a bill or a guarantee. Subscription offers, taxes,
exchange rates, quotas and actual utilization have not been verified. No credits, free tiers,
reservations, Azure Hybrid Benefit or savings plans are deducted.

Official [Azure Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices)
was actually queried. Selected meter values are retained in
[evidence/AZ-011-retail-prices.json](evidence/AZ-011-retail-prices.json); explicit scenario arithmetic
is in [evidence/AZ-011-cost-model.json](evidence/AZ-011-cost-model.json).
Refresh the [Azure calculator](https://azure.microsoft.com/en-us/pricing/calculator/) and verify the
actual subscription quote before AZ-012. Budget alerts warn; they never enforce a spending cap.

| Resource / selected setting | Public rate used / charge driver |
|---|---|
| Five Linux .NET 10 APIs on one Basic B2, 1 instance, Always On | $0.034/hour = $24.82/730h; shared 2 cores/3.5GB, no slots or autoscale |
| Service Bus Standard, 5 queues, 1 topic/5 subscriptions | $0.013441/hour base (~$9.81/730h); do not also add the API's alternative $10/month meter; gross $0.80/million operations |
| SQL GP serverless Gen5, max 1 vCore/min 0.5, 60min auto-pause | $0.521758/vCore-hour, CPU/memory dependent; 32GB storage at $0.115/GB-month; IO modeled $0.20/million |
| Cosmos NoSQL, single region, database-shared manual 400RU/s | 4 x $0.008/hour = $23.36/730h plus $0.25/GB-month; free tier explicitly false |
| Four Standard LRS accounts: images, receipt documents, cursor ring, Functions host/deployment | Hot Blob $0.0208/GB-month; writes $0.05/10k, reads $0.004/10k; soft-deleted versions also consume storage |
| Functions Flex, .NET 10 isolated, 2048MB, no always-ready instances | On-demand $0.000026/GB-second and $0.000004/10 executions; maximum 40 instances is a scale ceiling, not a hard monetary cap |
| Key Vault Standard, software RSA keys and versioned signing secrets | $0.03/10k operations; retain all wrapping-key versions needed by rings |
| Log Analytics + workspace-based Application Insights | $2.30/GB ingestion modeled gross, 30-day retention; configured 0.1GB/day quota is approximate, can overshoot and can suppress diagnostics |
| Internet egress | $0.087/GB gross estimate; no shared free allowance credited |

Rate sources: [App Service Linux](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/),
[Service Bus](https://azure.microsoft.com/en-us/pricing/details/service-bus/),
[SQL](https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single/),
[Cosmos](https://azure.microsoft.com/en-us/pricing/details/cosmos-db/autoscale-provisioned/),
[Blob](https://azure.microsoft.com/en-us/pricing/details/storage/blobs/),
[Functions](https://azure.microsoft.com/en-us/pricing/details/functions/),
[Key Vault](https://azure.microsoft.com/en-us/pricing/details/key-vault/) and
[Monitor](https://azure.microsoft.com/en-us/pricing/details/monitor/).

## Study and continuous scenarios

All resources remain allocated for the entire stated window. A study session is 2h, followed by a
1h SQL idle tail; all five APIs and Functions must actually be stopped for SQL to become idle.
The SQL range assumes 0.5–1 billed vCore while online; memory can raise the billed amount within
this configured envelope. Polling workers, health checks, pooled sessions or a resume can keep
compute running. The continuous scenario therefore matters more than optimistic auto-pause.
See [serverless billing](https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-billing).

| Window | SQL active hours | Estimated total USD |
|---|---:|---:|
| One retained day, one session | 3 | $3.09–3.87 |
| One retained week, five sessions | 15 | $20.04–23.95 |
| 730h month, twenty sessions | 60 | $85.67–101.32 |
| 730h continuous workers | 730 | $260.46–450.90 |

The reproducible model assumes 5GB total Blob data, 1GB Cosmos data, daily 100 Functions invocations
at 2GB x 2s, 1,000 Blob writes/1,000 reads, 200 vault operations, 2,000 broker operations,
0.03GB logs, 0.1GB internet egress and 20,000 SQL IO operations. It adds a **planning reserve** of
$5/730h for host transactions, metrics and backup variance; that reserve is not a quoted meter or
an upper bound. SQL backup beyond included allowance, growth, retries, diagnostics and traffic
must be priced from actual usage. No hidden VNet/private endpoint, NAT, ACR, paid certificate,
slot, extra region or paid monitoring add-on is included. A separate hardened network design
needs another quote. Sample $500/month alerts require explicit review; they authorize no spend.

## Free offers and stopping

The [SQL free offer](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer) can provide
100,000 vCore-seconds, 32GB data and 32GB backup/month for eligible databases. Eligibility has not
been established. Review its **auto-pause until next month** limit behavior rather than opting
into overage billing. The template intentionally selects a priced serverless profile and does not
claim offer eligibility; enabling a reviewed free-offer profile requires an explicit parameter/SKU
change and fresh validation before deployment. Never substitute a paid tier silently after rejection.
Even 0.5vCore for sixty active hours is 108,000 vCore-seconds, beyond that allowance.

[Cosmos free tier](https://learn.microsoft.com/en-us/azure/cosmos-db/free-tier) is opt-in at creation,
limited by subscription/account eligibility, and separate from serverless billing. The template
uses one manually provisioned shared database and no autoscale or multi-region. Its explicit
`cosmosFreeTier` switch must remain false until eligibility is reviewed; enabling it is not a promise
that every meter is free.

**Stopping Web Apps does not stop App Service plan charges.** Service Bus retains its base charge;
Cosmos retains provisioned throughput; Blob/SQL/backup/monitoring storage continues billing. SQL
may pause only after activity really stops. Removing compute while retaining data is still not free.
Use the inventory/retention procedure in [cloud lifecycle](cloud-lifecycle.md). The local memory
observation is described in [cloud execution](cloud-execution.md); it is not proof of B2 cloud capacity.
