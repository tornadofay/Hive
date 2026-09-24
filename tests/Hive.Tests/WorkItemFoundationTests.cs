using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class WorkItemFoundationTests
{
    [Fact]
    public void WorkItemAttachmentContent_RejectsHashMismatchAndAcceptsMatchingContent()
    {
        var metadata = new WorkItemAttachmentMetadata(
            "sample.png",
            "image/png",
            3,
            "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81");

        var content = new byte[] { 1, 2, 3 };

        var valid = new WorkItemAttachmentContent(metadata, content);

        Assert.Equal(content, valid.Content.ToArray());

        Assert.Throws<ArgumentException>(
            () => new WorkItemAttachmentContent(
                metadata,
                new byte[] { 4, 5, 6 }));
    }

    [Fact]
    public void WorkItemImageContracts_RejectMediaTypesBeyondPersistenceLimit()
    {
        var maxLengthMediaType = "image/" + new string('x', 194);
        Assert.Equal(200, maxLengthMediaType.Length);

        var metadata = new WorkItemAttachmentMetadata(
            "sample.png",
            maxLengthMediaType,
            1,
            "4bf5122f344554c53bde2ebb8cd2c7b3c0a7f7a7a0f1b2c3d4e5f60718293a4b5");

        Assert.Equal(maxLengthMediaType, metadata.MediaType);

        var submission = new WorkItemImageSubmission(
            "sample.png",
            maxLengthMediaType,
            new byte[] { 1 });

        Assert.Equal(maxLengthMediaType, submission.MediaType);

        var oversized = "image/" + new string('x', 195);

        Assert.Throws<ArgumentException>(
            () => new WorkItemAttachmentMetadata(
                "sample.png",
                oversized,
                1,
                "4bf5122f344554c53bde2ebb8cd2c7b3c0a7f7a7a0f1b2c3d4e5f60718293a4b5"));

        Assert.Throws<ArgumentException>(
            () => new WorkItemImageSubmission(
                "sample.png",
                oversized,
                new byte[] { 1 }));
    }

    [Fact]
    public void WorkItem_CreateEstablishesIndependentIdentityScopeAndProvenance()
    {
        var createdAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var correlation = CorrelationId.New();

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Tenant(tenant),
            new ResourceProvenance(principal, createdAt, correlation),
            createdAt);

        Assert.Equal(ResourceKind.WorkItem, workItem.Resource.Kind);
        Assert.Equal(WorkItemStatus.Created, workItem.Status);
        Assert.False(workItem.IsTerminal);
        Assert.Equal(ResourceVersion.Initial, workItem.Resource.Version);
        Assert.Equal(principal, workItem.Resource.Owner);
        Assert.Equal(ResourceScope.Tenant(tenant), workItem.Resource.Scope);
        Assert.Equal(principal, workItem.Resource.Provenance.CreatedBy);
        Assert.Equal(correlation, workItem.Resource.Provenance.CorrelationId);
    }

    [Fact]
    public void WorkItem_StatusTransitionPreservesIdentityProvenanceAndScope()
    {
        var createdAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Tenant(tenant),
            new ResourceProvenance(principal, createdAt, CorrelationId.New()),
            createdAt);

        var updatedAt = createdAt.AddMinutes(5);
        var next = workItem.TransitionTo(WorkItemStatus.Queued, updatedAt);

        Assert.Equal(workItem.Id, next.Id);
        Assert.Equal(workItem.Resource.Identity, next.Resource.Identity);
        Assert.Equal(workItem.Resource.Scope, next.Resource.Scope);
        Assert.Equal(workItem.Resource.Owner, next.Resource.Owner);
        Assert.Equal(workItem.Resource.Provenance, next.Resource.Provenance);
        Assert.Equal(new ResourceVersion(2), next.Resource.Version);
        Assert.Equal(updatedAt, next.Resource.Lifecycle.ChangedAtUtc);
        Assert.Equal(WorkItemStatus.Created, workItem.Status);
        Assert.Equal(WorkItemStatus.Queued, next.Status);
    }

    [Fact]
    public void WorkItem_SuspendAndResumePreserveWorkStateWhileVersioningResource()
    {
        var now = DateTimeOffset.UtcNow;
        var principal = PrincipalId.New();

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Global(),
            new ResourceProvenance(principal, now, CorrelationId.New()),
            now)
            .TransitionTo(WorkItemStatus.Running, now.AddMinutes(1));

        var suspended = workItem.Suspend(now.AddMinutes(2));
        var resumed = suspended.Resume(now.AddMinutes(3));

        Assert.Equal(WorkItemStatus.Running, suspended.Status);
        Assert.Equal(ResourceLifecycleStatus.Suspended, suspended.Resource.Lifecycle.Status);
        Assert.Equal(new ResourceVersion(3), suspended.Resource.Version);

        Assert.Equal(WorkItemStatus.Running, resumed.Status);
        Assert.Equal(ResourceLifecycleStatus.Active, resumed.Resource.Lifecycle.Status);
        Assert.Equal(new ResourceVersion(4), resumed.Resource.Version);
        Assert.Equal(now.AddMinutes(3), resumed.Resource.Lifecycle.ChangedAtUtc);
    }

    [Fact]
    public void WorkItem_TerminalStateCannotBeChanged()
    {
        var now = DateTimeOffset.UtcNow;
        var principal = PrincipalId.New();

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Global(),
            new ResourceProvenance(principal, now, CorrelationId.New()),
            now)
            .TransitionTo(WorkItemStatus.Completed, now.AddMinutes(1));

        Assert.True(workItem.IsTerminal);

        Assert.Throws<InvalidOperationException>(() =>
            workItem.TransitionTo(WorkItemStatus.Failed, now.AddMinutes(2)));
    }

    [Fact]
    public void WorkItem_RetiredResourceCannotChangeWorkStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var principal = PrincipalId.New();

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Global(),
            new ResourceProvenance(principal, now, CorrelationId.New()),
            now)
            .Retire(now.AddMinutes(1));

        Assert.Equal(ResourceLifecycleStatus.Retired, workItem.Resource.Lifecycle.Status);

        Assert.Throws<InvalidOperationException>(() =>
            workItem.TransitionTo(WorkItemStatus.Queued, now.AddMinutes(2)));
    }

    [Fact]
    public void SeparateWorkItems_DoNotShareMutableProvenanceOrStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var principal = PrincipalId.New();

        var first = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Global(),
            new ResourceProvenance(principal, now, CorrelationId.New()),
            now);

        var second = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Global(),
            new ResourceProvenance(principal, now, CorrelationId.New()),
            now);

        var firstUpdated = first.TransitionTo(WorkItemStatus.Queued, now.AddMinutes(1));

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Resource.Provenance.CorrelationId, second.Resource.Provenance.CorrelationId);
        Assert.Equal(WorkItemStatus.Created, second.Status);
        Assert.Equal(ResourceVersion.Initial, second.Resource.Version);
        Assert.Equal(ResourceVersion.Initial, first.Resource.Version);
        Assert.Equal(new ResourceVersion(2), firstUpdated.Resource.Version);
    }
}
