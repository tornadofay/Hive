ALTER TABLE [HiveAgentDefinitions]
ADD COLUMN [ConfiguredExecutionTargetId] TEXT NULL
    REFERENCES [HiveExecutionTargets] ([ExecutionTargetId]);

CREATE INDEX [IX_HiveAgentDefinitions_ConfiguredExecutionTarget]
    ON [HiveAgentDefinitions] ([ConfiguredExecutionTargetId]);
