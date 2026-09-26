IF COL_LENGTH('[dbo].[HiveProviderAccounts]', 'CredentialSecretId') IS NULL
BEGIN
    ALTER TABLE [dbo].[HiveProviderAccounts]
    ADD [CredentialSecretId] UNIQUEIDENTIFIER NULL;
END;

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 8,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
