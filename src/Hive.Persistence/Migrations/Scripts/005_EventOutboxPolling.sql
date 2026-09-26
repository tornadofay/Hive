ALTER TABLE [dbo].[HiveEventOutbox]
ADD
    [AttemptCount] INT NULL,
    [LeaseId] UNIQUEIDENTIFIER NULL,
    [LeaseExpiresAtUtc] DATETIME2(7) NULL;

GO

UPDATE [dbo].[HiveEventOutbox]
SET [AttemptCount] = 0
WHERE [AttemptCount] IS NULL;

GO

ALTER TABLE [dbo].[HiveEventOutbox]
ALTER COLUMN [AttemptCount] INT NOT NULL;

GO

ALTER TABLE [dbo].[HiveEventOutbox]
ADD CONSTRAINT [DF_HiveEventOutbox_AttemptCount]
DEFAULT (0) FOR [AttemptCount];

GO

ALTER TABLE [dbo].[HiveEventOutbox]
ADD CONSTRAINT [CK_HiveEventOutbox_AttemptCount]
CHECK ([AttemptCount] >= 0);

GO

ALTER TABLE [dbo].[HiveEventOutbox]
ADD CONSTRAINT [CK_HiveEventOutbox_LeaseState]
CHECK
(
    ([LeaseId] IS NULL AND [LeaseExpiresAtUtc] IS NULL)
    OR
    ([LeaseId] IS NOT NULL AND [LeaseExpiresAtUtc] IS NOT NULL)
);

GO

CREATE INDEX [IX_HiveEventOutbox_Lease]
    ON [dbo].[HiveEventOutbox] ([LeaseExpiresAtUtc], [CreatedAtUtc], [EventId]);

GO

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 5,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
