ALTER TABLE [HiveEventOutbox]
ADD COLUMN [AttemptCount] INTEGER NOT NULL DEFAULT 0;

ALTER TABLE [HiveEventOutbox]
ADD COLUMN [LeaseId] TEXT NULL;

ALTER TABLE [HiveEventOutbox]
ADD COLUMN [LeaseExpiresAtUtc] TEXT NULL;

CREATE INDEX [IX_HiveEventOutbox_Lease]
    ON [HiveEventOutbox] ([LeaseExpiresAtUtc], [CreatedAtUtc], [EventId]);
