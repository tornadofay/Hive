/*
    Durable owner/scope-aware ExecutionTarget favorites.

    Favorites are preferences, not Hive resources. The target identity remains
    durable even when the target is retired, so removing a target from the
    active candidate set does not silently erase the user's preference.
*/

IF OBJECT_ID(N'[dbo].[HiveExecutionTargetFavorites]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[HiveExecutionTargetFavorites]
    (
        [DeploymentId] UNIQUEIDENTIFIER NOT NULL,
        [OwnerPrincipalId] UNIQUEIDENTIFIER NOT NULL,
        [ScopeKind] INT NOT NULL,
        [ScopeIdentity] UNIQUEIDENTIFIER NOT NULL,
        [ExecutionTargetId] UNIQUEIDENTIFIER NOT NULL,
        [DisplayOrder] INT NOT NULL,
        CONSTRAINT [PK_HiveExecutionTargetFavorites]
            PRIMARY KEY CLUSTERED
            (
                [DeploymentId],
                [OwnerPrincipalId],
                [ScopeKind],
                [ScopeIdentity],
                [ExecutionTargetId]
            )
    );

    CREATE INDEX [IX_HiveExecutionTargetFavorites_Order]
        ON [dbo].[HiveExecutionTargetFavorites]
        (
            [DeploymentId],
            [OwnerPrincipalId],
            [ScopeKind],
            [ScopeIdentity],
            [DisplayOrder],
            [ExecutionTargetId]
        );
END;

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 14,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
