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
        Assert.Equal(7, migration.Value!.CurrentSchemaVersion);

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
        Assert.Single(activity.Value!);
        Assert.Equal("work-item.created", activity.Value[0].EventType);
        Assert.Equal(WorkItemStatus.Created, activity.Value[0].Status);
        Assert.Equal(ResourceVersion.Initial, activity.Value[0].Version);
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
        HiveDatabaseOptions options) =>
        new(
            new SqlProviderResourceStore(options),
            new SqlAgentDefinitionResourceStore(options),
            new SqlWorkItemResourceStore(options));

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
