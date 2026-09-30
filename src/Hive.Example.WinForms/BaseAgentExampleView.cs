using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class BaseAgentExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public BaseAgentExampleView(IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run Agent example"
        };

        _surface.SetInformation(
            "Creates a base Agent through AgentFactory, then creates two independent RuntimeInstance objects and one Execution in each. No persistence, provider call, MAF workflow, or cognitive behavior is involved.",
            "The two runtimes have different identities but the same Agent owner and generation. Completing one execution does not change the other runtime's execution state.",
            "Generation",
            "Base Agent only; CognitiveAgent is a later explicit generation");

        _surface.CodeSnippet = """
            var factory = new AgentFactory(authorizer);
            var agent = factory.Create<Agent>(definition, context);
            var runtime = agent.Value.CreateRuntimeInstance();
            var execution = runtime.StartExecution();
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private Task RunExampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var definition = new AgentDefinition(
            "example-base-agent",
            "Example Base Agent",
            AgentGeneration.Base);

        var context = new AgentCreationContext(
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New()));

        var factory = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer());

        var agentResult = factory.Create<Agent>(
            definition,
            context);

        EnsureSuccess(agentResult, "Agent creation");

        var agent = agentResult.Value!;
        var firstRuntime = agent.CreateRuntimeInstance();
        var secondRuntime = agent.CreateRuntimeInstance();

        var firstExecutionResult = firstRuntime.StartExecution();
        var secondExecutionResult = secondRuntime.StartExecution();

        EnsureSuccess(firstExecutionResult, "First execution creation");
        EnsureSuccess(secondExecutionResult, "Second execution creation");

        var firstExecution = firstExecutionResult.Value!;
        var secondExecution = secondExecutionResult.Value!;

        var completedFirst = firstExecution.Complete();
        EnsureSuccess(completedFirst, "First execution completion");

        if (secondExecution.Status != ExecutionStatus.Running)
        {
            throw new InvalidOperationException(
                "Runtime isolation check failed: the second execution changed when the first completed.");
        }

        _output.Write(
            "Base Agent / AgentFactory Example",
            $"""
            Agent: {agent.Id}
            Definition: {agent.Definition.Key}; generation={agent.Generation}
            Runtime 1: {firstRuntime.Id}; status={firstRuntime.Status}
            Runtime 2: {secondRuntime.Id}; status={secondRuntime.Status}
            Runtime identities distinct: {firstRuntime.Id != secondRuntime.Id}
            Execution 1: {firstExecution.Id}; final={completedFirst.Value!.Status}
            Execution 2: {secondExecution.Id}; unchanged={secondExecution.Status == ExecutionStatus.Running}
            """);

        return Task.CompletedTask;
    }

    private static void EnsureSuccess<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
    }
}