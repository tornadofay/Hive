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
    private readonly HiveCrudPage<AgentDefinition> _page;

    private IReadOnlyList<ExecutionTarget> _targets = Array.Empty<ExecutionTarget>();

    public HiveAgentSettingsView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

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
                "Key",
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
        var targets = await _management
            .ListExecutionTargetsAsync(
                _accessContext,
                cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        if (targets.IsFailure)
            throw new InvalidOperationException(targets.Error!.Message);

        _targets = targets.Value!;

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
            _themeManager);

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
        HiveMessageBox.ShowError(
            FindForm(),
            message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _page.OperationFailed -= PageOperationFailed;

        base.Dispose(disposing);
    }
}
