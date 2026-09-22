using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Management;

namespace Hive.Example.WinForms;

internal sealed class HiveSettingsExampleView : UserControl
{
    private readonly IServiceProvider _services;
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _context;
    private readonly HiveSettingsView _settings;
    private readonly HiveButton _captureButton;
    private readonly IHiveExampleOutput _output;

    public HiveSettingsExampleView(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _management = services.GetManagementFacade();
        _output = services.GetExampleOutput();

        _context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        _captureButton = new HiveButton
        {
            Text = "Capture configuration",
            Style = HiveButtonStyle.Secondary,
            Width = 148
        };
        _captureButton.Click += async (_, _) =>
            await CaptureConfigurationAsync();

        var description = new Label
        {
            Dock = DockStyle.Fill,
            Text = "First-class Hive Settings over Hive.Management. Provider credentials are stored through Secret Store; persistence connection testing is non-destructive and never applies migrations.",
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 12, 0)
        };

        var banner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(0, 0, 0, 10)
        };

        banner.Controls.Add(description);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 168,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        actions.Controls.Add(_captureButton);
        banner.Controls.Add(actions);

        _settings = new HiveSettingsView(
            _management,
            _context,
            services.GetThemeManager());

        var content = new Panel
        {
            Dock = DockStyle.Fill
        };
        content.Controls.Add(_settings);

        Controls.Add(content);
        Controls.Add(banner);

        services.GetThemeManager().Apply(this);
    }

    private async Task CaptureConfigurationAsync()
    {
        _captureButton.Enabled = false;

        try
        {
            var configuration = await _management
                .GetPersistenceConfigurationAsync(_context);

            if (configuration.IsFailure)
            {
                _output.Write(
                    "Hive Settings",
                    $"Configuration read failed: {configuration.Error!.Message}");
                return;
            }

            var value = configuration.Value!;
            var output = $"""
            Persistence backend: {value.Backend}
            Server / instance: {value.ServerName}
            Port: {value.Port?.ToString() ?? "default"}
            Database: {value.DatabaseName}
            Authentication: {value.AuthenticationMode}
            SQL user configured: {!string.IsNullOrWhiteSpace(value.UserName)}
            Credential reference configured: {value.CredentialSecret is not null}
            Encrypt: {value.Encrypt}
            Trust server certificate: {value.TrustServerCertificate}
            Create database if missing: {value.CreateDatabaseIfMissing}
            Command timeout: {value.CommandTimeoutSeconds}s
            """;

            var providers = await _management
                .ListProvidersAsync(_context);

            output += Environment.NewLine + Environment.NewLine +
                "Providers:";

            if (providers.IsFailure)
            {
                output += Environment.NewLine +
                    $"  ERROR: {providers.Error!.Message}";
            }
            else
            {
                foreach (var provider in providers.Value!)
                {
                    output += Environment.NewLine +
                        $"  Provider: {provider.DisplayName} [{provider.Key}] | Transport={provider.TransportKind}";

                    var accounts = await _management
                        .ListProviderAccountsAsync(
                            provider.Id,
                            _context);

                    if (accounts.IsFailure)
                    {
                        output += Environment.NewLine +
                            $"    Accounts: ERROR: {accounts.Error!.Message}";
                        continue;
                    }

                    foreach (var account in accounts.Value!)
                    {
                        output += Environment.NewLine +
                            $"    Account: {account.DisplayName} [{account.Key}] | " +
                            $"CredentialReference={account.CredentialSecret is not null}";

                        var targets = await _management
                            .ListExecutionTargetsAsync(
                                account.Id,
                                _context);

                        if (targets.IsFailure)
                        {
                            output += Environment.NewLine +
                                $"      Targets: ERROR: {targets.Error!.Message}";
                            continue;
                        }

                        foreach (var target in targets.Value!)
                        {
                            output += Environment.NewLine +
                                $"      Target: {target.DisplayName} [{target.Key}] | " +
                                $"Endpoint={target.Endpoint.AbsoluteUri} | " +
                                $"Model={target.Model ?? "(none)"} | " +
                                $"Deployment={target.Deployment ?? "(none)"}";
                        }
                    }
                }
            }

            var test = await _management
                .TestPersistenceConnectionAsync(
                    value,
                    _context);

            if (test.IsSuccess)
            {
                output += Environment.NewLine + Environment.NewLine +
                    $"""
                    Persistence connection test: succeeded
                    Database state: {test.Value!.DatabaseState}
                    Current schema: {test.Value.CurrentSchemaVersion?.ToString() ?? "none"}
                    Supported schema: {test.Value.SupportedSchemaVersion}
                    Message: {test.Value.Message}
                    """;
            }
            else
            {
                output += Environment.NewLine + Environment.NewLine +
                    $"Persistence connection test: failed — {test.Error!.Message}";
            }

            _output.Write(
                "Hive Settings / Provider & Persistence",
                output);
        }
        finally
        {
            _captureButton.Enabled = true;
        }
    }
