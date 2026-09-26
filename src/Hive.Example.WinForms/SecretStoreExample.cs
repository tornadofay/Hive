using Hive.Persistence;
using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal sealed class SecretStoreExample : IHiveExample
{
    public string Category => "Providers";

    public string Subcategory => "Security";

    public int Order => 20;

    public string Title => "DPAPI Secret Store";

    public UserControl CreateView(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new SecretStoreExampleView(
            HiveDatabaseOptions.LocalDevelopment(),
            services.GetExampleOutput());
    }
}
