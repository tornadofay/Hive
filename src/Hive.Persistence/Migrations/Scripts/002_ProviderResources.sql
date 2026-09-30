CREATE TABLE [dbo].[HiveProviders]
(
    [ProviderId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveProviders] PRIMARY KEY CLUSTERED,
    [ProviderKey] NVARCHAR(100) NOT NULL,
    [DisplayName] NVARCHAR(200) NOT NULL,
    [TransportKind] NVARCHAR(100) NOT NULL,
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
    CONSTRAINT [CK_HiveProviders_ProviderKey] CHECK (LEN([ProviderKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveProviders_DisplayName] CHECK (LEN([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveProviders_TransportKind] CHECK (LEN([TransportKind]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveProviders_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveProviders_ResourceVersion] CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveProviders_Lifecycle] CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveProviders_MetadataJson] CHECK (ISJSON([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveProviders_ProviderKey]
    ON [dbo].[HiveProviders] ([ProviderKey]);

CREATE INDEX [IX_HiveProviders_OwnerScope]
    ON [dbo].[HiveProviders] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);

CREATE TABLE [dbo].[HiveProviderAccounts]
(
    [ProviderAccountId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveProviderAccounts] PRIMARY KEY CLUSTERED,
    [ProviderId] UNIQUEIDENTIFIER NOT NULL,
    [AccountKey] NVARCHAR(100) NOT NULL,
    [DisplayName] NVARCHAR(200) NOT NULL,
    [ExternalAccountId] NVARCHAR(200) NULL,
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
    CONSTRAINT [FK_HiveProviderAccounts_Provider]
        FOREIGN KEY ([ProviderId])
        REFERENCES [dbo].[HiveProviders] ([ProviderId]),
    CONSTRAINT [CK_HiveProviderAccounts_AccountKey] CHECK (LEN([AccountKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveProviderAccounts_DisplayName] CHECK (LEN([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveProviderAccounts_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveProviderAccounts_ResourceVersion] CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveProviderAccounts_Lifecycle] CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveProviderAccounts_MetadataJson] CHECK (ISJSON([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveProviderAccounts_Provider_Key]
    ON [dbo].[HiveProviderAccounts] ([ProviderId], [AccountKey]);

CREATE INDEX [IX_HiveProviderAccounts_OwnerScope]
    ON [dbo].[HiveProviderAccounts] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);

CREATE TABLE [dbo].[HiveExecutionTargets]
(
    [ExecutionTargetId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveExecutionTargets] PRIMARY KEY CLUSTERED,
    [ProviderId] UNIQUEIDENTIFIER NOT NULL,
    [ProviderAccountId] UNIQUEIDENTIFIER NOT NULL,
    [TargetKey] NVARCHAR(100) NOT NULL,
    [DisplayName] NVARCHAR(200) NOT NULL,
    [EndpointUri] NVARCHAR(2048) NOT NULL,
    [Model] NVARCHAR(512) NULL,
    [Deployment] NVARCHAR(512) NULL,
    [CapabilitiesJson] NVARCHAR(MAX) NOT NULL,
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
    CONSTRAINT [FK_HiveExecutionTargets_Provider]
        FOREIGN KEY ([ProviderId])
        REFERENCES [dbo].[HiveProviders] ([ProviderId]),
    CONSTRAINT [FK_HiveExecutionTargets_ProviderAccount]
        FOREIGN KEY ([ProviderAccountId])
        REFERENCES [dbo].[HiveProviderAccounts] ([ProviderAccountId]),
    CONSTRAINT [CK_HiveExecutionTargets_TargetKey] CHECK (LEN([TargetKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveExecutionTargets_DisplayName] CHECK (LEN([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveExecutionTargets_EndpointUri] CHECK (LEN([EndpointUri]) BETWEEN 1 AND 2048),
    CONSTRAINT [CK_HiveExecutionTargets_ModelOrDeployment] CHECK
    (
        LEN(ISNULL([Model], '')) > 0
        OR LEN(ISNULL([Deployment], '')) > 0
    ),
    CONSTRAINT [CK_HiveExecutionTargets_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveExecutionTargets_ResourceVersion] CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveExecutionTargets_Lifecycle] CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveExecutionTargets_MetadataJson] CHECK (ISJSON([MetadataJson]) = 1),
    CONSTRAINT [CK_HiveExecutionTargets_CapabilitiesJson] CHECK (ISJSON([CapabilitiesJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveExecutionTargets_ProviderAccount_Key]
    ON [dbo].[HiveExecutionTargets] ([ProviderAccountId], [TargetKey]);

CREATE INDEX [IX_HiveExecutionTargets_Provider]
    ON [dbo].[HiveExecutionTargets] ([ProviderId]);

CREATE INDEX [IX_HiveExecutionTargets_OwnerScope]
    ON [dbo].[HiveExecutionTargets] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 2,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
