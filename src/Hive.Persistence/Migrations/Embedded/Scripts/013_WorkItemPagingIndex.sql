CREATE INDEX IF NOT EXISTS [IX_HiveWorkItems_OwnerCreated]
    ON [HiveWorkItems]
    (
        [OwnerPrincipalId],
        [CreatedAtUtc] DESC,
        [WorkItemId] ASC
    );
