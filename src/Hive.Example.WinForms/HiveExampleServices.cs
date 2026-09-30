using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Example.WinForms;

internal sealed class HiveExampleServices : IServiceProvider
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;

    public HiveExampleServices(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output,
        HiveHostServiceGraph graph,
        ResourceAccessContext accessContext)
    {
        ArgumentNullException.ThrowIfNull(themeManager);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(accessContext);

        _themeManager = themeManager;
        _output = output;
        _management = graph.Management;
        _accessContext = accessContext;
    }

    public ResourceAccessContext AccessContext => _accessContext;

    public AgentDefinition? SelectedAgentDefinition { get; private set; }

    public void SetSelectedAgentDefinition(AgentDefinition? definition) =>
        SelectedAgentDefinition = definition;

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(HiveExampleServices)
            ? this
            : serviceType == typeof(IHiveThemeManager)
                ? _themeManager
                : serviceType == typeof(IHiveExampleOutput)
                    ? _output
                    : serviceType == typeof(IHiveManagementFacade)
                        ? _management
                        : null;
    }
}
