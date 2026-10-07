CREATE TABLE [HiveSecrets]
(
    [SecretId] TEXT NOT NULL
        CONSTRAINT [PK_HiveSecrets] PRIMARY KEY,
    [SecretKey] TEXT NOT NULL,
    [DisplayName] TEXT NOT NULL,
    [EncryptedValue] BLOB NOT NULL,
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
    CONSTRAINT [CK_HiveSecrets_SecretKey]
        CHECK (length([SecretKey]) BETWEEN 1 AND 100),
    CONSTRAINT [CK_HiveSecrets_DisplayName]
        CHECK (length([DisplayName]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveSecrets_EncryptedValue]
        CHECK (length([EncryptedValue]) > 0),
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
        CHECK (json_valid([MetadataJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveSecrets_SecretKey]
    ON [HiveSecrets] ([SecretKey]);

CREATE INDEX [IX_HiveSecrets_OwnerScope]
    ON [HiveSecrets] ([OwnerPrincipalId], [ScopeKind], [ScopeIdentity]);
