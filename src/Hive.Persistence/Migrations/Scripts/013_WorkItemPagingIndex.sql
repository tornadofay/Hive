/*
    The bounded WorkItem paging contract orders by owner + creation time +
    WorkItem identity. Keep the existing owner/scope and owner/status indexes
    for their other access patterns while adding an index that matches the
    paging order.

    Scope/lifecycle columns are included so the bounded scope predicate and
    retired filtering can be evaluated from the nonclustered index before
    looking up the full WorkItem row.
*/

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_HiveWorkItems_OwnerCreated'
      AND [object_id] = OBJECT_ID(N'[dbo].[HiveWorkItems]')
)
BEGIN
    CREATE INDEX [IX_HiveWorkItems_OwnerCreated]
        ON [dbo].[HiveWorkItems]
        (
            [OwnerPrincipalId],
            [CreatedAtUtc] DESC,
            [WorkItemId] ASC
        )
        INCLUDE
        (
            [ScopeKind],
            [ScopeIdentity],
            [LifecycleStatus]
        );
END;

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 13,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
