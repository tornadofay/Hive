CREATE TABLE [HiveAgentWorkState]
(
    [WorkStateKind] INTEGER NOT NULL,
    [StateId] TEXT NOT NULL,
    [AgentId] TEXT NOT NULL,
    [RuntimeId] TEXT NOT NULL,
    [OwnerPrincipalId] TEXT NOT NULL,
    [ScopeKind] INTEGER NOT NULL,
    [ScopeIdentity] TEXT NOT NULL,
    [ResourceVersion] INTEGER NOT NULL,
    [CreatedAtUtc] TEXT NOT NULL,
    [CorrelationId] TEXT NOT NULL,
    [CausationId] TEXT NULL,
    [SourceKind] INTEGER NULL,
    [SourceIdentity] TEXT NULL,
    [LifecycleStatus] INTEGER NOT NULL,
    [LifecycleChangedAtUtc] TEXT NOT NULL,
    [StateStatus] INTEGER NOT NULL,
    [StateKey] TEXT NULL,
    [ExpiresAtUtc] TEXT NULL,
    [StateJson] TEXT NOT NULL,
    [UpdatedAtUtc] TEXT NOT NULL,
    CONSTRAINT [PK_HiveAgentWorkState]
        PRIMARY KEY ([WorkStateKind], [StateId]),
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
        CHECK (json_valid([StateJson]) = 1)
);

CREATE INDEX [IX_HiveAgentWorkState_Runtime]
    ON [HiveAgentWorkState]
    (
        [WorkStateKind],
        [AgentId],
        [RuntimeId],
        [CreatedAtUtc],
        [StateId]
    );

CREATE INDEX [IX_HiveAgentWorkState_QuestionExpiry]
    ON [HiveAgentWorkState]
    (
        [WorkStateKind],
        [StateStatus],
        [ExpiresAtUtc],
        [StateId]
    );
