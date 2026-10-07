CREATE TABLE "HiveSchemaVersion"
(
    "SchemaRowId" INTEGER NOT NULL PRIMARY KEY,
    "SchemaVersion" INTEGER NOT NULL,
    "RecordedAtUtc" TEXT NOT NULL,
    CONSTRAINT "CK_HiveSchemaVersion_SchemaRowId" CHECK ("SchemaRowId" = 1),
    CONSTRAINT "CK_HiveSchemaVersion_SchemaVersion" CHECK ("SchemaVersion" > 0)
);

CREATE UNIQUE INDEX "UX_HiveSchemaVersion_SchemaVersion"
    ON "HiveSchemaVersion" ("SchemaVersion");

CREATE TABLE "HiveMigrationJournal"
(
    "MigrationVersion" INTEGER NOT NULL PRIMARY KEY,
    "ScriptName" TEXT NOT NULL UNIQUE,
    "AppliedAtUtc" TEXT NOT NULL,
    CONSTRAINT "CK_HiveMigrationJournal_MigrationVersion"
        CHECK ("MigrationVersion" > 0)
);