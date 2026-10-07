using System.Globalization;
using System.Reflection;

namespace Hive.Persistence;

internal static class EmbeddedPersistenceMigrationCatalog
{
    private const string ResourceMarker = ".Migrations.Embedded.Scripts.";

    public static IReadOnlyList<EmbeddedPersistenceMigration> Load(
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var migrations = assembly
            .GetManifestResourceNames()
            .Where(static name =>
                name.Contains(ResourceMarker, StringComparison.OrdinalIgnoreCase))
            .Select(name => LoadMigration(assembly, name))
            .OrderBy(static migration => migration.Version)
            .ThenBy(static migration => migration.Name, StringComparer.Ordinal)
            .ToArray();

        Validate(migrations);
        return migrations;
    }

    private static EmbeddedPersistenceMigration LoadMigration(
        Assembly assembly,
        string resourceName)
    {
        var markerIndex = resourceName.IndexOf(
            ResourceMarker,
            StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
            throw new InvalidOperationException(
                "An Embedded persistence migration resource has an invalid resource name.");

        var fileName = resourceName[(markerIndex + ResourceMarker.Length)..];

        if (!fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "An Embedded persistence migration resource must be a .sql file.");

        var separatorIndex = fileName.IndexOf('_');

        if (separatorIndex <= 0 ||
            !int.TryParse(
                fileName[..separatorIndex],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var version) ||
            version <= 0)
        {
            throw new InvalidOperationException(
                "An Embedded persistence migration resource must begin with a positive numeric version.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                "An Embedded persistence migration resource could not be opened.");

        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException(
                $"Embedded persistence migration '{fileName}' is empty.");

        return new EmbeddedPersistenceMigration(
            version,
            fileName,
            sql);
    }

    public static void Validate(
        IReadOnlyList<EmbeddedPersistenceMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        if (migrations.Count == 0)
            throw new InvalidOperationException(
                "At least one Embedded persistence migration is required.");

        for (var index = 0; index < migrations.Count; index++)
        {
            var migration = migrations[index];

            if (migration.Version != index + 1)
                throw new InvalidOperationException(
                    "Embedded persistence migration versions must be contiguous and start at 1.");

            if (string.IsNullOrWhiteSpace(migration.Name))
                throw new InvalidOperationException(
                    "Embedded persistence migration names are required.");

            if (string.IsNullOrWhiteSpace(migration.Sql))
                throw new InvalidOperationException(
                    $"Embedded persistence migration '{migration.Name}' is empty.");
        }

        if (migrations[^1].Version != EmbeddedPersistenceSchema.CurrentSchemaVersion)
            throw new InvalidOperationException(
                "Embedded persistence migration catalog does not match the supported foundation schema version.");
    }
}
