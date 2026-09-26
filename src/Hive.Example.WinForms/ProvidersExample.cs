using Hive.Persistence;
using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class ProvidersExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Provider Platform";

    public int Order => 10;

    public string Title => "Provider / ProviderAccount / ExecutionTarget";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ProvidersExampleView(
            HiveDatabaseOptions.LocalDevelopment(),
            services.GetExampleOutput());
    }
}
