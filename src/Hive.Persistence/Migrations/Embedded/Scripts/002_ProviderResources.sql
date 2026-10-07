CREATE TABLE [HiveProviders]
(
    [ProviderId] TEXT NOT NULL
        CONSTRAINT [PK_HiveProviders] PRIMARY KEY,
    [ProviderKey] TEXT NOT NULL,
    [DisplayName] TEXT NOT NULL,
    [TransportKind] TEXT NOT NULL,
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
    CONSTRAINT [CK_HiveProviders_ProviderKey] CHECK (length([ProviderKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveProviders_DisplayName] CHECK (length([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveProviders_TransportKind] CHECK (length([TransportKind]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveProviders_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveProviders_ResourceVersion] CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveProviders_Lifecycle] CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveProviders_MetadataJson] CHECK (json_valid([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveProviders_ProviderKey]
    ON [HiveProviders] ([ProviderKey]);

CREATE INDEX [IX_HiveProviders_OwnerScope]
    ON [HiveProviders] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);

CREATE TABLE [HiveProviderAccounts]
(
    [ProviderAccountId] TEXT NOT NULL
        CONSTRAINT [PK_HiveProviderAccounts] PRIMARY KEY,
    [ProviderId] TEXT NOT NULL,
    [AccountKey] TEXT NOT NULL,
    [DisplayName] TEXT NOT NULL,
    [ExternalAccountId] TEXT NULL,
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
    CONSTRAINT [FK_HiveProviderAccounts_Provider]
        FOREIGN KEY ([ProviderId])
        REFERENCES [HiveProviders] ([ProviderId]),
    CONSTRAINT [CK_HiveProviderAccounts_AccountKey] CHECK (length([AccountKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveProviderAccounts_DisplayName] CHECK (length([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveProviderAccounts_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveProviderAccounts_ResourceVersion] CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveProviderAccounts_Lifecycle] CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveProviderAccounts_MetadataJson] CHECK (json_valid([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveProviderAccounts_Provider_Key]
    ON [HiveProviderAccounts] ([ProviderId], [AccountKey]);

CREATE INDEX [IX_HiveProviderAccounts_OwnerScope]
    ON [HiveProviderAccounts] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);

CREATE TABLE [HiveExecutionTargets]
(
    [ExecutionTargetId] TEXT NOT NULL
        CONSTRAINT [PK_HiveExecutionTargets] PRIMARY KEY,
    [ProviderId] TEXT NOT NULL,
    [ProviderAccountId] TEXT NOT NULL,
    [TargetKey] TEXT NOT NULL,
    [DisplayName] TEXT NOT NULL,
    [EndpointUri] TEXT NOT NULL,
    [Model] TEXT NULL,
    [Deployment] TEXT NULL,
    [CapabilitiesJson] TEXT NOT NULL,
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
    CONSTRAINT [FK_HiveExecutionTargets_Provider]
        FOREIGN KEY ([ProviderId])
        REFERENCES [HiveProviders] ([ProviderId]),
    CONSTRAINT [FK_HiveExecutionTargets_ProviderAccount]
        FOREIGN KEY ([ProviderAccountId])
        REFERENCES [HiveProviderAccounts] ([ProviderAccountId]),
    CONSTRAINT [CK_HiveExecutionTargets_TargetKey] CHECK (length([TargetKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveExecutionTargets_DisplayName] CHECK (length([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveExecutionTargets_EndpointUri] CHECK (length([EndpointUri]) BETWEEN 1 AND 2048),
    CONSTRAINT [CK_HiveExecutionTargets_ModelOrDeployment] CHECK
    (
        length(COALESCE([Model], '')) > 0
        OR length(COALESCE([Deployment], '')) > 0
    ),
    CONSTRAINT [CK_HiveExecutionTargets_Scope] CHECK
    (
        ([ScopeKind] = 0 AND [ScopeIdentity] IS NULL)
        OR ([ScopeKind] BETWEEN 1 AND 6 AND [ScopeIdentity] IS NOT NULL)
    ),
    CONSTRAINT [CK_HiveExecutionTargets_ResourceVersion] CHECK ([ResourceVersion] > 0),
    CONSTRAINT [CK_HiveExecutionTargets_Lifecycle] CHECK ([LifecycleStatus] BETWEEN 0 AND 2),
    CONSTRAINT [CK_HiveExecutionTargets_MetadataJson] CHECK (json_valid([MetadataJson]) = 1),
    CONSTRAINT [CK_HiveExecutionTargets_CapabilitiesJson] CHECK (json_valid([CapabilitiesJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveExecutionTargets_ProviderAccount_Key]
    ON [HiveExecutionTargets] ([ProviderAccountId], [TargetKey]);

CREATE INDEX [IX_HiveExecutionTargets_Provider]
    ON [HiveExecutionTargets] ([ProviderId]);

CREATE INDEX [IX_HiveExecutionTargets_OwnerScope]
    ON [HiveExecutionTargets] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);
