CREATE TABLE [dbo].[HiveAgentDefinitions]
(
    [AgentDefinitionId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveAgentDefinitions] PRIMARY KEY CLUSTERED,
    [DefinitionKey] NVARCHAR(100) NOT NULL,
    [DisplayName] NVARCHAR(200) NOT NULL,
    [Generation] INT NOT NULL,
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
    CONSTRAINT [CK_HiveAgentDefinitions_DefinitionKey]
        CHECK (LEN([DefinitionKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveAgentDefinitions_DisplayName]
        CHECK (LEN([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveAgentDefinitions_Generation]
        CHECK ([Generation] BETWEEN 0 AND 1),
    CONSTRAINT [CK_HiveAgentDefinitions_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveAgentDefinitions_ResourceVersion]
        CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveAgentDefinitions_Lifecycle]
        CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveAgentDefinitions_MetadataJson]
        CHECK (ISJSON([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveAgentDefinitions_Owner_Key]
    ON [dbo].[HiveAgentDefinitions] ([OwnerPrincipalId], [DefinitionKey]);

CREATE INDEX [IX_HiveAgentDefinitions_OwnerScope]
    ON [dbo].[HiveAgentDefinitions] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 6,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
