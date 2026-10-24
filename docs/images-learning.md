# Blob images — design and learning (AZ-006)

Catalog owns SQL metadata and a private Blob container; SQL never stores image bytes/base64/URLs.
The typed Angular client uses ordinary authenticated same-origin proxy URLs. Neither credentials nor
expiring SAS URLs reach NGXS/browser. Decoded MIME is derived from bytes rather than a file name/header.
Tour visibility is checked before listing/downloading; inactive tours are hidden from tourists.
Deletion status is rechecked after the bounded download spool. A deletion already accepted hides reads.

## Preserved source behavior and deliberate provider changes

Reviewed read-only source: Catalog Images/{ValidatedImage,TourImages,ImageStorage,ImageEndpoints}.cs,
tests/Lab.Tests/{ImageExtensionTests,ImageFixtures}.cs and baseline contracts/workflows.
Only the validated JPEG/PNG parser and synthetic JPEG fixture were selectively ported, with namespaces
and fresh temporary-file prefix changed. No source credentials, migrations, S3 SDK/config, generated
output, Blazor or .git were copied. Existing Catalog domain/security/SQL conventions were reused.

The original's maximum2MiB, maximum3 non-deleted images/tour and2048x2048 decode bounds are preserved.
Azure spec05 suggests5MiB/five as examples; increasing source bounds would weaken an existing invariant.
Two concurrent upload decodes,40s total upload budget, private temporary spool removed on dispose,
PNG signature/chunk/CRC/animation rejection, JPEG signature/full decode/ending, bounded chunk count,
and cancellation remain. Eight concurrent authorized content responses; no request queue.
The download checks byte size and SHA256 while spooling at most2MiB before returning bytes.

BlobImageStorage is a singleton actual Azure.Storage.Blobs12.30.1 SDK adapter. If-None-Match:* upload
never overwrites an existing object; duplicate keys require equal size/hash/MIME metadata.
HEAD after PUT verifies object success before SQL Ready. SDK network retry count0, timeout15s;
durable SQL intent/lease owns retries. Runtime has no container-create/policy or broad list/purge path.
Local provisioning verifies PublicAccessType.None and refuses public existing containers.
Cloud configuration uses HTTPS and managed identity, fixed owned container and tourlabazure account
prefix; it rejects connection strings, SAS/query credentials, foreign hosts and production emulators.
Actual container-scoped RBAC/account public-access denial remains AZ-009/011/012 verification.

Azure.Core1.62 consolidates credential types. Pin Azure.Identity1.21 forwarding facade to override
SqlClient's older transitive Identity package; the initial older-package collision was corrected.
References: [official credential consolidation guide](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/MigrationGuide.md),
[conditional Blob upload API](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs.blobclient.uploadasync),
[StbImageSharp source](https://github.com/StbSharp/StbImageSharp).
StbImageSharp2.30.16 and all .NET transitive versions are locked.

## Cross-system boundary

1. Validate bytes before creating durable work. SQL transaction takes schema-principal application
   locks in tour-then-image order, validates count/content/idempotency and persists Pending, generated
   random object key, immutable hash/size/MIME, owner lease2minutes and UploadClaimed history.
2. Commit SQL before PUT/HEAD. No SQL transaction remains open during Blob I/O.
3. SQL Ready update requires the same lease owner, unexpired lease and Pending status. An old upload
   cannot finish after deletion or another claimant. CompletionSuperseded records failed finalization.
4. DELETE preserves the in-flight upload lease, increments version and commits DeletePending/history.
   Recovery waits for lease expiry, deletes that owned key idempotently and marks Deleted. Late
   completion never publishes Ready.
5. Recovery claims one due Pending/DeletePending/Deleted through a short SQL transaction with
   UPDLOCK/READPAST/READCOMMITTEDLOCK. This also works with read-committed snapshot configuration.
   Version checks/leases protect competing processes. Network calls happen after commit.
6. Pending with verified matching object becomes Ready; absent work older15minutes or mismatched
   metadata becomes DeletePending. Young absent Pending backs off15seconds. Deletion failure retains
   intent/history and lease, retrying after expiry. Deleted tombstones are retained and swept hourly
   indefinitely, so a very late uncertain PUT is removed without metadata resurrection.
   Completion checks owner/status/expiry transactionally. History and attempt counters are durable.
7. Worker creates an async scope/context per item, at most10 items/pass,15s between passes and25s
   per recovery I/O budget. It touches known SQL-owned keys only; unrelated objects are never scanned
   or purged. Ready metadata is authoritative publication; a missing/corrupt Blob yields unavailable/
   placeholder, never a fabricated download. External object corruption is not silently repaired.

There is no atomic SQL+Blob transaction and no exactly-once claim. Temporary late objects may exist
until the next retained tombstone sweep; they remain private and never reappear as Ready.
Test-only fault wrappers and afterStorage barriers exercise actual Blob I/O with real SQL. They are
not unauthenticated crash HTTP endpoints. Restart evidence stages durable success-before-SQL fixtures
while the helper's Catalog is stopped, then proves automatic recovery by the restarted hosted worker.

## Angular/NGXS

ImagesApi is typed transport; ImagesState owns shared data, loading/error/mutation lifecycle.
LoadImages cancels older requests; generation/request/tour guards ignore old account/navigation
responses. UserSessionChanged clears state. Upload uses the same intention ID for retries of the same
selected file, no automatic retry; account/tour changes clear the transient selection. The server
always validates regardless of client checks. Returned proxy URLs are ordinary paths, never auth.
TourImagesPanel renders Ready content, broken-download/pending placeholders, Admin upload/delete/
one-item recovery controls and explicit reload. No private state or files are persisted in NGXS storage.

## Recovery practice

Use deterministic tests rather than container destruction: after Blob success throw before SQL
completion, resume with a new context, race two claims, delay PUT across deletion, fail DELETE then
retry, inject a late object for a Deleted tombstone, corrupt metadata/content and retain unrelated
markers. Each fixture uses unique tour/image IDs and retains its SQL/object evidence.
Never delete either project's databases, queues, container, volume or test history to get a green run.
