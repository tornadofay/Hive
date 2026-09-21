# Phase 0.4 — Persistence bootstrap example

This example uses the public Hive.Persistence API to create Hive's SQL Server database when necessary and run the embedded DbUp migrations.

```csharp
using Hive.Persistence;

var options = new HiveDatabaseOptions(
    "Server=localhost\\MSSQLSERVER01;Database=Hive;Trusted_Connection=True;");

var migrator = new HiveDatabaseMigrator(options);
var result = await migrator.MigrateAsync();

if (result.IsFailure)
{
    Console.Error.WriteLine(result.Error?.Message);
    return;
}

var outcome = result.Value!;

Console.WriteLine(
    $"Migration status: {outcome.Status}; " +
    $"schema: {outcome.CurrentSchemaVersion}; " +
    $"scripts applied: {outcome.AppliedMigrationCount}");
```

Expected first-run result:

```text
Migration status: Applied; schema: 1; scripts applied: 1
```

Expected subsequent-run result:

```text
Migration status: AlreadyCurrent; schema: 1; scripts applied: 0
```

For local development without an explicit server connection string, use:

```text
(localdb)\MSSQLLocalDB
```

Hive creates the target database automatically by default before applying migrations. Set `createDatabaseIfMissing: false` only when the database must already exist.

Connection strings are runtime configuration and must not be committed to source control or written to diagnostics.

A database whose recorded Hive schema version is newer than the code-supported version is rejected before normal migration proceeds.