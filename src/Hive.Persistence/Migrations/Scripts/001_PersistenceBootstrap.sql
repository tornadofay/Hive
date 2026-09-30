CREATE TABLE [dbo].[HiveSchemaVersion]
(
    [SchemaRowId] TINYINT NOT NULL
        CONSTRAINT [PK_HiveSchemaVersion] PRIMARY KEY CLUSTERED,
    [SchemaVersion] INT NOT NULL,
    [RecordedAtUtc] DATETIME2(7) NOT NULL,
    CONSTRAINT [CK_HiveSchemaVersion_SchemaRowId] CHECK ([SchemaRowId] = 1),
    CONSTRAINT [CK_HiveSchemaVersion_SchemaVersion] CHECK ([SchemaVersion] > 0)
);

CREATE UNIQUE INDEX [UX_HiveSchemaVersion_SchemaVersion]
    ON [dbo].[HiveSchemaVersion] ([SchemaVersion]);

INSERT INTO [dbo].[HiveSchemaVersion]
(
    [SchemaRowId],
    [SchemaVersion],
    [RecordedAtUtc]
)
VALUES
(
    1,
    1,
    SYSUTCDATETIME()
);