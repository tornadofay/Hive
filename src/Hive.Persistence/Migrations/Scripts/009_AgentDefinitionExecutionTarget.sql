IF COL_LENGTH('[dbo].[HiveAgentDefinitions]', 'ConfiguredExecutionTargetId') IS NULL
BEGIN
    ALTER TABLE [dbo].[HiveAgentDefinitions]
    ADD [ConfiguredExecutionTargetId] UNIQUEIDENTIFIER NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_HiveAgentDefinitions_ExecutionTarget'
      AND [parent_object_id] = OBJECT_ID(N'[dbo].[HiveAgentDefinitions]')
)
BEGIN
    ALTER TABLE [dbo].[HiveAgentDefinitions]
    ADD CONSTRAINT [FK_HiveAgentDefinitions_ExecutionTarget]
        FOREIGN KEY ([ConfiguredExecutionTargetId])
        REFERENCES [dbo].[HiveExecutionTargets] ([ExecutionTargetId]);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_HiveAgentDefinitions_ConfiguredExecutionTarget'
      AND [object_id] = OBJECT_ID(N'[dbo].[HiveAgentDefinitions]')
)
BEGIN
    CREATE INDEX [IX_HiveAgentDefinitions_ConfiguredExecutionTarget]
        ON [dbo].[HiveAgentDefinitions] ([ConfiguredExecutionTargetId]);
END;

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 9,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
