# BulkDeleteParallel
# Dataverse Bulk Delete (Parallel)

A .NET console tool for bulk-deleting Microsoft Dataverse records in parallel. Built for cleanup jobs where the built-in Bulk Delete job or a single-threaded loop is too slow — reads matching records via FetchXML paging and deletes them concurrently across multiple worker connections using `ExecuteMultiple`.

## How it works

- **RecordReader** pages through `FilterXml` results and streams record IDs into a bounded channel as it goes — it does not wait for the entire result set before deleting starts.
- **BulkDeleteService** runs the reader as a producer and `WorkerCount` consumers in parallel off that channel, so paging and deleting overlap.
- **DeleteWorker** batches IDs into `ExecuteMultiple` requests (`ContinueOnError = true`) and reports each record's outcome to the console as `Deleted`, `Failed`, or `AlreadyGone`.
- **AlreadyGone** specifically means a retried batch found the record already deleted by a prior attempt that appeared to fail (e.g. a client-side timeout after the server had already committed). It's counted separately from real failures.
- **RetryHelper** retries whole `ExecuteMultiple` calls on transient faults (timeouts, 429s, throttling, gateway errors) with exponential backoff.
- **DataverseClientFactory** authenticates once and hands out `ServiceClient.Clone()` connections to workers, avoiding a full OAuth handshake per worker.
- Real failures (not `AlreadyGone`) are written to `failed-ids-{timestamp}.txt` at the end so they can be inspected or retried.

## Requirements

- .NET 8 SDK or later
- `Microsoft.PowerPlatform.Dataverse.Client`
- A Dataverse application user (recommended) or user account with delete privilege on the target entity

## Configuration

Create `appsettings.json` next to the executable (do **not** commit this file — see [Secrets](#secrets)):

```json
{
  "DeleteConfiguration": {
    "ConnectionString": "AuthType=ClientSecret;Url=https://yourorg.crm.dynamics.com;ClientId=<app-id>;ClientSecret=<secret>",
    "EntityLogicalName": "cdi_emailevent",
    "FilterXml": "<filter><condition attribute='createdon' operator='olderthan-x-months' value='24' /></filter>",
    "FetchPageSize": 5000,
    "BatchSize": 200,
    "WorkerCount": 8,
    "QueueSize": 20,
    "RequestTimeoutMinutes": 30,
    "RetryCount": 5,
    "BypassSyncPlugins": false
  }
}
```

| Setting | Meaning |
|---|---|
| `ConnectionString` | Standard Dataverse `ServiceClient` connection string. |
| `EntityLogicalName` | Logical name of the entity to delete from. |
| `FilterXml` | Raw `<filter>` FetchXML injected into the entity's `<fetch>` — this is what scopes the delete. Test it with Advanced Find or a plain `RetrieveMultiple` before pointing this tool at it. |
| `FetchPageSize` | Records per FetchXML page when reading matching IDs. |
| `BatchSize` | Records per `ExecuteMultiple` delete request. Lower this if you're seeing timeouts on large batches. |
| `WorkerCount` | Number of concurrent delete connections. |
| `QueueSize` | How many unconsumed batches can sit in the channel between the reader and the workers before the reader blocks. |
| `RequestTimeoutMinutes` | Applied to `ServiceClient.MaxConnectionTimeout` before any connection is opened. |
| `RetryCount` | Max retries per batch on transient faults. |
| `BypassSyncPlugins` | Sets `BypassCustomPluginExecution` on each delete. Requires `prvBypassCustomPlugin`; understand what synchronous logic you're skipping before enabling this. |

## Usage

```bash
dotnet run
```

Console output is one line per record (`Id=... Status=Deleted|Failed|AlreadyGone`) plus a summary at the end. Press `Ctrl+C` to cancel — in-flight batches finish before the process exits.

## Safety

This tool has **no dry-run mode and no confirmation prompt**. It starts deleting as soon as it connects. Before running against a real environment:

- Validate `FilterXml` against a read-only query first and check the record count.
- Run against a sandbox/UAT environment before production.
- Be aware of Dataverse Service Protection Limits — high `WorkerCount` × `BatchSize` combinations can trip throttling under sustained load.
- `AlreadyGone` results are expected background noise from retries, not something to investigate; genuine failures land in `failed-ids-*.txt`.

## Secrets

`appsettings.json` contains a live connection string. Add it to `.gitignore` and commit an `appsettings.example.json` with placeholder values instead.

## License

Add a license before making the repo public — MIT is a reasonable default if you don't have another preference.
