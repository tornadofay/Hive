CREATE TABLE [dbo].[HiveEventLog]
(
    [EventId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveEventLog] PRIMARY KEY CLUSTERED,
    [StreamKind] INT NOT NULL,
    [StreamIdentity] UNIQUEIDENTIFIER NOT NULL,
    [StreamVersion] BIGINT NOT NULL,
    [OccurredAtUtc] DATETIME2(7) NOT NULL,
    [EventType] NVARCHAR(200) NOT NULL,
    [PayloadSchemaVersion] INT NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [CausationId] UNIQUEIDENTIFIER NULL,
    [PayloadJson] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [CK_HiveEventLog_StreamKind] CHECK ([StreamKind] >= 0),
    CONSTRAINT [CK_HiveEventLog_StreamIdentity] CHECK ([StreamIdentity] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveEventLog_StreamVersion] CHECK ([StreamVersion] > 0),
    CONSTRAINT [CK_HiveEventLog_EventType] CHECK (LEN([EventType]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveEventLog_PayloadSchemaVersion] CHECK ([PayloadSchemaVersion] > 0),
    CONSTRAINT [CK_HiveEventLog_PayloadJson] CHECK (ISJSON([PayloadJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveEventLog_StreamVersion]
    ON [dbo].[HiveEventLog] ([StreamKind], [StreamIdentity], [StreamVersion]);

CREATE INDEX [IX_HiveEventLog_Stream]
    ON [dbo].[HiveEventLog] ([StreamKind], [StreamIdentity], [StreamVersion]);

CREATE TABLE [dbo].[HiveEventSnapshots]
(
    [StreamKind] INT NOT NULL,
    [StreamIdentity] UNIQUEIDENTIFIER NOT NULL,
    [SnapshotVersion] BIGINT NOT NULL,
    [PayloadSchemaVersion] INT NOT NULL,
    [StateJson] NVARCHAR(MAX) NOT NULL,
    [UpdatedAtUtc] DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_HiveEventSnapshots]
        PRIMARY KEY CLUSTERED ([StreamKind], [StreamIdentity]),
    CONSTRAINT [CK_HiveEventSnapshots_StreamKind] CHECK ([StreamKind] >= 0),
    CONSTRAINT [CK_HiveEventSnapshots_StreamIdentity] CHECK ([StreamIdentity] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveEventSnapshots_SnapshotVersion] CHECK ([SnapshotVersion] > 0),
    CONSTRAINT [CK_HiveEventSnapshots_PayloadSchemaVersion] CHECK ([PayloadSchemaVersion] > 0),
    CONSTRAINT [CK_HiveEventSnapshots_StateJson] CHECK (ISJSON([StateJson]) = 1)
);

CREATE TABLE [dbo].[HiveEventOutbox]
(
    [EventId] UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT [PK_HiveEventOutbox] PRIMARY KEY CLUSTERED,
    [StreamKind] INT NOT NULL,
    [StreamIdentity] UNIQUEIDENTIFIER NOT NULL,
    [StreamVersion] BIGINT NOT NULL,
    [OccurredAtUtc] DATETIME2(7) NOT NULL,
    [EventType] NVARCHAR(200) NOT NULL,
    [PayloadSchemaVersion] INT NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [CausationId] UNIQUEIDENTIFIER NULL,
    [PayloadJson] NVARCHAR(MAX) NOT NULL,
    [CreatedAtUtc] DATETIME2(7) NOT NULL,
    CONSTRAINT [FK_HiveEventOutbox_Event]
        FOREIGN KEY ([EventId])
        REFERENCES [dbo].[HiveEventLog] ([EventId]),
    CONSTRAINT [CK_HiveEventOutbox_StreamKind] CHECK ([StreamKind] >= 0),
    CONSTRAINT [CK_HiveEventOutbox_StreamIdentity] CHECK ([StreamIdentity] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveEventOutbox_StreamVersion] CHECK ([StreamVersion] > 0),
    CONSTRAINT [CK_HiveEventOutbox_EventType] CHECK (LEN([EventType]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveEventOutbox_PayloadSchemaVersion] CHECK ([PayloadSchemaVersion] > 0),
    CONSTRAINT [CK_HiveEventOutbox_PayloadJson] CHECK (ISJSON([PayloadJson]) = 1)
);

CREATE INDEX [IX_HiveEventOutbox_Created]
    ON [dbo].[HiveEventOutbox] ([CreatedAtUtc], [EventId]);

CREATE INDEX [IX_HiveEventOutbox_Stream]
    ON [dbo].[HiveEventOutbox] ([StreamKind], [StreamIdentity], [StreamVersion]);

UPDATE [dbo].[HiveSchemaVersion]
SET [SchemaVersion] = 4,
    [RecordedAtUtc] = SYSUTCDATETIME()
WHERE [SchemaRowId] = 1;
