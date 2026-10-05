using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ProviderCatalogExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;
    private readonly HiveExampleTestSurface _surface;

    public ProviderCatalogExampleView(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output ?? throw new ArgumentNullException(nameof(output));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Show provider catalog"
        };

        _surface.SetInformation(
            "Shows the complete built-in Hive provider inventory from static application metadata. The example does not create Provider resources, access credentials, or call any provider endpoint.",
            "The output separates OpenAI-compatible providers from providers that require a native integration, and shows credential mode, endpoint safety classification, discovery profile, and pricing-normalization profile.",
            "Providers / Provider Platform / Built-In Provider Catalog",
            "Uses the in-process static BuiltInProviderCatalog; no database, credential, or external provider call is required.");

        _surface.CodeSnippet = """
            foreach (var provider in BuiltInProviderCatalog.All)
            {
                // Static application metadata only.
                // No Provider resource or external call is created.
            }
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);
        _themeManager.Apply(this);
    }

    private Task RunExampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var providers = BuiltInProviderCatalog.All;
        var compatible = providers.Count(
            static provider => provider.IsOpenAICompatible);
        var native = providers.Count(
            static provider => provider.RequiresNativeIntegration);
        var normalOnboarding = providers.Count(
            static provider => provider.NormalOnboardingSupported);
        var advancedOnly = providers.Count(
            static provider => !provider.NormalOnboardingSupported);

        var lines = providers
            .Select(
                static provider =>
                    $"{provider.Key} | {provider.DisplayName} | " +
                    $"{(provider.IsOpenAICompatible ? "OpenAI-compatible" : "Native integration required")} | " +
                    $"Credential={provider.CredentialRequirement} | " +
                    $"Endpoint={provider.DiscoveryEndpointKind} | " +
                    $"Discovery={provider.DiscoveryProfile} | " +
                    $"Pricing={provider.PricingNormalizationProfile}")
            .ToArray();

        _output.Write(
            "Built-In Provider Catalog",
            string.Join(
                Environment.NewLine,
                [
                    $"Catalog entries: {providers.Count}",
                    $"OpenAI-compatible: {compatible}",
                    $"Native integration required: {native}",
                    $"Normal onboarding supported: {normalOnboarding}",
                    $"Advanced-only configuration: {advancedOnly}",
                    "External provider call: no",
                    "Durable Provider resource created: no",
                    "",
                    "Provider | Integration | Credential | Endpoint | Discovery | Pricing",
                    .. lines
                ]));

        return Task.CompletedTask;
    }
}
