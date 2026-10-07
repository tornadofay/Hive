namespace Hive.Example.WinForms;

internal sealed class PersistenceDataMigrationExample : IHiveExample
{
    public string Category => "Persistence";
    public string Subcategory => "Data Migration";
    public IReadOnlyList<string> AdditionalNavigationPath => ["Full-Data Migration"];
    public int Order => 20;
    public string Title => "SQL Server ↔ Embedded";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new PersistenceDataMigrationExampleView(
            services.GetExampleOutput());
    }
}
