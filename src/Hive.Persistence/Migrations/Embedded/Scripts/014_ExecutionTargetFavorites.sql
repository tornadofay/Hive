CREATE TABLE [HiveExecutionTargetFavorites]
(
    [DeploymentId] TEXT NOT NULL,
    [OwnerPrincipalId] TEXT NOT NULL,
    [ScopeKind] INTEGER NOT NULL,
    [ScopeIdentity] TEXT NOT NULL,
    [ExecutionTargetId] TEXT NOT NULL,
    [DisplayOrder] INTEGER NOT NULL,
    CONSTRAINT [PK_HiveExecutionTargetFavorites]
        PRIMARY KEY
        (
            [DeploymentId],
            [OwnerPrincipalId],
            [ScopeKind],
            [ScopeIdentity],
            [ExecutionTargetId]
        )
);

CREATE INDEX [IX_HiveExecutionTargetFavorites_Order]
    ON [HiveExecutionTargetFavorites]
    (
        [DeploymentId],
        [OwnerPrincipalId],
        [ScopeKind],
        [ScopeIdentity],
        [DisplayOrder],
        [ExecutionTargetId]
    );
