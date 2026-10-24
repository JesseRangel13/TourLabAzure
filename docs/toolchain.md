# Pinned toolchain and verification

Checked2026-10-23 against official docs, package metadata and MCR manifests. Registry availability
is not proof a container starts on this host. Lockfiles belong to this new project.

| Dependency | Pin | Evidence |
|---|---|---|
| .NET SDK |10.0.402, rollForward disabled | installed; restore/build pass |
| ASP.NET runtime/container |10.0.13 | installed runtime; MCR digest pinned in Dockerfile |
| Functions SDK/worker | Azure.Functions.Sdk1.0.1 / Worker2.52.0 / Http3.3.0 | NuGet availability; net10 build/metadata pass |
| Functions Core Tools | no verified installed pin | NOT VERIFIED: command missing; install compatible4.x via official instructions before host checks |
| Node/npm |24.18.0 /12.0.1 | installed; package engines and .nvmrc enforce |
| Angular runtime/compiler |21.2.25 | npm registry availability and compatible build |
| Angular CLI/build |21.2.28 | npm registry availability and compatible build |
| NGXS |21.0.0 | npm peer: Angular>=21<22; RxJS>=7 |
| TypeScript/RxJS |5.9.3 /7.8.2 | Angular21 compatibility table and successful compile |
| Vitest/jsdom |4.1.12 /28.1.0 | locked; patched moderate audit finding; tests pass |
| Playwright Test |1.64.1 | independent lockfile; Chromium version supplied by matching package |
| SQL Server |2022-CU24-ubuntu-22.04 | MCR manifest digest verified; runtime NOT VERIFIED |
| Service Bus emulator |2.0.1 | MCR manifest digest verified; runtime NOT VERIFIED |
| Azurite |3.37.0 | MCR manifest digest verified; runtime NOT VERIFIED |
| Cosmos vNext |vnext-latest at immutable sha256 in Compose | official supported preview entry; manifest verified; runtime NOT VERIFIED |
| Docker/Compose | observed29.6.2 /5.3.1 | CLI present; engine absent; Compose config validated |

Functions SDK's generated extensions project needs a separate GenerateFunctionsExtensionProject target
after solution restore (see scripts/Verify-Foundation.ps1). First build logged AZFW0108 fallback;
explicit restore avoids claiming a fully offline build from an incomplete restore.
Generated Functions extension payload/metadata is verified locally; do not confuse it with host invocation.
Business SDKs (EF SqlServer, Service Bus, Blob, Cosmos) are pinned when introduced, not added unused now.
No automatic Angular major upgrade or npm audit fix --force is used.

References:
- https://angular.dev/reference/versions (Angular21 supports Node24 and TypeScript>=5.9<6)
- https://www.ngxs.io/v21.0
- https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide
- https://learn.microsoft.com/en-us/azure/service-bus-messaging/test-locally-with-service-bus-emulator
- https://learn.microsoft.com/en-us/azure/cosmos-db/emulator-linux
- https://github.com/advisories/GHSA-qxg6-89mg-rqv8
- https://api.nuget.org/v3-flatcontainer/azure.functions.sdk/index.json
- https://registry.npmjs.org/@ngxs%2fstore/21.0.0

Functions .NET10 requires isolated worker2.50+ and a supported host. No Linux classic Consumption
deployment is implied. Regional platform/SKU compatibility remains AZ-011/012 verification.

## AZ-002 additions

EF Core SqlServer, Identity EF, Data Protection EF, JwtBearer and EF tool10.0.13; YARP2.3.0;
crypto Pkcs10.0.13; Angular forms21.2.25. Installed real SQL2025 LocalDB17.0.4025.3 is an independent
local execution profile; SQL2022 Docker image pin remains unchanged and is separately NOT VERIFIED.
Microsoft release metadata/SHA-512 verified private SDK10.0.402 with runtime/ASP.NET10.0.13.
MCR SDK10.0.402 and aspnet10.0.13 digests are pinned in Dockerfile; image execution is NOT VERIFIED.
All10projects report no vulnerable NuGet packages at the observed audit; npm audit also reports0.
No advisory suppression or global/source SDK upgrade was used.

References:
https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
https://github.com/advisories/GHSA-2fmv-rv2c-59x7
https://github.com/advisories/GHSA-846f-f764-76mx
https://github.com/advisories/GHSA-r955-8q5j-3h83

The first private SDK invocation automatically created a standard ASP.NET development certificate
in the current-user store. It is not the lab CA, was not trusted/used for these hosts and did not
replace source certificates. Use-Toolchain now disables that first-run behavior for future invocations.