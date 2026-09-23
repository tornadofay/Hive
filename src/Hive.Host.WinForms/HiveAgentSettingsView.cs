using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class HiveAgentSettingsView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput? _output;
    private readonly HiveCrudPage<AgentDefinition> _page;

    private IReadOnlyList<ExecutionTarget> _targets = Array.Empty<ExecutionTarget>();

    public HiveAgentSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;

        _page = new HiveCrudPage<AgentDefinition>
        {
            Title = "Agents",
            Description =
                "Manage AgentDefinitions and their explicit configured ExecutionTarget references.",
            PageSize = 20,
            AllowAdd = true,
            AllowEdit = true,
            AllowDelete = true,
            ShowRefresh = true,
            ShowSearch = true,
            SearchPlaceholder = "Search agents..."
        };

        _page.SetColumns(
            new HiveCrudColumn<AgentDefinition>(
                "Resource key",
                190,
                item => item.Key),
            new HiveCrudColumn<AgentDefinition>(
                "Name",
                240,
                item => item.DisplayName),
            new HiveCrudColumn<AgentDefinition>(
                "Generation",
                110,
                item => item.Generation.ToString()),
            new HiveCrudColumn<AgentDefinition>(
                "Execution target",
                260,
                GetTargetDisplay),
            new HiveCrudColumn<AgentDefinition>(
                "Lifecycle",
                110,
                item => item.Resource?.Lifecycle.Status.ToString() ?? "Unpersisted"));

        _page.LoadItemsAsync = LoadAsync;
        _page.EditItemAsync = EditAsync;
        _page.DeleteItemAsync = DeleteAsync;
        _page.GetItemDisplayName = item =>
            $"{item.DisplayName} [{item.Key}]";

        _page.OperationFailed += PageOperationFailed;

        Controls.Add(_page);
        _themeManager.Apply(this);
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default) =>
        await _page.RefreshAsync(cancellationToken).ConfigureAwait(true);

    private async Task<IReadOnlyList<AgentDefinition>> LoadAsync(
        CancellationToken cancellationToken)
    {
        var providers = await _management
            .ListProvidersAsync(
                _accessContext,
                includeRetired: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (providers.IsFailure)
            throw new InvalidOperationException(providers.Error!.Message);

        var targets = new List<ExecutionTarget>();

        foreach (var provider in providers.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var accounts = await _management
                .ListProviderAccountsAsync(
                    provider.Id,
                    _accessContext,
                    includeRetired: true,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            if (accounts.IsFailure)
                throw new InvalidOperationException(accounts.Error!.Message);

            foreach (var account in accounts.Value!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var accountTargets = await _management
                    .ListExecutionTargetsAsync(
                        account.Id,
                        _accessContext,
                        includeRetired: true,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(true);

                if (accountTargets.IsFailure)
                    throw new InvalidOperationException(accountTargets.Error!.Message);

                targets.AddRange(accountTargets.Value!);
            }
        }

        _targets = targets;

        var agents = await _management
            .ListAgentDefinitionsAsync(
                _accessContext,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (agents.IsFailure)
            throw new InvalidOperationException(agents.Error!.Message);

        return agents.Value!;
    }

    private async Task<AgentDefinition?> EditAsync(
        AgentDefinition? definition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var editor = new HiveAgentDefinitionEditorForm(
            definition,
            _targets,
            _themeManager,
            _output);

        var owner = FindForm();
        var result = editor.ShowDialog(owner);

        if (result != DialogResult.OK ||
            editor.Definition is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var saveResult = definition is null
            ? await _management.CreateAgentDefinitionAsync(
                editor.Definition,
                _accessContext,
                cancellationToken)
            : await _management.UpdateAgentDefinitionAsync(
                editor.Definition,
                _accessContext,
                cancellationToken);

        if (saveResult.IsFailure)
            throw new InvalidOperationException(saveResult.Error!.Message);

        return saveResult.Value;
    }

    private async Task DeleteAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken)
    {
        var result = await _management
            .DeleteAgentDefinitionAsync(
                definition.Id,
                _accessContext,
                cancellationToken)
            .ConfigureAwait(true);

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error!.Message);
    }

    private string GetTargetDisplay(AgentDefinition definition)
    {
        if (definition.ConfiguredExecutionTargetId is null)
            return "Unconfigured";

        var target = _targets.FirstOrDefault(
            item => item.Id == definition.ConfiguredExecutionTargetId.Value);

        if (target is null)
            return "Missing target";

        var status = target.Resource.Lifecycle.Status;
        return status == ResourceLifecycleStatus.Active
            ? target.DisplayName
            : $"{target.DisplayName} [{status}]";
    }

    private void PageOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        var message = e.Exception.Message;

        _page.SetStatus(message);
        HiveUiErrorReporter.Report(
            FindForm(),
            e.Exception,
            "Agent operation failed",
            "The Agent operation could not be completed.",
            _output,
            _themeManager);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _page.OperationFailed -= PageOperationFailed;

        base.Dispose(disposing);
    }
}
