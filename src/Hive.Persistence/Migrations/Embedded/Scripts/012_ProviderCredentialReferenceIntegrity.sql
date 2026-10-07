CREATE INDEX IF NOT EXISTS [IX_HiveProviderAccounts_CredentialSecretId]
    ON [HiveProviderAccounts] ([CredentialSecretId])
    WHERE [CredentialSecretId] IS NOT NULL;
