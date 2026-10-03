using System.Drawing;
using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveAgentDefinitionEditorForm : HiveForm
{
    private readonly AgentDefinition? _existing;
    private readonly IReadOnlyList<ExecutionTarget> _targets;
    private readonly IReadOnlyList<Provider> _providers;
    private readonly IReadOnlyList<ProviderAccount> _accounts;
    private readonly IReadOnlySet<ExecutionTargetId> _favoriteTargetIds;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveExampleOutput? _output;
    private readonly TextBox _keyTextBox;
    private readonly TextBox _displayNameTextBox;
    private readonly HiveComboBox _generationComboBox;
    private readonly HiveComboBox _providerComboBox;
    private readonly HiveComboBox _accountComboBox;
    private readonly HiveComboBox _targetComboBox;
    private readonly HiveButton _saveButton;
    private readonly HiveButton _cancelButton;
    private bool _updatingFilters;

    public HiveAgentDefinitionEditorForm(
        AgentDefinition? definition,
        IReadOnlyList<ExecutionTarget> targets,
        IReadOnlyList<Provider> providers,
        IReadOnlyList<ProviderAccount> accounts,
        IReadOnlyCollection<ExecutionTargetId>? favoriteTargetIds,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            definition is null ? "New Agent" : "Edit Agent",
            "Agent definition and configured execution target",
            new Size(760, 620),
            new Size(680, 540),
            themeManager)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(themeManager);

        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _existing = definition;
        _targets = targets;
        _providers = providers;
        _accounts = accounts;
        _favoriteTargetIds = (favoriteTargetIds ?? Array.Empty<ExecutionTargetId>()).ToHashSet();
        _output = output;

        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: false,
            allowMaximize: false,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(20));

        var editor = new HiveEditorLayout();

        _keyTextBox = CreateTextBox();
        _keyTextBox.PlaceholderText = "e.g. support-agent";
        _displayNameTextBox = CreateTextBox();
        _displayNameTextBox.PlaceholderText = "e.g. Customer Support Agent";

        _generationComboBox = CreateComboBox();
        foreach (var generation in Enum.GetValues<AgentGeneration>())
            _generationComboBox.Items.Add(generation);

        _providerComboBox = CreateComboBox();
        _accountComboBox = CreateComboBox();
        _targetComboBox = CreateComboBox();

        _providerComboBox.SelectedIndexChanged += ProviderFilterChanged;
        _accountComboBox.SelectedIndexChanged += AccountFilterChanged;

        _keyTextBox.Text = definition?.Key ?? string.Empty;
        _displayNameTextBox.Text = definition?.DisplayName ?? string.Empty;
        _generationComboBox.SelectedItem =
            definition?.Generation ?? AgentGeneration.Base;

        PopulateProviderFilter();
        RefreshAccountFilter();
        RefreshTargetChoices();
        SelectTarget(definition?.ConfiguredExecutionTargetId);

        if (definition is not null)
            SetReadOnlyVisualState(_keyTextBox, themeManager);

        editor.AddField(
            "Resource key",
            "Stable internal AgentDefinition identifier. It becomes read-only after creation.",
            _keyTextBox);

        editor.AddField(
            "Display name",
            "Human-readable Agent name shown in host Settings and selection.",
            _displayNameTextBox);

        editor.AddField(
            "Generation",
            "Agent generation contract. Runtime promotion or demotion is not performed here.",
            _generationComboBox);

        editor.AddField(
            "Provider",
            "Optional filter for the ExecutionTarget list.",
            _providerComboBox);

        editor.AddField(
            "Account",
            "Optional Provider Account filter for the ExecutionTarget list.",
            _accountComboBox);

        editor.AddField(
            "Execution target",
            "Optional explicit target reference. When favorites are configured, they narrow the choices; the existing configured target remains available while editing. Provider, account, endpoint, model, and credentials remain owned by the referenced ExecutionTarget.",
            _targetComboBox,
            86);

        _saveButton = editor.AddActionButton(
            definition is null ? "Create" : "Save",
            HiveButtonStyle.Primary,
            96);
        _cancelButton = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);

        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _saveButton.Click += (_, _) => Save();

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        BodyPanel.Controls.Add(editor);
        ThemeManager.Apply(BodyPanel);
    }

    public AgentDefinition? Definition { get; private set; }

    internal HiveComboBox ProviderSelector => _providerComboBox;
    internal HiveComboBox AccountSelector => _accountComboBox;
    internal HiveComboBox TargetSelector => _targetComboBox;

    private void PopulateProviderFilter()
    {
        _updatingFilters = true;
        try
        {
            _providerComboBox.Items.Clear();
            _providerComboBox.Items.Add(new ProviderChoice(null, "All Providers"));

            foreach (var provider in _providers
                         .Where(provider => _targets.Any(target => target.ProviderId == provider.Id))
                         .OrderBy(provider => provider.DisplayName, StringComparer.Ordinal))
            {
                _providerComboBox.Items.Add(
                    new ProviderChoice(provider, provider.DisplayName));
            }

            _providerComboBox.SelectedIndex = 0;
        }
        finally
        {
            _updatingFilters = false;
        }
    }

    private void RefreshAccountFilter()
    {
        _updatingFilters = true;
        try
        {
            var selectedProvider = GetSelectedProvider();

            _accountComboBox.Items.Clear();
            _accountComboBox.Items.Add(new AccountChoice(null, "All Accounts"));

            foreach (var account in _accounts
                         .Where(account =>
                             selectedProvider is null ||
                             account.ProviderId == selectedProvider.Id)
                         .Where(account => _targets.Any(target => target.ProviderAccountId == account.Id))
                         .OrderBy(account => account.DisplayName, StringComparer.Ordinal))
            {
                _accountComboBox.Items.Add(
                    new AccountChoice(account, account.DisplayName));
            }

            _accountComboBox.SelectedIndex = 0;
        }
        finally
        {
            _updatingFilters = false;
        }
    }

    private void RefreshTargetChoices()
    {
        var selectedProvider = GetSelectedProvider();
        var selectedAccount = GetSelectedAccount();

        _updatingFilters = true;
        try
        {
            var candidates = ExecutionTargetFavoriteFilter.Apply(
                _targets,
                _favoriteTargetIds);

            if (_favoriteTargetIds.Count > 0 &&
                _existing?.ConfiguredExecutionTargetId is { } currentTargetId &&
                candidates.All(target => target.Id != currentTargetId))
            {
                var currentTarget = _targets.FirstOrDefault(
                    target => target.Id == currentTargetId);

                if (currentTarget is not null)
                    candidates = candidates
                        .Append(currentTarget)
                        .ToList();
            }

            if (selectedProvider is not null)
            {
                candidates = candidates
                    .Where(target => target.ProviderId == selectedProvider.Id)
                    .ToList();
            }

            if (selectedAccount is not null)
            {
                candidates = candidates
                    .Where(target => target.ProviderAccountId == selectedAccount.Id)
                    .ToList();
            }

            var currentTargetId = _existing?.ConfiguredExecutionTargetId;

            _targetComboBox.Items.Clear();
            _targetComboBox.Items.Add(
                new TargetChoice(
                    null,
                    "Unconfigured — no execution target"));

            foreach (var target in candidates
                         .OrderBy(target => target.DisplayName, StringComparer.Ordinal)
                         .ThenBy(target => target.Key, StringComparer.Ordinal)
                         .ThenBy(target => target.Id.Value))
            {
                var status = target.Resource.Lifecycle.Status;
                var suffix = status == ResourceLifecycleStatus.Active
                    ? target.Key
                    : $"{target.Key} — {status}";
                var currentSuffix = target.Id == currentTargetId
                    ? " — Current configuration"
                    : string.Empty;

                _targetComboBox.Items.Add(
                    new TargetChoice(
                        target.Id,
                        $"{target.DisplayName} [{suffix}]{currentSuffix}"));
            }
        }
        finally
        {
            _updatingFilters = false;
        }

        SelectTarget(_targetComboBox.SelectedItem is TargetChoice choice
            ? choice.Id
            : _existing?.ConfiguredExecutionTargetId);
    }

    private void ProviderFilterChanged(object? sender, EventArgs e)
    {
        if (_updatingFilters)
            return;

        var previousTargetId = GetSelectedTargetId();

        RefreshAccountFilter();
        RefreshTargetChoices();

        SelectTarget(previousTargetId);
    }

    private void AccountFilterChanged(object? sender, EventArgs e)
    {
        if (_updatingFilters)
            return;

        var previousTargetId = GetSelectedTargetId();
        RefreshTargetChoices();
        SelectTarget(previousTargetId);
    }

    private ExecutionTargetId? GetSelectedTargetId() =>
        _targetComboBox.SelectedItem is TargetChoice choice
            ? choice.Id
            : null;

    private Provider? GetSelectedProvider() =>
        (_providerComboBox.SelectedItem as ProviderChoice)?.Value;

    private ProviderAccount? GetSelectedAccount() =>
        (_accountComboBox.SelectedItem as AccountChoice)?.Value;

    private void Save()
    {
        try
        {
            var key = _keyTextBox.Text.Trim();
            var displayName = _displayNameTextBox.Text.Trim();

            if (_generationComboBox.SelectedItem is not AgentGeneration generation)
                throw new InvalidOperationException("A valid Agent generation is required.");

            var targetId = GetSelectedTargetId();

            Definition = _existing is null
                ? new AgentDefinition(
                    HiveSettingsResourceFactory.CreateEnvelope(
                        ResourceKind.AgentDefinition,
                        AgentDefinitionId.New(),
                        _accessContext),
                    key,
                    displayName,
                    generation,
                    targetId)
                : _existing
                    .WithDisplayName(displayName)
                    .WithGeneration(generation)
                    .WithConfiguredExecutionTarget(targetId);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                this,
                exception,
                "Agent",
                "The Agent could not be saved.",
                _output,
                ThemeManager);
        }
    }

    private void SelectTarget(ExecutionTargetId? targetId)
    {
        var index = 0;

        if (targetId is not null)
        {
            for (var i = 1; i < _targetComboBox.Items.Count; i++)
            {
                if (_targetComboBox.Items[i] is TargetChoice choice &&
                    choice.Id == targetId)
                {
                    index = i;
                    break;
                }
            }
        }

        _targetComboBox.SelectedIndex = index;
    }

    private static HiveComboBox CreateComboBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle
        };

    private static void SetReadOnlyVisualState(
        TextBox textBox,
        IHiveThemeManager themeManager)
    {
        textBox.ReadOnly = true;
        textBox.TabStop = false;
        textBox.Cursor = Cursors.Arrow;
        textBox.BackColor = themeManager.Theme.Palette.ElevatedSurface;
        textBox.ForeColor = themeManager.Theme.Palette.MutedText;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _providerComboBox.SelectedIndexChanged -= ProviderFilterChanged;
            _accountComboBox.SelectedIndexChanged -= AccountFilterChanged;
        }

        base.Dispose(disposing);
    }

    private sealed record ProviderChoice(Provider? Value, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record AccountChoice(ProviderAccount? Value, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record TargetChoice(
        ExecutionTargetId? Id,
        string Display)
    {
        public override string ToString() => Display;
    }
}
