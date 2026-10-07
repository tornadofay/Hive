CREATE TABLE [HiveAgentDefinitions]
(
    [AgentDefinitionId] TEXT NOT NULL
        CONSTRAINT [PK_HiveAgentDefinitions] PRIMARY KEY,
    [DefinitionKey] TEXT NOT NULL,
    [DisplayName] TEXT NOT NULL,
    [Generation] INTEGER NOT NULL,
    [OwnerPrincipalId] TEXT NOT NULL,
    [ScopeKind] INTEGER NOT NULL,
    [ScopeIdentity] TEXT NULL,
    [ResourceVersion] INTEGER NOT NULL,
    [CreatedByPrincipalId] TEXT NOT NULL,
    [CreatedAtUtc] TEXT NOT NULL,
    [CorrelationId] TEXT NOT NULL,
    [CausationId] TEXT NULL,
    [LifecycleStatus] INTEGER NOT NULL,
    [LifecycleChangedAtUtc] TEXT NOT NULL,
    [MetadataJson] TEXT NOT NULL,
    CONSTRAINT [CK_HiveAgentDefinitions_DefinitionKey]
        CHECK (length([DefinitionKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveAgentDefinitions_DisplayName]
        CHECK (length([DisplayName]) BETWEEN 1 AND 200),
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
        CHECK (json_valid([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveAgentDefinitions_Owner_Key]
    ON [HiveAgentDefinitions] ([OwnerPrincipalId], [DefinitionKey]);

CREATE INDEX [IX_HiveAgentDefinitions_OwnerScope]
    ON [HiveAgentDefinitions] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);
