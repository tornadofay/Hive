CREATE TABLE [dbo].[HiveSecrets]
(
    [SecretId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveSecrets] PRIMARY KEY CLUSTERED,
    [SecretKey] NVARCHAR(100) NOT NULL,
    [DisplayName] NVARCHAR(200) NOT NULL,
    [EncryptedValue] VARBINARY(MAX) NOT NULL,
    [OwnerPrincipalId] UNIQUEIDENTIFIER NOT NULL,
    [ScopeKind] INT NOT NULL,
    [ScopeIdentity] UNIQUEIDENTIFIER NULL,
    [ResourceVersion] BIGINT NOT NULL,
    [CreatedByPrincipalId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAtUtc] DATETIME2(7) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [CausationId] UNIQUEIDENTIFIER NULL,
    [LifecycleStatus] INT NOT NULL,
    [LifecycleChangedAtUtc] DATETIME2(7) NOT NULL,
    [MetadataJson] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [CK_HiveSecrets_SecretKey]
        CHECK (LEN([SecretKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveSecrets_DisplayName]
        CHECK (LEN([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveSecrets_EncryptedValue]
        CHECK (DATALENGTH([EncryptedValue]) > 0),
    CONSTRAINT [CK_HiveSecrets_Scope]
        CHECK
        (
            ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
            OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
        ),
    CONSTRAINT [CK_HiveSecrets_ResourceVersion]
        CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveSecrets_Lifecycle]
        CHECK ([LifecycleStatus] = 0),
    CONSTRAINT [CK_HiveSecrets_MetadataJson]
        CHECK (ISJSON([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveSecrets_SecretKey]
    ON [dbo].[HiveSecrets] ([SecretKey]);

CREATE INDEX [IX_HiveSecrets_OwnerScope]
    ON [dbo].[HiveSecrets] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 3,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
