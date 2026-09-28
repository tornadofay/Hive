using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

internal sealed class HiveInputPreparationManagementService :
    HiveManagementServiceBase
{
    private readonly HiveProviderManagementService _providers;

    internal HiveInputPreparationManagementService(
        HiveProviderManagementService providers)
    {
        _providers = providers
            ?? throw new ArgumentNullException(nameof(providers));
    }

    internal async Task<Result<InputPreparationResult>> PrepareInputAsync(
        InputSubmission submission,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
        {
            return Result<InputPreparationResult>.Failure(contextError);
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ExecutionTarget> executionTargets =
            Array.Empty<ExecutionTarget>();

        if (InputPreparationEngine.RequiresVisionTargetRouting(submission))
        {
            var targets = await _providers
                .ListExecutionTargetsAsync(
                    accessContext,
                    includeRetired: false,
                    cancellationToken)
                .ConfigureAwait(false);

            if (targets.IsFailure)
            {
                return Result<InputPreparationResult>.Failure(
                    targets.Error!);
            }

            executionTargets = targets.Value!;

            var capabilityOverrides = await _providers
                .GetExecutionTargetCapabilityOverridesAsync(
                    executionTargets,
                    accessContext,
                    cancellationToken)
                .ConfigureAwait(false);

            if (capabilityOverrides.IsFailure)
            {
                return Result<InputPreparationResult>.Failure(
                    capabilityOverrides.Error!);
            }

            return InputPreparationEngine.Prepare(
                submission,
                executionTargets,
                capabilityOverrides.Value!,
                cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return InputPreparationEngine.Prepare(
            submission,
            executionTargets,
            cancellationToken);
    }
}