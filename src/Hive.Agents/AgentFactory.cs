using Hive.Core;

namespace Hive.Agents;

public sealed class AgentFactory
{
    private readonly IAgentCreationAuthorizer _authorizer;

    public AgentFactory(IAgentCreationAuthorizer authorizer)
    {
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public Result<TAgent> Create<TAgent>(
        AgentDefinition definition,
        AgentCreationContext context)
        where TAgent : Agent
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        if (context.AccessContext.DeploymentId is null ||
            context.AccessContext.PrincipalId is null)
        {
            return Result<TAgent>.Failure(
                Error.Validation(
                    "hive.agent.creation.identity-required",
                    "Agent creation requires a deployment and principal identity."));
        }

        if (definition.Generation != AgentGeneration.Base)
        {
            return Result<TAgent>.Failure(
                Error.Unsupported(
                    "hive.agent.creation.generation-not-supported",
                    $"Agent generation '{definition.Generation}' is not supported by the current factory."));
        }

        var authorization = _authorizer.Authorize(definition, context);

        if (authorization.IsFailure)
            return Result<TAgent>.Failure(authorization.Error!);

        if (typeof(TAgent) != typeof(Agent))
        {
            return Result<TAgent>.Failure(
                Error.Unsupported(
                    "hive.agent.creation.type-not-supported",
                    $"Agent type '{typeof(TAgent).FullName}' is not registered for the current generation."));
        }

        var agent = new Agent(
            AgentId.New(),
            definition,
            DateTimeOffset.UtcNow);

        return Result<TAgent>.Success((TAgent)(object)agent);
    }
}

public sealed class AllowBaseAgentCreationAuthorizer : IAgentCreationAuthorizer
{
    public Result Authorize(
        AgentDefinition definition,
        AgentCreationContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        return definition.Generation == AgentGeneration.Base
            ? Result.Success()
            : Result.Failure(
                Error.Unsupported(
                    "hive.agent.creation.generation-not-supported",
                    $"Agent generation '{definition.Generation}' is not supported."));
    }
}