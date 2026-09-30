/*
    ProviderAccount credential references are validated by the persistence
    boundary before being written. Keep an index on the nullable reference so
    secret deletion can efficiently detect active references without making
    migration dependent on cleanup of legacy dangling references.
*/

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_HiveProviderAccounts_CredentialSecretId'
      AND [object_id] = OBJECT_ID(N'[dbo].[HiveProviderAccounts]')
)
BEGIN
    CREATE INDEX [IX_HiveProviderAccounts_CredentialSecretId]
        ON [dbo].[HiveProviderAccounts] ([CredentialSecretId])
        WHERE [CredentialSecretId] IS NOT NULL;
END;

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 12,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
