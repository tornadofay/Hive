DROP INDEX IF EXISTS [UX_HiveAgentDefinitions_Owner_Key];

CREATE UNIQUE INDEX IF NOT EXISTS [UX_HiveAgentDefinitions_Owner_ActiveKey]
    ON [HiveAgentDefinitions] ([OwnerPrincipalId], [DefinitionKey])
    WHERE [LifecycleStatus] = 0;
