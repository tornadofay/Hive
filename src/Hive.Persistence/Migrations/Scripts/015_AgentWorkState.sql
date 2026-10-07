CREATE TABLE [dbo].[HiveAgentWorkState]
(
    [WorkStateKind] INT NOT NULL,
    [StateId] UNIQUEIDENTIFIER NOT NULL,
    [AgentId] UNIQUEIDENTIFIER NOT NULL,
    [RuntimeId] UNIQUEIDENTIFIER NOT NULL,
    [OwnerPrincipalId] UNIQUEIDENTIFIER NOT NULL,
    [ScopeKind] INT NOT NULL,
    [ScopeIdentity] UNIQUEIDENTIFIER NOT NULL,
    [ResourceVersion] BIGINT NOT NULL,
    [CreatedAtUtc] DATETIME2(7) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [CausationId] UNIQUEIDENTIFIER NULL,
    [SourceKind] INT NULL,
    [SourceIdentity] UNIQUEIDENTIFIER NULL,
    [LifecycleStatus] INT NOT NULL,
    [LifecycleChangedAtUtc] DATETIME2(7) NOT NULL,
    [StateStatus] INT NOT NULL,
    [StateKey] NVARCHAR(MAX) NULL,
    [ExpiresAtUtc] DATETIME2(7) NULL,
    [StateJson] NVARCHAR(MAX) NOT NULL,
    [UpdatedAtUtc] DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_HiveAgentWorkState]
        PRIMARY KEY CLUSTERED ([WorkStateKind], [StateId]),
    CONSTRAINT [CK_HiveAgentWorkState_Kind]
        CHECK ([WorkStateKind] BETWEEN 1 AND 4),
    CONSTRAINT [CK_HiveAgentWorkState_StateId]
        CHECK ([StateId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveAgentWorkState_AgentId]
        CHECK ([AgentId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveAgentWorkState_RuntimeId]
        CHECK ([RuntimeId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveAgentWorkState_OwnerPrincipalId]
        CHECK ([OwnerPrincipalId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveAgentWorkState_Scope]
        CHECK ([ScopeKind] = 5 AND [ScopeIdentity] = [RuntimeId]),
    CONSTRAINT [CK_HiveAgentWorkState_ResourceVersion]
        CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveAgentWorkState_CorrelationId]
        CHECK ([CorrelationId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveAgentWorkState_CausationId]
        CHECK ([CausationId] IS NULL OR [CausationId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveAgentWorkState_Source]
        CHECK
        (
            ([SourceKind] IS NULL AND [SourceIdentity] IS NULL)
            OR ([SourceKind] IS NOT NULL AND [SourceIdentity] IS NOT NULL)
        ),
    CONSTRAINT [CK_HiveAgentWorkState_Lifecycle]
        CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveAgentWorkState_StateStatus]
        CHECK ([StateStatus] >= 0),
    CONSTRAINT [CK_HiveAgentWorkState_StateJson]
        CHECK (ISJSON([StateJson]) = 1)
);

CREATE INDEX [IX_HiveAgentWorkState_Runtime]
    ON [dbo].[HiveAgentWorkState]
    (
        [WorkStateKind],
        [AgentId],
        [RuntimeId],
        [CreatedAtUtc],
        [StateId]
    );

CREATE INDEX [IX_HiveAgentWorkState_QuestionExpiry]
    ON [dbo].[HiveAgentWorkState]
    (
        [WorkStateKind],
        [StateStatus],
        [ExpiresAtUtc],
        [StateId]
    );

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 15,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
