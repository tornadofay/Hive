using Hive.Core;

namespace Hive.Agents;

internal static class RuntimeProtocolGuard
{
    public static Result Validate(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId)
    {
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.PrincipalId is null ||
            accessContext.DeploymentId is null ||
            accessContext.TenantId is null)
        {
            return Result.Failure(
                Error.Validation(
                    "hive.agent.protocol.identity-required",
                    "Runtime-owned work protocols require deployment, tenant, and principal identity."));
        }

        if (accessContext.AgentId is null ||
            accessContext.AgentId.Value != agentId)
        {
            return Result.Failure(
                new Error(
                    "hive.agent.protocol.agent-mismatch",
                    ErrorCategory.Forbidden,
                    "The access context does not belong to the requested Agent."));
        }

        if (accessContext.RuntimeId is null ||
            accessContext.RuntimeId.Value != runtimeId)
        {
            return Result.Failure(
                new Error(
                    "hive.agent.protocol.runtime-mismatch",
                    ErrorCategory.Forbidden,
                    "The access context does not belong to the requested RuntimeInstance."));
        }

        if (!ResourceScope.Runtime(runtimeId).Matches(accessContext))
        {
            return Result.Failure(
                new Error(
                    "hive.agent.protocol.scope-mismatch",
                    ErrorCategory.Forbidden,
                    "The access context does not satisfy the Runtime resource scope."));
        }

        return Result.Success();
    }

    public static Result ValidateOwner(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        PrincipalId owner)
    {
        var validation = Validate(accessContext, agentId, runtimeId);
        if (validation.IsFailure)
            return validation;

        return accessContext.PrincipalId!.Value == owner
            ? Result.Success()
            : Result.Failure(
                new Error(
                    "hive.agent.protocol.owner-mismatch",
                    ErrorCategory.Forbidden,
                    "The access context principal does not own the requested Agent resource."));
    }
}
