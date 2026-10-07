CREATE TABLE [HiveWorkItems]
(
    [WorkItemId] TEXT NOT NULL
        CONSTRAINT [PK_HiveWorkItems] PRIMARY KEY,
    [OwnerPrincipalId] TEXT NOT NULL,
    [ScopeKind] INTEGER NOT NULL,
    [ScopeIdentity] TEXT NULL,
    [ResourceVersion] INTEGER NOT NULL,
    [CreatedByPrincipalId] TEXT NOT NULL,
    [CreatedAtUtc] TEXT NOT NULL,
    [CorrelationId] TEXT NOT NULL,
    [CausationId] TEXT NULL,
    [SourceKind] INTEGER NULL,
    [SourceIdentity] TEXT NULL,
    [LifecycleStatus] INTEGER NOT NULL,
    [LifecycleChangedAtUtc] TEXT NOT NULL,
    [Status] INTEGER NOT NULL,
    [MetadataJson] TEXT NOT NULL,
    [AttachmentFileName] TEXT NULL,
    [AttachmentMediaType] TEXT NULL,
    [AttachmentContentLength] INTEGER NULL,
    [AttachmentSha256] TEXT NULL,
    CONSTRAINT [CK_HiveWorkItems_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveWorkItems_ResourceVersion]
        CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveWorkItems_Lifecycle]
        CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveWorkItems_Status]
        CHECK ([Status] BETWEEN 0 AND 7),
    CONSTRAINT [CK_HiveWorkItems_Source] CHECK
    (
        ([SourceKind] IS NULL AND [SourceIdentity] IS NULL)
        OR ([SourceKind] IS NOT NULL AND [SourceIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveWorkItems_MetadataJson]
        CHECK (json_valid([MetadataJson]) = 1),
    CONSTRAINT [CK_HiveWorkItems_AttachmentMetadata] CHECK
    (
        ([AttachmentFileName] IS NULL
            AND [AttachmentMediaType] IS NULL
            AND [AttachmentContentLength] IS NULL
            AND [AttachmentSha256] IS NULL)
        OR
        ([AttachmentFileName] IS NOT NULL
            AND [AttachmentMediaType] IS NOT NULL
            AND [AttachmentContentLength] IS NOT NULL
            AND [AttachmentContentLength] > 0
            AND [AttachmentSha256] IS NOT NULL)
    )
);

CREATE INDEX [IX_HiveWorkItems_OwnerScope]
    ON [HiveWorkItems] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity], [WorkItemId]);

CREATE INDEX [IX_HiveWorkItems_OwnerStatus]
    ON [HiveWorkItems] ([OwnerPrincipalId], [Status], [WorkItemId]);

CREATE TABLE [HiveWorkItemAttachments]
(
    [WorkItemId] TEXT NOT NULL
        CONSTRAINT [PK_HiveWorkItemAttachments] PRIMARY KEY,
    [FileName] TEXT NOT NULL,
    [MediaType] TEXT NOT NULL,
    [ContentLength] INTEGER NOT NULL,
    [Sha256] TEXT NOT NULL,
    [Content] BLOB NOT NULL,
    [CreatedAtUtc] TEXT NOT NULL,
    CONSTRAINT [FK_HiveWorkItemAttachments_WorkItem]
        FOREIGN KEY ([WorkItemId])
        REFERENCES [HiveWorkItems] ([WorkItemId]),
    CONSTRAINT [CK_HiveWorkItemAttachments_ContentLength]
        CHECK ([ContentLength] > 0 AND length([Content]) = [ContentLength]),
    CONSTRAINT [CK_HiveWorkItemAttachments_Sha256]
        CHECK (length([Sha256]) = 64)
);
