CREATE TABLE [HiveEventLog]
(
    [EventId] TEXT NOT NULL
        CONSTRAINT [PK_HiveEventLog] PRIMARY KEY,
    [StreamKind] INTEGER NOT NULL,
    [StreamIdentity] TEXT NOT NULL,
    [StreamVersion] INTEGER NOT NULL,
    [OccurredAtUtc] TEXT NOT NULL,
    [EventType] TEXT NOT NULL,
    [PayloadSchemaVersion] INTEGER NOT NULL,
    [CorrelationId] TEXT NOT NULL,
    [CausationId] TEXT NULL,
    [PayloadJson] TEXT NOT NULL,
    CONSTRAINT [CK_HiveEventLog_StreamKind] CHECK ([StreamKind] >= 0),
    CONSTRAINT [CK_HiveEventLog_StreamIdentity] CHECK ([StreamIdentity] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveEventLog_StreamVersion] CHECK ([StreamVersion] > 0),
    CONSTRAINT [CK_HiveEventLog_EventType] CHECK (length([EventType]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveEventLog_PayloadSchemaVersion] CHECK ([PayloadSchemaVersion] > 0),
    CONSTRAINT [CK_HiveEventLog_PayloadJson] CHECK (json_valid([PayloadJson]) = 1)
);

CREATE UNIQUE INDEX [UX_HiveEventLog_StreamVersion]
    ON [HiveEventLog] ([StreamKind], [StreamIdentity], [StreamVersion]);

CREATE INDEX [IX_HiveEventLog_Stream]
    ON [HiveEventLog] ([StreamKind], [StreamIdentity], [StreamVersion]);

CREATE TABLE [HiveEventSnapshots]
(
    [StreamKind] INTEGER NOT NULL,
    [StreamIdentity] TEXT NOT NULL,
    [SnapshotVersion] INTEGER NOT NULL,
    [PayloadSchemaVersion] INTEGER NOT NULL,
    [StateJson] TEXT NOT NULL,
    [UpdatedAtUtc] TEXT NOT NULL,
    CONSTRAINT [PK_HiveEventSnapshots]
        PRIMARY KEY ([StreamKind], [StreamIdentity]),
    CONSTRAINT [CK_HiveEventSnapshots_StreamKind] CHECK ([StreamKind] >= 0),
    CONSTRAINT [CK_HiveEventSnapshots_StreamIdentity] CHECK ([StreamIdentity] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveEventSnapshots_SnapshotVersion] CHECK ([SnapshotVersion] > 0),
    CONSTRAINT [CK_HiveEventSnapshots_PayloadSchemaVersion] CHECK ([PayloadSchemaVersion] > 0),
    CONSTRAINT [CK_HiveEventSnapshots_StateJson] CHECK (json_valid([StateJson]) = 1)
);

CREATE TABLE [HiveEventOutbox]
(
    [EventId] TEXT NOT NULL
        CONSTRAINT [PK_HiveEventOutbox] PRIMARY KEY,
    [StreamKind] INTEGER NOT NULL,
    [StreamIdentity] TEXT NOT NULL,
    [StreamVersion] INTEGER NOT NULL,
    [OccurredAtUtc] TEXT NOT NULL,
    [EventType] TEXT NOT NULL,
    [PayloadSchemaVersion] INTEGER NOT NULL,
    [CorrelationId] TEXT NOT NULL,
    [CausationId] TEXT NULL,
    [PayloadJson] TEXT NOT NULL,
    [CreatedAtUtc] TEXT NOT NULL,
    CONSTRAINT [FK_HiveEventOutbox_Event]
        FOREIGN KEY ([EventId])
        REFERENCES [HiveEventLog] ([EventId]),
    CONSTRAINT [CK_HiveEventOutbox_StreamKind] CHECK ([StreamKind] >= 0),
    CONSTRAINT [CK_HiveEventOutbox_StreamIdentity] CHECK ([StreamIdentity] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_HiveEventOutbox_StreamVersion] CHECK ([StreamVersion] > 0),
    CONSTRAINT [CK_HiveEventOutbox_EventType] CHECK (length([EventType]) BETWEEN 1 AND 200),
    CONSTRAINT [CK_HiveEventOutbox_PayloadSchemaVersion] CHECK ([PayloadSchemaVersion] > 0),
    CONSTRAINT [CK_HiveEventOutbox_PayloadJson] CHECK (json_valid([PayloadJson]) = 1)
);

CREATE INDEX [IX_HiveEventOutbox_Created]
    ON [HiveEventOutbox] ([CreatedAtUtc], [EventId]);

CREATE INDEX [IX_HiveEventOutbox_Stream]
    ON [HiveEventOutbox] ([StreamKind], [StreamIdentity], [StreamVersion]);
