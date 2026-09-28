namespace Hive.Management;

public sealed record ProviderSettingsOperationResult(
    Hive.Core.Provider Provider,
    int EndpointsAttempted,
    int EndpointsSucceeded,
    int ModelsDiscovered,
    int AutomaticTargetsCreated,
    int AutomaticTargetsReactivated,
    int AutomaticTargetsRetired,
    IReadOnlyList<Hive.Core.Error> DiscoveryErrors)
{
    public bool HasDiscoveryErrors => DiscoveryErrors.Count != 0;

    public int ActiveAutomaticTargetCount { get; init; }
}
