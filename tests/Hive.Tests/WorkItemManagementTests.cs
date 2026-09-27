using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class WorkItemManagementTests
{
    [Fact]
    public async Task ImageSubmission_PersistsWorkItemAttachmentAndActivity()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemSubmission");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);
        Assert.Equal(
            HiveDatabaseSchema.CurrentSchemaVersion,
            migration.Value!.CurrentSchemaVersion);

        var management = CreateFacade(database.Options);
        var context = CreateContext();
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var created = await management.CreateImageWorkItemAsync(
            new WorkItemImageSubmission(
                "invoice.png",
                "image/png",
                content),
            context);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.NotNull(created.Value);
        Assert.Equal(WorkItemStatus.Created, created.Value!.Status);
        Assert.Equal(ResourceVersion.Initial, created.Value.Resource.Version);
        Assert.NotNull(created.Value.Attachment);
        Assert.Equal("invoice.png", created.Value.Attachment!.FileName);
        Assert.Equal(content.LongLength, created.Value.Attachment.ContentLength);

        var stored = await management.GetWorkItemAttachmentAsync(
            created.Value.Id,
            context);

        Assert.True(stored.IsSuccess, stored.Error?.Message);
        Assert.Equal(content, stored.Value!.Content.ToArray());
        Assert.Equal(created.Value!.Attachment!.Sha256, stored.Value!.Metadata.Sha256);

        var activity = await management.GetWorkItemActivityAsync(
            created.Value.Id,
            context);

        Assert.True(activity.IsSuccess, activity.Error?.Message);
        var activities = activity.Value!;
        Assert.Single(activities);
        Assert.Equal("work-item.created", activities[0].EventType);
        Assert.Equal(WorkItemStatus.Created, activities[0].Status);
        Assert.Equal(ResourceVersion.Initial, activities[0].Version);
    }

    [Fact]
    public async Task WorkItemListing_UsesBoundedDeterministicPages()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemPaging");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var management = CreateFacade(database.Options);
        var context = CreateContext();

        var created = new List<WorkItem>();

        for (var index = 0; index < 5; index++)
        {
            var result = await management.CreateImageWorkItemAsync(
                new WorkItemImageSubmission(
                    $"page-{index}.png",
                    "image/png",
                    new byte[] { 1, 2, 3, 4 }),
                context);

            Assert.True(result.IsSuccess, result.Error?.Message);
            created.Add(result.Value!);
        }

        var first = await management.ListWorkItemsPageAsync(
            context,
            pageSize: 2);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(2, first.Value!.Items.Count);
        Assert.True(first.Value.HasNextPage);

        var second = await management.ListWorkItemsPageAsync(
            context,
            pageSize: 2,
            cursor: first.Value.NextCursor);

        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(2, second.Value!.Items.Count);
        Assert.True(second.Value.HasNextPage);

        var third = await management.ListWorkItemsPageAsync(
            context,
            pageSize: 2,
            cursor: second.Value.NextCursor);

        Assert.True(third.IsSuccess, third.Error?.Message);
        Assert.Single(third.Value!.Items);
        Assert.False(third.Value.HasNextPage);

        var ids = first.Value.Items
            .Concat(second.Value.Items)
            .Concat(third.Value.Items)
            .Select(static item => item.Id)
            .ToArray();

        Assert.Equal(created.Count, ids.Distinct().Count());
        Assert.Equal(
            created.Select(static item => item.Id).OrderBy(static id => id.Value).ToArray(),
            ids.OrderBy(static id => id.Value).ToArray());
    }

    [Fact]
    public async Task WorkItemListing_PagedOrderMatchesLegacyOrderWhenTimestampsTie()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemPagingOrder");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var management = CreateFacade(database.Options, clock);
        var context = CreateContext();

        for (var index = 0; index < 5; index++)
        {
            var result = await management.CreateImageWorkItemAsync(
                new WorkItemImageSubmission(
                    $"ordered-{index}.png",
                    "image/png",
                    new byte[] { 1, 2, 3, 4 }),
                context);

            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        var legacy = await management.ListWorkItemsAsync(context);
        Assert.True(legacy.IsSuccess, legacy.Error?.Message);

        var paged = new List<WorkItem>();
        WorkItemListCursor? cursor = null;

        do
        {
            var page = await management.ListWorkItemsPageAsync(
                context,
                pageSize: 2,
                cursor: cursor);

            Assert.True(page.IsSuccess, page.Error?.Message);
            paged.AddRange(page.Value!.Items);
            cursor = page.Value.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(
            legacy.Value!.Select(static item => item.Id).ToArray(),
            paged.Select(static item => item.Id).ToArray());
    }

    [Fact]
    public async Task WorkItemListing_RejectsInvalidPageSize()
    {
        var management = CreateFacade(HiveDatabaseOptions.LocalDevelopment());
        var context = CreateContext();

        var result = await management.ListWorkItemsPageAsync(
            context,
            pageSize: WorkItemListPage.MaxPageSize + 1);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.management.work-item.page-size-invalid",
            result.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, result.Error.Category);
    }

    [Fact]
    public async Task WorkItemActivity_RejectsUndefinedStringStatus()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemActivityInvalidStatus");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var management = CreateFacade(database.Options);
        var context = CreateContext();

        var created = await management.CreateImageWorkItemAsync(
            CreateSubmission(),
            context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        await using (var connection = new Microsoft.Data.SqlClient.SqlConnection(
                         database.Options.ConnectionString))
        {
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE [dbo].[HiveEventLog]
                SET [PayloadJson] = @PayloadJson
                WHERE [EventId] =
                (
                    SELECT TOP (1) [EventId]
                    FROM [dbo].[HiveEventLog]
                    WHERE [StreamKind] = @StreamKind
                      AND [StreamIdentity] = @StreamIdentity
                      AND [EventType] = @EventType
                    ORDER BY [StreamVersion]
                );
                """;
            command.Parameters.AddWithValue("@PayloadJson",
                """{"version":1,"status":"999"}""");
            command.Parameters.AddWithValue(
                "@StreamKind",
                (int)ResourceKind.WorkItem);
            command.Parameters.AddWithValue(
                "@StreamIdentity",
                created.Value!.Id.Value);
            command.Parameters.AddWithValue(
                "@EventType",
                "work-item.created");

            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var activity = await management.GetWorkItemActivityAsync(
            created.Value!.Id,
            context);

        Assert.True(activity.IsFailure);
        Assert.Equal(
            "hive.management.work-item.activity-invalid",
            activity.Error!.Code);
        Assert.Equal(
            ErrorCategory.Validation,
            activity.Error.Category);
        Assert.Equal(
            "A WorkItem activity event contains an invalid status value.",
            activity.Error.Message);
    }

    [Fact]
    public async Task WorkItemActivity_RejectsMalformedReasonValue()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemActivityInvalidReason");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var management = CreateFacade(database.Options);
        var context = CreateContext();

        var created = await management.CreateImageWorkItemAsync(
            CreateSubmission(),
            context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        await using (var connection = new Microsoft.Data.SqlClient.SqlConnection(
                         database.Options.ConnectionString))
        {
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE [dbo].[HiveEventLog]
                SET [PayloadJson] = @PayloadJson
                WHERE [EventId] =
                (
                    SELECT TOP (1) [EventId]
                    FROM [dbo].[HiveEventLog]
                    WHERE [StreamKind] = @StreamKind
                      AND [StreamIdentity] = @StreamIdentity
                      AND [EventType] = @EventType
                    ORDER BY [StreamVersion]
                );
                """;
            command.Parameters.AddWithValue(
                "@PayloadJson",
                """{"version":1,"status":"Created","reason":42}""");
            command.Parameters.AddWithValue(
                "@StreamKind",
                (int)ResourceKind.WorkItem);
            command.Parameters.AddWithValue(
                "@StreamIdentity",
                created.Value!.Id.Value);
            command.Parameters.AddWithValue(
                "@EventType",
                "work-item.created");

            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var activity = await management.GetWorkItemActivityAsync(
            created.Value!.Id,
            context);

        Assert.True(activity.IsFailure);
        Assert.Equal(
            "hive.management.work-item.activity-invalid",
            activity.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, activity.Error.Category);
        Assert.Equal(
            "A WorkItem activity event contains an invalid reason value.",
            activity.Error.Message);
    }

    [Fact]
    public async Task WorkItemApproval_UsesExpectedVersionAndEnforcesState()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemApproval");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var management = CreateFacade(database.Options);
        var context = CreateContext();

        var created = await management.CreateImageWorkItemAsync(
            CreateSubmission(),
            context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var notPending = await management.ApproveWorkItemAsync(
            created.Value!.Id,
            created.Value.Resource.Version,
            context);

        Assert.True(notPending.IsFailure);
        Assert.Equal(ErrorCategory.Conflict, notPending.Error!.Category);

        var requested = await management.RequestWorkItemApprovalAsync(
            created.Value.Id,
            created.Value.Resource.Version,
            context);

        Assert.True(requested.IsSuccess, requested.Error?.Message);
        Assert.Equal(WorkItemStatus.PendingApproval, requested.Value!.Status);
        Assert.Equal(new ResourceVersion(2), requested.Value.Resource.Version);

        var stale = await management.ApproveWorkItemAsync(
            created.Value.Id,
            created.Value.Resource.Version,
            context);

        Assert.True(stale.IsFailure);
        Assert.Equal(ErrorCategory.Concurrency, stale.Error!.Category);

        var approved = await management.ApproveWorkItemAsync(
            created.Value.Id,
            requested.Value.Resource.Version,
            context);

        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.Equal(WorkItemStatus.Completed, approved.Value!.Status);
        Assert.Equal(new ResourceVersion(3), approved.Value.Resource.Version);

        var activity = await management.GetWorkItemActivityAsync(
            created.Value.Id,
            context);

        Assert.True(activity.IsSuccess, activity.Error?.Message);
        Assert.Equal(
            ["work-item.created", "work-item.approval-requested", "work-item.approved"],
            activity.Value!.Select(static item => item.EventType).ToArray());
    }

    [Fact]
    public async Task WorkItemRejection_RequiresReasonAndCurrentPendingVersion()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemRejection");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var management = CreateFacade(database.Options);
        var context = CreateContext();

        var created = await management.CreateImageWorkItemAsync(
            CreateSubmission(),
            context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var missingReason = await management.RejectWorkItemAsync(
            created.Value!.Id,
            created.Value.Resource.Version,
            context,
            " ");

        Assert.True(missingReason.IsFailure);
        Assert.Equal(ErrorCategory.Validation, missingReason.Error!.Category);

        var requested = await management.RequestWorkItemApprovalAsync(
            created.Value.Id,
            created.Value.Resource.Version,
            context);

        Assert.True(requested.IsSuccess, requested.Error?.Message);

        var rejected = await management.RejectWorkItemAsync(
            created.Value.Id,
            requested.Value!.Resource.Version,
            context,
            "Business application data was invalid.");

        Assert.True(rejected.IsSuccess, rejected.Error?.Message);
        Assert.Equal(WorkItemStatus.Rejected, rejected.Value!.Status);

        var activity = await management.GetWorkItemActivityAsync(
            created.Value.Id,
            context);

        Assert.True(activity.IsSuccess, activity.Error?.Message);
        Assert.EndsWith(
            "Business application data was invalid.",
            activity.Value![2].Message);
    }

    [Fact]
    public async Task WorkItemAccess_IsOwnerAndScopeIsolated()
    {
        var database = new PersistenceTestDatabase("Hive_Test_WorkItemAccess");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var management = CreateFacade(database.Options);
        var context = CreateContext();

        var created = await management.CreateImageWorkItemAsync(
            CreateSubmission(),
            context);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var ownerMismatch = await management.GetWorkItemAsync(
            created.Value!.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                context.TenantId,
                PrincipalId.New()));

        Assert.True(ownerMismatch.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, ownerMismatch.Error!.Category);

        var scopeMismatch = await management.GetWorkItemAsync(
            created.Value.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                TenantId.New(),
                context.PrincipalId));

        Assert.True(scopeMismatch.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, scopeMismatch.Error!.Category);

        var list = await management.ListWorkItemsAsync(context);

        Assert.True(list.IsSuccess, list.Error?.Message);
        Assert.Single(list.Value!);
    }

    [Fact]
    public async Task ImageSubmission_RejectsOversizedAndNonImageInput()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkItemImageSubmission(
                "document.txt",
                "text/plain",
                new byte[] { 1 }));

        var oversized = new byte[WorkItemImageSubmission.MaxContentBytes + 1];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkItemImageSubmission(
                "large.png",
                "image/png",
                oversized));
    }

    private static HiveManagementFacade CreateFacade(
        HiveDatabaseOptions options,
        IClock? clock = null) =>
        new(
            new SqlProviderResourceStore(options),
            new SqlAgentDefinitionResourceStore(options),
            new SqlWorkItemResourceStore(options, clock));

    private static ResourceAccessContext CreateContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

    private static WorkItemImageSubmission CreateSubmission() =>
        new(
            "sample.png",
            "image/png",
            new byte[] { 137, 80, 78, 71 });
}
