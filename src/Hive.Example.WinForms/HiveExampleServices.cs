using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using Hive.Persistence;
using Hive.Providers.OpenAICompatible;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleServices : IServiceProvider
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;
    private readonly IHiveManagementFacade _management;

    public HiveExampleServices(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        ArgumentNullException.ThrowIfNull(output);

        _themeManager = themeManager;
        _output = output;
        var databaseOptions = HiveDatabaseOptions.LocalDevelopment();

        _management = new HiveManagementFacade(
            new SqlProviderResourceStore(databaseOptions),
            new SqlAgentDefinitionResourceStore(databaseOptions),
            new SqlWorkItemResourceStore(databaseOptions),
            new SqlDpapiSecretStore(databaseOptions),
            new OpenAICompatibleProviderConnectionTester(),
            new JsonHiveConfigurationStore(),
            new HivePersistenceConnectionTester());
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(IHiveThemeManager)
            ? _themeManager
            : serviceType == typeof(IHiveExampleOutput)
                ? _output
                : serviceType == typeof(IHiveManagementFacade)
                    ? _management
                    : null;
    }
}
