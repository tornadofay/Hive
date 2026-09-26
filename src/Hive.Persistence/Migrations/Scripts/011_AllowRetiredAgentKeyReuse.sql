/*
    AgentDefinition keys identify active configurations for an owner.

    Retired AgentDefinitions remain durable history, but their keys must be
    reusable for a newly created AgentDefinition. Replace the historical
    unfiltered unique index with a filtered unique index over active rows.
*/

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_HiveAgentDefinitions_Owner_Key'
      AND [object_id] = OBJECT_ID(N'[dbo].[HiveAgentDefinitions]')
)
BEGIN
    DROP INDEX [UX_HiveAgentDefinitions_Owner_Key]
        ON [dbo].[HiveAgentDefinitions];
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_HiveAgentDefinitions_Owner_ActiveKey'
      AND [object_id] = OBJECT_ID(N'[dbo].[HiveAgentDefinitions]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_HiveAgentDefinitions_Owner_ActiveKey]
        ON [dbo].[HiveAgentDefinitions] ([OwnerPrincipalId], [DefinitionKey])
        WHERE [LifecycleStatus] = 0;
END;

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 11,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
