# AZ-010 testing and CI execution

Work only from the independent TourLab Azure root. The original AWS/Blazor source is read-only.
Payments, notification deliveries and confirmation documents are SIMULATED. These commands do
not log in to Azure, provision resources, publish images or deploy applications.

## Local prerequisites and commands

Use SDK10.0.402/runtime10.0.13, Node24.18.0/npm12.0.1, pinned lockfiles and Docker Linux.
Reuse existing private configuration and emulator data; never regenerate .local over existing secrets.
A fresh checkout first runs `./scripts/New-LocalConfiguration.ps1` from PowerShell7.
Start SQL/Service Bus only after explicit license acceptance. Existing acceptance does not authorize
cloud resources. Read the SQL and emulator license links in local-execution.md.

```powershell
. ./scripts/Use-Toolchain.ps1
./scripts/Verify-Foundation.ps1
./scripts/Start-Sql.ps1 -AcceptSqlDeveloperLicense
./scripts/Start-Broker.ps1 -AcceptSqlDeveloperLicense -AcceptServiceBusLicense
./scripts/Start-Images.ps1
./scripts/Start-Cosmos.ps1
./scripts/Wait-Dependencies.ps1
./scripts/Trust-LocalCa.ps1
./scripts/Verify-Integration.ps1 -Profile Docker -IsolatedBroker -AcceptSqlDeveloperLicense -AcceptServiceBusLicense
./scripts/Verify-FullStack.ps1 -Profile Docker -IsolatedBroker -AcceptSqlDeveloperLicense -AcceptServiceBusLicense
./scripts/Write-TestSummary.ps1
```

Do not run Start-Broker against an existing broker: it deliberately refuses automatic recreation.
Reuse its current listener and health; this preserves queued data. Start-Sql is scoped to the new lab.
LocalDB is also supported by selecting `-Profile LocalDb` on the two verification scripts.
Verify-Foundation was executed in Windows PowerShell5.1. Full-stack ran in PowerShell7.6.5;
the retained5.1 process-shutdown/platform branches are not claimed as an executed full-stack5.1 run.
Fresh certificate generation and Linux CI use PowerShell7. Trust-LocalCa uses the current user's Root
store and Windows may require clicking the generated CA security prompt. It needs no machine-wide
trust. Never add ignoreHTTPSErrors, insecure Chromium flags or a permissive TLS callback.

Linux CI explicitly installs the run's CA in OS trust and Chromium NSS using certutil. For local Linux,
follow those same reviewable steps in .github/workflows/ci.yml; trust only the public ca.crt generated
by this checkout. NODE_EXTRA_CA_CERTS supplies Node's separate HTTPS trust for APIRequestContext.
The installed Playwright1.64.1 package chooses its matching Chromium revision under .local/playwright.
Functions4.16.0 archives are pinned to verified platform-specific SHA256 values before extraction.

## What the scripts establish

Verify-Integration uses one fresh physical SQL/storage regression profile with messaging disabled,
then ANOTHER broker profile with actual transport enabled. Explicit class filters exclude staged/live
host tests without matching Redelivery by accident. Both categories fail on skips/failures. The
protection rollover fixture honors the selected real SQL profile and retains all assertions.
BrokerOnly is an explicit diagnostic category selection; default CI runs both categories. Do not call
BrokerOnly a complete SQL/storage regression pass.

Verify-FullStack uses a third fresh physical database/profile. It checks ports9443..9447/9450 before
starting anything and refuses to stop pre-existing listeners. It starts five HTTPS APIs, actual durable
workers and the actual isolated Functions listener, then checks strict-CA HTTPS liveness. Readiness
remains authenticated: the browser verifies all five ready responses through a real session and401
on anonymous backend readiness. Windows curl uses the existing absent-CRL best-effort option;
certificate chain, name and expiry validation remain enabled. Chromium never ignores HTTPS errors.
The helper records owned child PIDs and stops only those trees in finally. Existing normal containers,
databases, queues, blobs and documents are retained.
There are no mocked browser API routes, direct browser SQL/broker access, fixed eventual-consistency
sleeps or automatic test retries. Web-first assertions and bounded polling wait for durable workflows.
One worker prevents shared fault controls from racing other tests. A private timestamp-only ledger
survives worker recycling; it stores no credentials, cookies, user IDs or authentication state and
paces real sign-in/sign-out requests below the unchanged ten-per-minute limit; it never retries429.
Browser contexts remain independent, including the concurrent Alice/Bob test. Test data uses GUIDs.

The receipt test reads actual downloaded bytes, verifies the simulated label/accepted total, compares
identical bytes after cancellation, checks Bob's content404 and propagates a known W3C trace ID.
The runner requires that ID in the actual Functions log before writing completed.json.
A passing Playwright JUnit file alone cannot establish that host trace assertion.

## Evidence and privacy

Per-run .local/integration-*/results contains private TRX. .local/e2e-* contains private process logs,
profile, child PIDs, completed marker and browser-report/{html,junit.xml}. Authenticated traces,
screenshots, videos and storageState files are disabled. Assertion reports may still contain private
DOM/response values; keep them local and access-controlled. Never attach their raw contents to CI.
Playwright reports are described in the [official reporter documentation](https://playwright.dev/docs/test-reporters).
[Trace data can expose credentials](https://github.com/microsoft/playwright/blob/main/docs/src/ci-intro.md).

Write-TestSummary exports only a fixed schema of integer test totals, category names and a Functions
trace boolean. It reads neither assertions nor log text into the output. CI uploads only ci-summary.json
for three days. Missing suites or incomplete host verification produce complete:false. A fresh CI
runner has exactly two integration categories and one browser run; local retained multiple runs are not silently
called one complete CI run. Build/test errors fail the job even when a diagnostic summary is emitted.

## CI boundaries

Pull requests/main execute the foundation job: locked .NET/Angular tests, audit, SPA, five separate
application image builds and isolated Functions packaging. It runs no licensed emulators or cloud CD.
workflow_dispatch requires BOTH license booleans to run full-stack after foundation. It uses a unique
Compose project per run/attempt and actual SQL, Service Bus, Azurite, Cosmos and Functions/Chromium.
It stops only that project's containers without removing volumes. No raw secret-bearing artifacts
are uploaded. Actions, SDKs, packages and dependency images are pinned; no push permission or OIDC
cloud credential is requested. Hosted runner memory/engine compatibility still needs actual remote
execution evidence; actionlint is syntax validation, not a successful remote job.

Current executed results, observations and environmental limits are in evidence/AZ-010.md.
Actual Azure identity/RBAC/quotas/hosting remain separate AZ-012 proof. IaC is AZ-011 only.

## Retained broker isolation

On a reused workstation prefer the IsolatedBroker flags shown above. Normal5673/5301 resources are
never restarted or recreated. The helper creates tourlab-azure-az010-<run> with fresh SQL credentials,
a private network/volume and loopback5674/5302. It explicitly accepts both licenses only after the
caller supplies both switches. It fails on occupied ports, emulator exit or startup deadline and stops
only its own project while retaining volumes. No queue purge, broad receive-and-complete or limit
increase is used to clear a backlog. LocalTestBroker and ReceiptTestBroker are explicit Development
settings for this separate fixture; cloud bootstrap rejects them. The normal5673 default remains.

Both verification helpers can use an already healthy normal broker when IsolatedBroker is omitted.
A bounded unique-ID selector holds at most64 unrelated messages for a test and abandons them;
if the retained backlog is larger, fail and use the isolated profile. Do not repeatedly run probes on a
busy shared namespace: PeekLocks and redelivery counters are real broker activity. Local test fixture
proof does not establish production throughput, emulator durability across restart or Azure IAM.


On a workstation with retained failed attempts, select the actual three result files explicitly:

```powershell
./scripts/Write-TestSummary.ps1 -SqlReport .local/integration-<sql-run>/results/sql-storage.trx -BrokerReport .local/integration-<broker-run>/results/broker.trx -BrowserReport .local/e2e-<browser-run>/browser-report/junit.xml
```

All selected paths must remain under this checkout's .local. The summary requires all three category
minimums, zero failed/skipped cases, and the actual Functions trace completion marker. Default CI
uses discovery on a fresh runner; retained multiple local attempts deliberately do not become one
complete run unless the operator explicitly selects the verified reports. No failed report is deleted.

The artifact action explicitly allows the hidden path for the EXACT single .local/ci-summary.json
file; it never uploads the directory or a glob. Hidden paths are otherwise excluded by default in
[upload-artifact4.6.2](https://github.com/actions/upload-artifact/tree/v4.6.2#uploading-hidden-files).
Only the fixed count schema is permitted in that file. Remote upload execution remains NOT VERIFIED.
