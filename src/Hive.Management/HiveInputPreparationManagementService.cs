using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

internal sealed class HiveInputPreparationManagementService :
    HiveManagementServiceBase
{
    private readonly IProviderResourceStore _providerResources;

    internal HiveInputPreparationManagementService(
        IProviderResourceStore providerResources)
    {
        _providerResources = providerResources
            ?? throw new ArgumentNullException(nameof(providerResources));
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
            var targets = await _providerResources
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
        }

        cancellationToken.ThrowIfCancellationRequested();

        return InputPreparationEngine.Prepare(
            submission,
            executionTargets,
            cancellationToken);
    }
}