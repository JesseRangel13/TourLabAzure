# Current task

AZ-002 — **Verified** (2026-10-23).

Antigravity verdict: PASS in docs/reviews/AZ-002.md (re-review complete).
Both requested script findings were fixed and independently verified.

Review fixes: Windows PowerShell 5.1-compatible private SDK platform selection; current-shell
HTTPS host launching; necessary 5.1 process-tree shutdown and startup-readiness compatibility.
The unchanged foundation security audit also required a scoped undici 7.30.1 build-tool override;
Angular remains 21.2.x. No security invariant, test assertion or isolation boundary was weakened.

Executed in Windows PowerShell 5.1.26100.9444 with SDK 10.0.402:
foundation passed (Release build 0 warnings/errors, 18 .NET tests, 12 Angular tests,
0 npm vulnerabilities, production build, Compose config);
real SQL/Identity 32 passed / 2 intentional live-suite skips;
separate live HTTPS roundtrip and restart/TLS tests both passed.
Live tests used -ConfigurationDirectory .local/verify-c2a320b8c49a4db4a45c15df9e95390b.
All HTTPS listeners were released after cleanup.

Limitation: the pre-existing default .local/sql profile fails authentication and its idempotent
setup reports SQL error 5170 (existing database-file collision). Existing files/data were preserved.
The no-argument live check is not claimed passed. Chromium auth UI/CA trust, Docker SQL/container
execution and Azure SQL/managed identity/cloud deployment remain NOT VERIFIED.
Functions host remains a later-task prerequisite.

Original source commit/status/all 416 tracked SHA-256 fingerprints are unchanged, confirmed
by docs/evidence/AZ-002-fixes-source-before.json and AZ-002-fixes-source-after.json.
No source mutation, data deletion, push or cloud operation occurred.

Exact commands, fixes, results and limitations: docs/evidence/AZ-002.md.
Execution guide: docs/sql-identity-execution.md.
Next: independent AZ-002 re-review; resolve the default lab database-file collision separately.
Then AZ-003 Catalog/quotes/inventory and Angular controls after the review gate.
