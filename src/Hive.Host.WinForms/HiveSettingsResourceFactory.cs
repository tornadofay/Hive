using Hive.Core;

namespace Hive.Host.WinForms;

internal static class HiveSettingsResourceFactory
{
    public static ResourceEnvelope<TIdentity> CreateEnvelope<TIdentity>(
        ResourceKind kind,
        TIdentity identity,
        ResourceAccessContext accessContext)
        where TIdentity : struct
    {
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.PrincipalId is not { } principalId)
            throw new InvalidOperationException(
                "A PrincipalId is required to create a Settings resource.");

        var now = DateTimeOffset.UtcNow;
        var scope = accessContext.TenantId is { } tenantId
            ? ResourceScope.Tenant(tenantId)
            : ResourceScope.Global();

        return new ResourceEnvelope<TIdentity>(
            kind,
            identity,
            principalId,
            scope,
            ResourceVersion.Initial,
            new ResourceProvenance(
                principalId,
                now,
                CorrelationId.New()),
            ResourceLifecycle.Active(now));
    }
}
