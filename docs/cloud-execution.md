# AZ-011 hosting and delivery preparation

**Implemented Awaiting Review. No Azure action has been executed.** AZ-010 independently passed
in [reviews/AZ-010.md](reviews/AZ-010.md). The current task changes only the Azure companion.
.NET SDK10.0.402, Angular21 and locked packages remain unchanged. Bicep0.48.1 was downloaded from
its [official release](https://github.com/Azure/bicep/releases/tag/v0.48.1) and its published SHA256
verified. Azure CLI2.91.0 is pinned for future cloud operations; it is not installed or authenticated
on this workstation. Linux/GitHub-hosted execution remains NOT VERIFIED.

## Infrastructure and security design

`infra/main.bicep` is resource-group scoped and composes storage, SQL/Cosmos, Service Bus, vault,
hosting, monitoring, settings and exact-scope RBAC modules. Parameters contain public metadata only;
never a provisioning password, private signing key, storage key, SAS or client secret. The sample
`infra/parameters.json.example` is deliberately not deployable. Copy and resolve it privately under
`.local`, together with `config/cloud/target.json.example`; review the exact suffix, region, tenant,
subscription, owner and group tags. No names, ports, writable paths, volumes or identities are shared
with the AWS repository.

| Resource | Prepared behavior |
|---|---|
| Five Web Apps | Linux .NET10 code ZIPs; one explicitly priced B2 plan/instance; Always On; TLS1.2/HTTPS-only; SCM/FTP Basic auth disabled; exact startup DLL; liveness health path |
| Functions | Separate Linux FC1 Flex plan, .NET10 isolated,2048MB,maximum40instances,no always-ready baseline; four calls/instance remain bounded by existing host.json |
| SQL | One database, binary collation, separate runtime users and five migrators; Entra-only server admin group; TLS, explicit firewall IPv4s; no AllowAzureServices wildcard |
| Service Bus | Standard; existing versioned queue/topic routing loaded from config/servicebus.json; explicit false `$Default` plus actual event filters; entity-scoped Sender/Receiver grants |
| Blob | Four accounts with OAuth/shared-key disabled, anonymous Blob access disabled and private containers; image Contributor, receipt Reader or custom read/write without delete; separate host/deployment account |
| Cosmos | One region, shared manual400RU/s database; /userId partition; departureUtcDESC/idASC composite index; Reservations container-only data role; local auth disabled |
| Key Vault | RBAC,90-day soft delete,purge protection; separate3072-bit wrapping keys; exact-secret and exact-key grants; no private signer in Bicep |
| Monitoring/budget |30-day workspace,0.1GB/day cap,workspace Insights with Entra metrics publishing; actual80%/forecast100% alerts on explicit USD budget; no hard spending limit |

The APIs and data-service endpoints in this low-cost profile have **public network ingress with
mandatory authentication**. Private Blob containers do not make the storage network private.
Backend JWT/owner/Admin checks remain independent of Gateway. VNet/private endpoints/NAT are not
silently added; they require a separately priced design. Existing cloud guards reject local test
broker flags, fault hooks, static credentials, local certificates and developer identity fallback.

[.NET10 App Service code hosting](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore)
and [Functions .NET10 isolated](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
are documented. Classic Linux Consumption is not this target. Flex configuration follows
[Functions IaC](https://learn.microsoft.com/en-us/azure/azure-functions/functions-infrastructure-as-code):
runtime/scale/deployment storage belong in functionAppConfig, not obsolete worker/runtime app
settings. Host storage and ReceiptBus identity settings are unprefixed for the Functions host;
worker-only configuration also has the existing AZURELAB_ prefix. Deployment storage uses system
identity with the separate host account Blob Owner grant; business storage never receives that role.

App Service's [secure unique hostname format](https://techcommunity.microsoft.com/blog/appsonazureblog/secure-unique-default-hostnames-ga-on-app-service-web-apps-and-public-preview-on/4303571)
can include a regional label. The templates request TenantReuse and use the **actual** defaultHostName
outputs. The endpoint validator accepts the documented regional form while retaining the lab prefix,
HTTPS/default-port/no-query/no-userinfo/exact-origin restrictions. Foreign/deceptive hosts remain
denied. Eight new hostname cases include six negative boundaries; previous security tests still pass.

## Offline commands actually appropriate to AZ-011

```powershell
./scripts/Verify-Infrastructure.ps1
./scripts/Build-Release.ps1 -Version az011-local-004
# Re-running an existing version refuses to overwrite it; choose a NEW version.
powershell.exe -NoProfile -File scripts/Verify-Foundation.ps1
```

The release builder performs locked restore, a production Angular build, Release .NET build,
framework-dependent publish of five APIs/Functions/privileged provisioner, and five EF10 idempotent
SQL scripts. It emits exactly six application ZIPs, one migration-tool ZIP, five schema scripts and
a SHA256/size/commit/version/dirty manifest in `.local/releases/<version>`; intermediate publish output
also remains private. Gateway ZIP contains the SPA. Functions ZIP contains host.json, worker.config,
extensions and the hidden .azurefunctions folder. The pinned Azure.Functions.Sdk uses runtime
worker indexing; it does not require a functions.metadata file. Linux deployment uses the DLL entry
points; local Windows packaging is not proof of a Linux or cloud startup.

The local release truthfully records a **dirty working tree** and is rejected for cloud delivery.
After independent review, commit the approved work and build a clean immutable release; AZ-011 does
not commit/push/publish on the user's behalf. Releases are content-immutable, not claimed to be
byte-for-byte reproducible across independent ZIP builds. Never put credentials, raw authenticated
reports or runtime configuration into the versioned package directory or upload other `.local` data.

## Future cloud sequence — AZ-012 authorization required

No following cloud example was executed. All review flags must reflect actual reviewed evidence;
never set them true just to pass a script. In particular, **exact trusted platform proxy topology and
capacity review remain mandatory gates**, including initial infrastructure execution. The baseline
rejects blanket forwarded-header trust and loopback proxies. A stable trustworthy App Service
proxy configuration has not been verified from this local workstation. If the selected topology
cannot provide it, stop and review a supported explicitly priced ingress design; do not clear known
proxy lists, trust arbitrary networks or enable ASPNETCORE_FORWARDEDHEADERS_ENABLED.
See [proxy security](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

1. Obtain independent AZ-011 PASS and explicit user cloud authorization. Review cost estimate,
   exact Azure subscription/tenant, tags, quota, runtime/region support, proxy topology, plan capacity,
   SQL network path, OIDC trust and retention. Create only the reviewed tagged lab group as a separate
   privileged bootstrap action. Providers must already be registered; preflight never registers them.
   App Service/Functions and SQL quota availability needs actual subscription evidence, not local tests.
2. Resolve private infrastructure/target metadata. `Test-AzurePreflight.ps1 -Target ... -Online` checks
   current identity, exact tags, tool version, provider states, .NET10/Flex region and recorded quota/
   egress/OIDC reviews. It does not switch subscriptions or prove data-plane access. A quota/capacity
   attestation is explicitly a human review record, not an automatic utilization measurement.
3. Use `Invoke-AzureInfrastructure.ps1 -Target .local/cloud-target.json -Parameters .local/parameters.json`
   for the offline Plan. Once separately authorized, `-Operation WhatIf -Execute -Authorization AZ-012`
   is a real Azure what-if. Review its IDs/costs. `-Operation Provision` is a separate deliberate mutation.
   Deploy resources first with configureApplications=false; no settings or signer-secret grants are
   applied. Stop the six apps before privileged work. There is no application/CD deployment in Bicep.
4. As the separately privileged provisioner, create **fresh** RSA3072 signing keys under private `.local`
   files, never reuse the AWS or local Development signer. Upload each private PKCS8 PEM using
   `az keyvault secret set --vault-name <exact-lab-vault> --name tourlab-azure-human-signing --file <private-file> --output none`
   (and the separate quote secret). Do not echo values. Record exact returned secret versions privately;
   export public SPKI PEMs and their uppercase SHA256 fingerprints. Resolve real reviewed proxies and
   SQL outbound IP allowlists. ConfigureApplications=true is a second reviewed infrastructure operation,
   using actual metadata and exact grants. Keep the apps stopped. RBAC propagation may delay startup;
   do not add broader roles to mask it. Secure private provisioning material and backup/retire it deliberately.
5. Connect as the **separate Entra SQL provisioning group**, directly to the reviewed database. Review
   and execute config/sql/azure-initialize.sql only after actual resource ownership inspection. It sets
   the ownership marker and RCSI; no master/server-login assumptions. Capture actual six system MI
   OBJECT IDs plus five distinct migrator OBJECT IDs from verified app/identity metadata. Fill the
   existing sql-principals plan, generate with Prepare-AzureSqlPrincipals.ps1, review it and execute as
   provisioner. Roles/ownership/history/receipt object denials remain independent of runtime code.
6. Run one schema at a time under its separate protected OIDC environment. `Invoke-AzureMigration.ps1`
   requires all six apps actually stopped, a clean exact release, expected object ID, short-lived SQL token,
   exact contained migrator name/SID, no inherited role, EngineEdition5 and lab marker. Identity seed
   requires private SeedUser JSON (exact Alice/Bob/Admin; fresh passwords, Tourist/Tourist/Admin),
   supplied with -SeedFile. Catalog receives only its existing synthetic seed. No other schema or real
   data is imported. The runtime applications never migrate. Token/seed-path environment values are
   removed after invocation; SDK exception bodies and credentials are not printed.
7. Reapply the reviewed SQL principal artifact **after** migrations to deny access to history tables and
   enforce receipt object/column restrictions. Execute actual positive/negative runtime permission
   probes, cross-schema/DDL/history denials and synthetic-user login. Record all five schema runs,
   exact release manifest SHA256, subscription/group, seed verification and effective permissions in
   a private migration-attestation file. Deploy rejects the false/default example attestation.
8. `Deploy-AzureRelease.ps1 -Target ... -Release ... -MigrationAttestation ...` defaults to a plan.
   Authorized execution uploads only exact hashed packages to the six existing sites, without IaC or
   migration credentials. It starts apps and performs bounded platform-TLS liveness and anonymous401
   tests. Functions ZIP deployment follows the current documented config-zip path for Flex, remote build
   false; API deployment uses Entra authentication with SCM Basic disabled. Actual delivery/listener
   success is NOT VERIFIED. Run authenticated readiness, booking/cancel/receipt/owner403-or404,
   direct-backend JWT and negative managed identity tests separately. Transport is at-least-once.
9. Exercise restart, same-cookie/ring persistence, rollback compatibility, actual monitoring charges and
   retained-data stop/removal only in AZ-012. See cloud-lifecycle.md. Do not down-migrate or purge data
   to get green tests.

## Manual OIDC workflow

`release.yml` is workflow_dispatch-only, main-only, with no Azure identity. It builds/packages and
uploads only its exact code/SQL release folder for7days. `cd.yml` is workflow_dispatch-only and
inert unless repository variable AZURELAB_CD_ENABLED is explicitly enabled during AZ-012 and the
input is AZ-012. It uses protected environments, no cancel-in-progress, no client secret, pinned
Azure/login2.3.0 commit and a runner-local pinned AzureCLI2.91.0 virtualenv. There is no push/main
trigger or infrastructure deployment step.

There are five distinct migration environments (`azure-study-migrate-identity`, etc.) and a separate
`azure-study-deploy` environment. Bicep creates five federated schema identities plus a separate
delivery identity. Migration identities get lab-group Reader metadata access and their own SQL
schema grants from the reviewed SQL artifact, never Website Contributor. Delivery gets Reader plus
Website Contributor at each exact site, never SQL migrator, Resource Group Contributor or Owner.
Runtime identities are system-assigned and never use those user-assigned CI identities.

Configure required reviewers, main-only branch restrictions and exact repository/environment OIDC
subjects. Each environment has AZURE_CLIENT_ID/TENANT_ID/SUBSCRIPTION_ID variables and private
AZURELAB_TARGET_JSON. Identity migration alone receives AZURELAB_SYNTHETIC_USERS_JSON;
deployment receives AZURELAB_MIGRATION_ATTESTATION_JSON after effective permissions and seed
checks. Do not share the seed secret with deployment. No environment or OIDC trust has been created.

Run all five migration dispatches serially, with all apps stopped; refresh the reviewed attestation,
then dispatch deploy against the **same package run**. Get-AzureRelease.ps1 verifies repository,
manual package workflow path, successful main run, exact commit, clean manifest and all twelve file
hashes. No raw authenticated test reports are uploaded. Actual hosted workflow/OIDC/cloud execution
is NOT VERIFIED. Expired seven-day packages require a new reviewed package and corresponding
manifest attestation, not an unverified artifact substitution.

| Concept | GitHub Actions here | Jenkins / Azure Pipelines comparison |
|---|---|---|
| Validation | existing CI plus offline infrastructure checks | staged pipeline/job with locked tools and test results |
| Immutable package | versioned release artifact plus manifest | archived/fingerprinted build artifact / pipeline artifact |
| Approval | protected environment plus manual dispatch | input gate / environment approval/check |
| Federation | GitHub OIDC environment subject | explicitly configured workload identity federation, not a copied client secret |
| Delivery | serialized deploy existing apps | deployment stage consuming the approved artifact; provision remains separate |

## Measured hosting boundary

Measure-LocalHosting.ps1 launched only its own five HTTPS API processes, made50strict-CA liveness
requests and stopped only those processes. The recorded Windows Development sample is483.53MiB
summed working set,497.49MiB sum of individual peaks (not necessarily simultaneous). Workers were
disabled and no business/Functions load ran. See evidence/AZ-011-local-hosting.json. This observation
supports a candidate shared plan discussion; it does not validate B2 memory/CPU/throughput in Azure.
Actual Linux/AlwaysOn/background-worker load, restart, CPU/p95 latency, SQL/Cosmos throttling and
Functions scaling remain unverified and must be explicitly planned in the mandatory capacity review;
actual hosted evidence is required for AZ-012 acceptance. No expensive
upgrade or fallback deployment is automatic.
