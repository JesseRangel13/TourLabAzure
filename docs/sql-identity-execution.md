# AZ-002 SQL Server, Identity and Angular execution

Work only in C:\Users\JesseRangel\Documents\ChatGPT\TourLab Azure.
The original Tour Microservices repository remains read-only. No remote or cloud deployment is required.

## First setup

Use PowerShell7, Node24.18.0/npm12.0.1 and .NET SDK10.0.402. This patch includes runtime/ASP.NET10.0.13.
SQL Server2025 Express LocalDB17.0.4025.3 is the locally executed profile; a separate named
tourlab-azure instance is used. The pre-existing MSSQLLocalDB instance is untouched.

On a fresh checkout, create private configuration before installing tools:

    ./scripts/New-LocalConfiguration.ps1
    ./scripts/Install-PrivateDotNet.ps1
    . ./scripts/Use-Toolchain.ps1
    dotnet tool restore
    dotnet restore TourLab.Azure.slnx --locked-mode
    dotnet msbuild src/Receipts.Functions/Receipts.Functions.csproj -t:GenerateFunctionsExtensionProject
    dotnet build TourLab.Azure.slnx -c Release --no-restore
    ./scripts/Initialize-SqlIdentity.ps1 -Profile LocalDb
    ./scripts/Verify-Foundation.ps1

Skip New-LocalConfiguration if .local already exists. Skip the private SDK install if SDK10.0.402
is installed globally or under .local/dotnet. No tool installer overwrites or upgrades the original.
The SDK ZIP is verified against Microsoft's release SHA-512 metadata. Use-Toolchain affects only
the current process/session. On Linux install the pinned SDK through official .NET tooling.

Initialize-SqlIdentity creates a new tagged tourlab_azure contained database, five schemas,
separate schema runtime/migration users, fresh signing/quote/Data Protection keys and Alice/Bob/Admin.
It is idempotent; existing user IDs, passwords, tables and history are preserved.
It refuses untagged databases, arbitrary servers, unsafe prefixes and existing secret regeneration.
LocalDB is real SQL Server; it is not SQLite, EF InMemory or a mock.

Open .local/sql/provisioner.json privately to obtain the three synthetic users' generated passwords.
Never copy passwords, connection strings, signing keys, cookie values or that file into Git/chat/logs.
Runtime hosts receive only their own .local/sql/{service}.json. The privileged provisioner configuration
is never loaded by a runtime host. New secret directories have owner-only ACLs/Unix permissions.

## Local SQL alternatives

The LocalDB profile uses a local named pipe with contained password users. Integrated Windows access
belongs only to the one-shot provisioner. Named-pipe local transport is explicitly Development-only.
This verifies SQL permissions/concurrency, not TCP certificate handling or Azure SQL identities.

For Docker SQL Server2022 Developer, explicitly accept its development/test license first:

    ./scripts/Start-Sql.ps1 -AcceptSqlDeveloperLicense
    ./scripts/Initialize-SqlIdentity.ps1 -Profile Docker -OutputDirectory .local/docker-sql -Database tourlab_azure_docker
    ./scripts/Verify-SqlIdentity.ps1 -Profile Docker

Start-Sql launches only the separately named SQL container, not all emulators. The Docker profile
uses encrypted loopback SQL with an explicit Development-only self-signed-certificate exception.
Production rejects local endpoints, optional encryption and TrustServerCertificate.
Actual Docker engine startup/SQL2022 verification was unavailable in this delivery; do not infer it from LocalDB.
Azure SQL principal provisioning is a separate reviewable artifact in config/sql/azure-principals.sql:
contained external identities, no server CREATE LOGIN or local sp_configure assumptions. It was not executed.
Managed-identity connection integration/cloud verification remains AZ-009/012.

## Run the UI

Build-Web packages Angular into Gateway's ignored wwwroot. Run Gateway in one terminal:

    ./scripts/Build-Web.ps1
    ./scripts/Start-Host.ps1 -Service Gateway

Open https://localhost:9443, choose Sign in, and use a generated Alice/Bob/Admin credential.
My account loads current identity from SQL-backed Identity; Sign out uses CSRF and invalidates the
security stamp. Cookies expire after30minutes without sliding extension. Other sessions revalidate
the persisted stamp within1minute; direct API-tool JWTs remain valid until their15minute expiry.
Gateway-issued proxy JWTs last2minutes and are created per request for the selected backend audience.
This is a closed educational issuer, not an OAuth/OIDC server or Entra end-user login.

Trust the newly generated lab CA in your interactive Windows/browser session when needed:

    ./scripts/Trust-LocalCa.ps1

The prior automation session could not complete current-user trust approval. Chromium's independent
trust store remains an explicit UI verification gap. Never set ignoreHTTPSErrors or disable TLS validation.
Backend HTTPS uses explicit Development-only trust of this lab CA and still checks certificate names,
signatures and expiry. Wrong-root TLS rejection was executed successfully.

Run each other service in a separate terminal when using proxied APIs:

    ./scripts/Start-Host.ps1 -Service Catalog
    ./scripts/Start-Host.ps1 -Service Reservations
    ./scripts/Start-Host.ps1 -Service Payments
    ./scripts/Start-Host.ps1 -Service Notifications

A nondefault private configuration directory can be supplied using -ConfigurationDirectory.
All ports remain9443..9447. Only process liveness is anonymous. Readiness and status/Admin status
enforce cookie or backend JWT authorization. No tours/bookings/payment workflow is fabricated yet.
The internal quote route authenticates only Reservations' separate service signer and returns503
until AZ-003 supplies the actual quote workflow. Gateway blocks its external variants.

## Browser contract

GET /auth/csrf creates a readable Secure AZURELAB-XSRF-TOKEN cookie and a distinct HttpOnly
__Host-tourlab-azure-csrf server cookie. Angular sends X-AZURELAB-XSRF-TOKEN on mutations.
The authentication cookie is __Host-tourlab-azure: Secure, HttpOnly, SameSite=Strict, path=/, no Domain.
Cookie names are unique because localhost cookies are shared across ports.

POST /auth/login accepts userName/password; POST /auth/logout requires the authenticated cookie.
Both require valid antiforgery and an allowed Origin when supplied. GET /auth/me returns userId/userName/role
or401. Origin failures/missing tokens return400 with safe code/traceId. Login failure is401;
credential rate limit is10per minute per actual client IP, and lockout is5failures for5minutes.
No forwarded header is trusted to select a user/IP/role. POST /auth/token is separately credential-based
and rate-limited for API tools; it rejects browser Origin/Fetch headers and accepts only a fixed backend name.
The Angular client never requests, stores or exports JWTs.

AuthState owns shared session/loading/errors; typed AuthApi owns HTTP. Credentials remain transient,
are scrubbed after completion and never enter state/localStorage. No NGXS persistence or DevTools is installed.
Logout,401, account change and reset invalidate prior loads and reset feature state through UserSessionChanged.
Protected navigation rechecks the authoritative session; login/logout mutations cannot race each other.

## Executed checks and reproduction

    ./scripts/Verify-SqlIdentity.ps1
    ./scripts/Verify-HttpsBff.ps1 -ConfigurationDirectory .local/sql
    ./scripts/Verify-Browser.ps1 -InstallBrowser

Verify-SqlIdentity creates a uniquely named verification database/configuration, tests real restricted
connections and preserves it afterward. It does not delete data to clean up. The real HTTP tests exercise
Identity against SQL, not an in-memory user store. Host-only tests do not establish relational guarantees.

Verify-HttpsBff refuses occupied lab ports, starts only its own five processes, verifies real CA-validated
forwarding and Admin checks, kills/restarts only its Gateway, reuses an existing protected cookie and
rejects a wrong CA. Its finally block stops only processes it created. Logs/cookies stay private.
The suite was executed and passed; it does not require Windows root-store mutation.
Browser smoke discovers the auth flow but needs actual Chromium trust approval before execution.
Authentication traces/screenshots are disabled. No private authentication artifact is uploaded by CI.

Stop native hosts with Ctrl+C. LocalDB data persists; stop only tourlab-azure after hosts have stopped
if desired. Docker: docker compose --env-file .local/dependencies.env stop sql preserves data.
No database/instance/volume/queue deletion is used as test cleanup.
The workflow's real SQL job requires manual license consent; default CI runs non-relational checks.
No GitHub workflow was dispatched or source repository pushed by this task.

See evidence/AZ-002.md, sql-provider-port.md and parity-matrix.md. Independent AZ-002 review is pending;
after review, AZ-003 implements Catalog models/handlers and UI without weakening inventory invariants.
