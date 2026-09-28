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

            var capabilityResolution = await _providers
                .GetExecutionTargetCapabilityOverridesAsync(
                    executionTargets,
                    new CapabilityKey("vision"),
                    accessContext,
                    cancellationToken)
                .ConfigureAwait(false);

            if (capabilityResolution.IsFailure)
            {
                return Result<InputPreparationResult>.Failure(
                    capabilityResolution.Error!);
            }

            var preparation = InputPreparationEngine.Prepare(
                submission,
                executionTargets,
                cancellationToken,
                capabilityResolution.Value!.Overrides);

            if (preparation.IsFailure ||
                capabilityResolution.Value.DiscoveryFailures.Count == 0)
            {
                return preparation;
            }

            var preparedImageItemIndexes = preparation.Value!
                .PreparedInputs
                .OfType<PreparedImageInput>()
                .Select(static input => input.ItemIndex)
                .ToHashSet();

            var failures = preparation.Value.Failures.ToList();

            for (var itemIndex = 0; itemIndex < submission.Items.Count; itemIndex++)
            {
                var item = submission.Items[itemIndex];

                if (!item.MediaType.StartsWith(
                        "image/",
                        StringComparison.OrdinalIgnoreCase) ||
                    preparedImageItemIndexes.Contains(itemIndex))
                {
                    continue;
                }

                foreach (var discoveryFailure in capabilityResolution.Value.DiscoveryFailures)
                {
                    failures.Add(
                        new InputPreparationFailure(
                            itemIndex,
                            item.FileName,
                            $"Provider discovery: {discoveryFailure.TargetKey}",
                            discoveryFailure.Error));
                }
            }

            return Result<InputPreparationResult>.Success(
                new InputPreparationResult(
                    preparation.Value.SubmissionId,
                    preparation.Value.PreparedInputs,
                    failures));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return InputPreparationEngine.Prepare(
            submission,
            executionTargets,
            cancellationToken);
    }
}