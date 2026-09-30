/*
    Normalize AgentDefinition -> ExecutionTarget reuse.

    An ExecutionTarget is intentionally reusable by multiple AgentDefinitions.
    Migration 009 creates a non-unique lookup index. Older/manual databases may
    contain an accidental unique index on ConfiguredExecutionTargetId; remove
    only unique indexes whose sole key column is ConfiguredExecutionTargetId.
*/

DECLARE @dropSql NVARCHAR(MAX) = N'';

SELECT @dropSql +=
    N'DROP INDEX ' + QUOTENAME(i.[name]) +
    N' ON [dbo].[HiveAgentDefinitions];' + CHAR(13) + CHAR(10)
FROM sys.indexes AS i
INNER JOIN sys.index_columns AS ic
    ON ic.[object_id] = i.[object_id]
   AND ic.[index_id] = i.[index_id]
INNER JOIN sys.columns AS c
    ON c.[object_id] = ic.[object_id]
   AND c.[column_id] = ic.[column_id]
WHERE i.[object_id] = OBJECT_ID(N'[dbo].[HiveAgentDefinitions]')
  AND i.[is_unique] = 1
  AND i.[is_primary_key] = 0
  AND i.[is_unique_constraint] = 0
  AND c.[name] = N'ConfiguredExecutionTargetId'
  AND
  (
      SELECT COUNT(*)
      FROM sys.index_columns AS all_ic
      WHERE all_ic.[object_id] = i.[object_id]
        AND all_ic.[index_id] = i.[index_id]
        AND all_ic.[key_ordinal] > 0
  ) = 1;

IF @dropSql <> N''
    EXEC sys.sp_executesql @dropSql;

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
SET [SchemaVersion] = 10,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
