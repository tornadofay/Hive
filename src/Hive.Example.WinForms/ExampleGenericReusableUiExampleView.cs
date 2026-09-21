using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Example.WinForms;

internal sealed class ExampleGenericReusableUiExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public ExampleGenericReusableUiExampleView(
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _output = services.GetExampleOutput();

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _surface.InputText =
            "Demonstrate the reusable Hive Example UI surface.";

        _surface.CodeSnippet = """
var example = new HiveExampleTestSurface
{
    InputText = "test value",
    CodeSnippet = "var result = await RunExampleAsync(...);"
};

example.SetInformation(
    "Description of the example.",
    "Expected result.",
    "Boundary",
    "Example-specific notes.");

example.ConfigureRun(
    async token =>
    {
        // Only the example-specific test logic lives here.
        await Task.CompletedTask;
    },
    output,
    owner);
""";

        _surface.SetInformation(
            "This is the standard reusable Example UI surface used by ordinary Hive developer examples. It centralizes the repeated Run/Copy actions, editable input, C# reproduction snippet, status handling, description, expected result, cancellation, and exception presentation.",
            "The example-specific view only supplies its test data and test logic. The common UI and execution behavior remain in Hive.Host.WinForms.UI.",
            "Example architecture",
            "Specialized examples such as Theme, Controls & CRUD, and Dialogs can keep their own custom layouts.");

        _surface.ConfigureRun(
            RunDemoAsync,
            _output,
            this);

        Controls.Add(_surface);
    }

    private async Task RunDemoAsync(CancellationToken cancellationToken)
    {
        var input = HiveExampleTestSurface.RequireInput(_surface.InputText);

        cancellationToken.ThrowIfCancellationRequested();

        await Task.Delay(100, cancellationToken);

        _output.Write(
            "EXAMPLE GENERIC REUSABLE UI",
            "Reusable Example surface executed successfully." +
            Environment.NewLine +
            Environment.NewLine +
            "Input:" +
            Environment.NewLine +
            input +
            Environment.NewLine +
            Environment.NewLine +
            "Shared UI supplied by HiveExampleTestSurface:" +
            Environment.NewLine +
            "• Run / busy / completion / cancellation / failure state" +
            Environment.NewLine +
            "• Editable test input" +
            Environment.NewLine +
            "• Copyable C# reproduction snippet" +
            Environment.NewLine +
            "• Description / expected-result / note sections" +
            Environment.NewLine +
            "• Common exception handling" +
            Environment.NewLine +
            "• Shared global Example output");
    }
}
