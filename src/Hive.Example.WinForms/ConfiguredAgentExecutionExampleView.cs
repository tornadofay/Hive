using Hive.Agents;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Example.WinForms;

internal sealed class ConfiguredAgentExecutionExampleView : UserControl
{
    private readonly HiveExampleServices _services;
    private readonly IHiveExampleOutput _output;
    private readonly Hive.Host.WinForms.UI.Controls.HiveExampleTestSurface _surface;

    public ConfiguredAgentExecutionExampleView(
        HiveExampleServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        _output = services.GetExampleOutput();

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run configured Agent"
        };

        _surface.SetInformation(
            "Runs the AgentDefinition selected by the Example Host through Hive.Management using its persisted ExecutionTarget configuration.",
            "Hive resolves the configured AgentDefinition, Provider, ProviderAccount, ExecutionTarget, and optional Secret Store credential. The example does not create a second database, service graph, provider target, or local fake endpoint.",
            "Configured host scope",
            "One configured AgentDefinition and one normal public-API operation. The selected target is resolved again for every execution.");

        _surface.CodeSnippet = """
            var result = await management.ExecuteConfiguredAgentAsync(
                selectedAgent.Id,
                accessContext,
                "Say hello from the configured Hive Agent.",
                cancellationToken);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(
        CancellationToken cancellationToken)
    {
        var selectedAgent = _services.SelectedAgentDefinition;

        if (selectedAgent is null)
        {
            throw new InvalidOperationException(
                "No configured AgentDefinition is selected. Configure an AgentDefinition in Hive Settings and select it in the Example Host.");
        }

        if (selectedAgent.ConfiguredExecutionTargetId is null)
        {
            throw new InvalidOperationException(
                $"The selected AgentDefinition '{selectedAgent.DisplayName}' has no configured ExecutionTarget. Repair it in Hive Settings.");
        }

        var result = await _services
            .GetManagementFacade()
            .ExecuteConfiguredAgentAsync(
                selectedAgent.Id,
                _services.AccessContext,
                "Say hello from the configured Hive Agent.",
                cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Configured Agent execution failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }

        var execution = result.Value!;

        var target = await _services
            .GetManagementFacade()
            .GetExecutionTargetAsync(
                execution.TargetId,
                _services.AccessContext,
                cancellationToken);

        if (target.IsFailure)
        {
            throw new InvalidOperationException(
                $"The configured ExecutionTarget could not be read after execution: {target.Error?.Code} [{target.Error?.Category}] {target.Error?.Message}");
        }

        var provider = await _services
            .GetManagementFacade()
            .GetProviderAsync(
                target.Value!.ProviderId,
                _services.AccessContext,
                cancellationToken);

        if (provider.IsFailure)
        {
            throw new InvalidOperationException(
                $"The configured Provider could not be read after execution: {provider.Error?.Code} [{provider.Error?.Category}] {provider.Error?.Message}");
        }

        var account = await _services
            .GetManagementFacade()
            .GetProviderAccountAsync(
                target.Value.ProviderAccountId,
                _services.AccessContext,
                cancellationToken);

        if (account.IsFailure)
        {
            throw new InvalidOperationException(
                $"The configured ProviderAccount could not be read after execution: {account.Error?.Code} [{account.Error?.Category}] {account.Error?.Message}");
        }

        _output.Write(
            "Configured Agent Execution",
            $"""
            AgentDefinition: {selectedAgent.Id}; key={selectedAgent.Key}
            Provider: {provider.Value!.Id}; key={provider.Value.Key}
            ProviderAccount: {account.Value!.Id}; key={account.Value.Key}
            ExecutionTarget: {target.Value.Id}; key={target.Value.Key}
            Model: {target.Value.Model ?? target.Value.Deployment}
            Execution: {execution.Execution.Id}; status={execution.Execution.Status}
            Target used: {execution.TargetId}
            Response: {execution.ResponseText}
            Provider response ID: {execution.ProviderResponseId ?? "(none)"}
            Provider credentials: not displayed
            Service graph: current host graph
            LocalDevelopment database: not used by this example
            """);
    }
}
