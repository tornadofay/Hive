namespace Hive.Host.WinForms;

internal interface IHiveAdvancedConfigurationPage
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
