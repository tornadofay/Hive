ALTER TABLE [dbo].[HiveEventOutbox]
ADD
    [AttemptCount] INT NULL,
    [LeaseId] UNIQUEIDENTIFIER NULL,
    [LeaseExpiresAtUtc] DATETIME2(7) NULL;

UPDATE [dbo].[HiveEventOutbox]
SET [AttemptCount] = 0
WHERE [AttemptCount] IS NULL;

ALTER TABLE [dbo].[HiveEventOutbox]
ALTER COLUMN [AttemptCount] INT NOT NULL;

ALTER TABLE [dbo].[HiveEventOutbox]
ADD CONSTRAINT [DF_HiveEventOutbox_AttemptCount]
DEFAULT (0) FOR [AttemptCount];

ALTER TABLE [dbo].[HiveEventOutbox]
ADD CONSTRAINT [CK_HiveEventOutbox_AttemptCount]
CHECK ([AttemptCount] >= 0);

ALTER TABLE [dbo].[HiveEventOutbox]
ADD CONSTRAINT [CK_HiveEventOutbox_LeaseState]
CHECK
(
    ([LeaseId] IS NULL AND [LeaseExpiresAtUtc] IS NULL)
    OR
    ([LeaseId] IS NOT NULL AND [LeaseExpiresAtUtc] IS NOT NULL)
);

CREATE INDEX [IX_HiveEventOutbox_Lease]
    ON [dbo].[HiveEventOutbox] ([LeaseExpiresAtUtc], [CreatedAtUtc], [EventId]);

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 5, [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
