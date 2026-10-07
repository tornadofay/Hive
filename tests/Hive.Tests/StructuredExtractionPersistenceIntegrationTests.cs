using Hive.Core;
using Hive.Coordination;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class StructuredExtractionPersistenceIntegrationTests
{
    [Fact]
    public async Task BatchState_SurvivesStoreRecreationAndOptimisticConcurrency()
    {
        var database = new PersistenceTestDatabase("Hive_Test_Phase117_Batch");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var now = new DateTimeOffset(
            2026,
            10,
            7,
            0,
            0,
            0,
            TimeSpan.Zero);

        var schema = new StructuredTargetSchema(
        [
            new StructuredTargetField(
                new SemanticFieldId("invoice.number"),
                "Invoice Number",
                StructuredValueType.String,
                required: true)
        ]);

        var candidate = new StructuredCandidate(
            StructuredCandidateId.New(),
            new StructuredCandidateProvenance(
                Guid.NewGuid(),
                0,
                "invoice.xlsx",
                InputSourceKind.Spreadsheet,
                "Orders",
                2,
                null,
                "mapping-1"),
            [
                new StructuredCandidateField(
                    new SemanticFieldId("invoice.number"),
                    StructuredValueType.String,
                    "INV-1",
                    StructuredValidationState.Valid)
            ]);

        var batch = new StructuredExtractionBatch(
            new ResourceEnvelope<StructuredExtractionBatchId>(
                ResourceKind.StructuredExtractionBatch,
                StructuredExtractionBatchId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now),
                new Dictionary<string, string>
                {
                    ["hive.structured-extraction.mapping-target-id"] =
                        ExecutionTargetId.New().Value.ToString("D")
                }),
            Guid.NewGuid(),
            schema,
            [
                new StructuredExtractionItemResult(
                    0,
                    "invoice.xlsx",
                    InputSourceKind.Spreadsheet,
                    StructuredExtractionItemStatus.Accepted,
                    candidate,
                    worksheetName: "Orders",
                    rowNumber: 2,
                    sourceFingerprint: "source-1",
                    mappingContextIdentity: "mapping-1",
                    sourceValues: new Dictionary<string, string>
                    {
                        ["Invoice Number"] = "INV-1"
                    })
            ],
            [
                new SpreadsheetMapping(
                    new SpreadsheetMappingContext(
                        "mapping-1",
                        "invoice.xlsx",
                        "Orders",
                        ["Invoice Number"],
                        [
                            new Dictionary<string, string>
                            {
                                ["Invoice Number"] = "INV-1"
                            }
                        ],
                        "source-1",
                        StructuredExtractionEngine.ComputeTargetSchemaFingerprint(schema)),
                    [
                        new SpreadsheetMappingEntry(
                            "Invoice Number",
                            new SemanticFieldId("invoice.number"))
                    ],
                    SpreadsheetMappingReviewState.Accepted)
            ],
            [0],
            StructuredExtractionBatchStatus.Accepted);

        var store = new SqlStructuredExtractionBatchStore(
            HiveEventPersistence.CreateSql(database.Options));

        var created = await store.CreateAsync(batch, context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var reloaded = new SqlStructuredExtractionBatchStore(
            HiveEventPersistence.CreateSql(database.Options));

        var loaded = await reloaded.GetAsync(batch.Id, context);

        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal(batch.Id, loaded.Value!.Id);
        Assert.Equal(StructuredExtractionBatchStatus.Accepted, loaded.Value.Status);
        Assert.Single(loaded.Value.AcceptedItemIndexes);
        Assert.True(
            loaded.Value.Items.Single().Candidate!.IsValid);
        Assert.Single(loaded.Value.Mappings);
        Assert.Equal(
            SpreadsheetMappingReviewState.Accepted,
            loaded.Value.Mappings.Single().ReviewState);

        var changed = loaded.Value.With(
            status: StructuredExtractionBatchStatus.ReviewRequired,
            changedAtUtc: now.AddMinutes(1));

        var updated = await reloaded.UpdateAsync(
            changed,
            batch.Resource.Version,
            context);

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(2, updated.Value!.Resource.Version.Value);

        var stale = await reloaded.UpdateAsync(
            loaded.Value.With(
                status: StructuredExtractionBatchStatus.ReviewRequired,
                changedAtUtc: now.AddMinutes(2)),
            loaded.Value.Resource.Version,
            context);

        Assert.True(stale.IsFailure);
        Assert.Equal(
            ErrorCategory.Concurrency,
            stale.Error!.Category);

        var afterRestart = await new SqlStructuredExtractionBatchStore(
            HiveEventPersistence.CreateSql(database.Options))
            .GetAsync(batch.Id, context);

        Assert.True(afterRestart.IsSuccess, afterRestart.Error?.Message);
        Assert.Equal(
            StructuredExtractionBatchStatus.ReviewRequired,
            afterRestart.Value!.Status);
        Assert.Equal(
            2,
            afterRestart.Value.Resource.Version.Value);
    }
}
