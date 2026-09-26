CREATE TABLE [dbo].[HiveWorkItems]
(
    [WorkItemId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveWorkItems] PRIMARY KEY CLUSTERED,
    [OwnerPrincipalId] UNIQUEIDENTIFIER NOT NULL,
    [ScopeKind] INT NOT NULL,
    [ScopeIdentity] UNIQUEIDENTIFIER NULL,
    [ResourceVersion] BIGINT NOT NULL,
    [CreatedByPrincipalId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAtUtc] DATETIME2(7) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [CausationId] UNIQUEIDENTIFIER NULL,
    [SourceKind] INT NULL,
    [SourceIdentity] UNIQUEIDENTIFIER NULL,
    [LifecycleStatus] INT NOT NULL,
    [LifecycleChangedAtUtc] DATETIME2(7) NOT NULL,
    [Status] INT NOT NULL,
    [MetadataJson] NVARCHAR(MAX) NOT NULL,
    [AttachmentFileName] NVARCHAR(260) NULL,
    [AttachmentMediaType] NVARCHAR(200) NULL,
    [AttachmentContentLength] BIGINT NULL,
    [AttachmentSha256] CHAR(64) NULL,
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
        CHECK (ISJSON([MetadataJson]) = 1),
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
    ON [dbo].[HiveWorkItems] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity], [WorkItemId]);

CREATE INDEX [IX_HiveWorkItems_OwnerStatus]
    ON [dbo].[HiveWorkItems] ([OwnerPrincipalId], [Status], [WorkItemId]);

CREATE TABLE [dbo].[HiveWorkItemAttachments]
(
    [WorkItemId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveWorkItemAttachments] PRIMARY KEY CLUSTERED,
    [FileName] NVARCHAR(260) NOT NULL,
    [MediaType] NVARCHAR(200) NOT NULL,
    [ContentLength] BIGINT NOT NULL,
    [Sha256] CHAR(64) NOT NULL,
    [Content] VARBINARY(MAX) NOT NULL,
    [CreatedAtUtc] DATETIME2(7) NOT NULL,
    CONSTRAINT [FK_HiveWorkItemAttachments_WorkItem]
        FOREIGN KEY ([WorkItemId])
        REFERENCES [dbo].[HiveWorkItems] ([WorkItemId]),
    CONSTRAINT [CK_HiveWorkItemAttachments_ContentLength]
        CHECK ([ContentLength] > 0 AND DATALENGTH([Content]) = [ContentLength]),
    CONSTRAINT [CK_HiveWorkItemAttachments_Sha256]
        CHECK (LEN([Sha256]) = 64)
);

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 7,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
