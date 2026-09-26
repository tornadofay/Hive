using Hive.Agents;
using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class AgentFactoryTests
{
    [Fact]
    public void Create_BaseAgent_PreservesDefinitionGenerationAndIdentity()
    {
        var definition = new AgentDefinition(
            "invoice-agent",
            "Invoice Agent",
            AgentGeneration.Base);
        var principal = PrincipalId.New();
        var context = new AgentCreationContext(
            new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                principal));

        var result = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(definition, context);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotEqual(default, result.Value!.Id);
        Assert.Equal(definition, result.Value.Definition);
        Assert.Equal(AgentGeneration.Base, result.Value.Generation);
    }

    [Fact]
    public void Create_RejectsMissingCreationIdentity()
    {
        var result = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition("agent", "Agent"),
                new AgentCreationContext(
                    new ResourceAccessContext()));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.agent.creation.identity-required",
            result.Error!.Code);
        Assert.Equal(ErrorCategory.Validation, result.Error.Category);
    }

    [Fact]
    public void Create_RejectsUnauthorizedCreation()
    {
        var result = new AgentFactory(
            new DenyAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition("agent", "Agent"),
                CreateContext());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error!.Category);
        Assert.Equal("hive.agent.creation.denied", result.Error.Code);
    }

    [Fact]
    public void Create_RejectsUnsupportedCognitiveGenerationWithoutPromotion()
    {
        var result = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition(
                    "agent",
                    "Agent",
                    AgentGeneration.Cognitive),
                CreateContext());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Unsupported, result.Error!.Category);
        Assert.Equal(
            "hive.agent.creation.generation-not-supported",
            result.Error.Code);
    }

    [Fact]
    public void Create_FromOneDefinitionProducesIsolatedRuntimeInstances()
    {
        var definition = new AgentDefinition("agent", "Agent");
        var factory = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer());
        var agentResult = factory.Create<Agent>(
            definition,
            CreateContext());

        Assert.True(agentResult.IsSuccess, agentResult.Error?.Message);

        var agent = agentResult.Value!;
        var firstRuntime = agent.CreateRuntimeInstance();
        var secondRuntime = agent.CreateRuntimeInstance();

        Assert.NotEqual(firstRuntime.Id, secondRuntime.Id);
        Assert.Equal(agent.Id, firstRuntime.AgentId);
        Assert.Equal(agent.Id, secondRuntime.AgentId);
        Assert.Equal(firstRuntime.Generation, secondRuntime.Generation);

        var firstExecution = firstRuntime.StartExecution();
        var secondExecution = secondRuntime.StartExecution();

        Assert.True(firstExecution.IsSuccess, firstExecution.Error?.Message);
        Assert.True(secondExecution.IsSuccess, secondExecution.Error?.Message);
        Assert.NotEqual(firstExecution.Value!.Id, secondExecution.Value!.Id);
        Assert.Equal(firstRuntime.Id, firstExecution.Value.RuntimeId);
        Assert.Equal(secondRuntime.Id, secondExecution.Value.RuntimeId);

        var completed = firstExecution.Value.Complete();

        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.Equal(
            ExecutionStatus.Succeeded,
            completed.Value!.Status);
        Assert.Equal(
            ExecutionStatus.Running,
            secondExecution.Value.Status);
    }

    [Fact]
    public void Runtime_StopPreventsNewExecution()
    {
        var agentResult = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition("agent", "Agent"),
                CreateContext());

        Assert.True(agentResult.IsSuccess, agentResult.Error?.Message);

        var runtime = agentResult.Value!.CreateRuntimeInstance();
        var stopped = runtime.Stop();

        Assert.True(stopped.IsSuccess, stopped.Error?.Message);
        Assert.Equal(RuntimeInstanceStatus.Stopped, stopped.Value!.Status);

        var execution = stopped.Value.StartExecution();

        Assert.True(execution.IsFailure);
        Assert.Equal(
            ErrorCategory.Validation,
            execution.Error!.Category);
        Assert.Equal(
            "hive.agent.runtime.not-active",
            execution.Error.Code);
    }

    [Fact]
    public void ExecutionLifecycle_RejectsTerminalTransition()
    {
        var agentResult = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition("agent", "Agent"),
                CreateContext());

        Assert.True(agentResult.IsSuccess, agentResult.Error?.Message);

        var executionResult = agentResult.Value!
            .CreateRuntimeInstance()
            .StartExecution();

        Assert.True(executionResult.IsSuccess, executionResult.Error?.Message);

        var completed = executionResult.Value!.Complete();
        var failed = completed.Value!.Fail();

        Assert.True(failed.IsFailure);
        Assert.Equal(
            ErrorCategory.Validation,
            failed.Error!.Category);
        Assert.Equal(
            "hive.agent.execution.invalid-transition",
            failed.Error.Code);
    }

    [Fact]
    public void AgentDefinition_RejectsInvalidGenerationAndText()
    {
        Assert.Throws<ArgumentException>(
            () => new AgentDefinition(" ", "Agent"));

        Assert.Throws<ArgumentException>(
            () => new AgentDefinition("agent", " "));

        Assert.Throws<ArgumentException>(
            () => new AgentDefinition(
                new string('x', 101),
                "Agent"));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AgentDefinition(
                "agent",
                "Agent",
                (AgentGeneration)99));
    }

    private static AgentCreationContext CreateContext() =>
        new(new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New()));

    private sealed class DenyAgentCreationAuthorizer : IAgentCreationAuthorizer
    {
        public Result Authorize(
            AgentDefinition definition,
            AgentCreationContext context) =>
            Result.Failure(
                new Error(
                    "hive.agent.creation.denied",
                    ErrorCategory.Forbidden,
                    "The requested Agent generation is not authorized."));
    }
}