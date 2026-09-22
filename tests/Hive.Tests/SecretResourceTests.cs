using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class SecretResourceTests
{
    [Fact]
    public void SecretMaterial_IsRedactedAndDisposed()
    {
        using var material = SecretMaterial.Create("test-secret-value");

        Assert.Equal("[REDACTED]", material.ToString());
        Assert.Equal("test-secret-value", material.Reveal());
        Assert.Equal("test-secret-value".Length, material.Length);

        material.Dispose();

        Assert.Throws<ObjectDisposedException>(() => material.Reveal());
        Assert.Throws<ObjectDisposedException>(() => _ = material.Length);
    }

    [Fact]
    public void SecretMaterial_EnforcesNonEmptyAndByteLimit()
    {
        Assert.Throws<ArgumentException>(
            () => SecretMaterial.Create(string.Empty));

        var boundary = SecretMaterial.Create(
            new string('x', 64 * 1024));

        boundary.Dispose();

        Assert.Throws<ArgumentException>(
            () => SecretMaterial.Create(
                new string('x', (64 * 1024) + 1)));
    }

    [Fact]
    public void Secret_RejectsWrongKindAndInvalidIdentityFields()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;

        var envelope = new ResourceEnvelope<SecretId>(
            ResourceKind.Provider,
            SecretId.New(),
            principal,
            ResourceScope.Tenant(tenant),
            ResourceVersion.Initial,
            new ResourceProvenance(
                principal,
                now,
                CorrelationId.New()),
            ResourceLifecycle.Active(now));

        Assert.Throws<ArgumentException>(
            () => new Secret(
                envelope,
                "secret",
                "Secret"));

        Assert.Throws<ArgumentException>(
            () => new Secret(
                new ResourceEnvelope<SecretId>(
                    ResourceKind.Secret,
                    SecretId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                string.Empty,
                "Secret"));

        Assert.Throws<ArgumentException>(
            () => new Secret(
                new ResourceEnvelope<SecretId>(
                    ResourceKind.Secret,
                    SecretId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                "secret",
                string.Empty));
    }

    [Fact]
    public void Secret_ToString_NeverContainsMaterial()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;

        var secret = new Secret(
            new ResourceEnvelope<SecretId>(
                ResourceKind.Secret,
                SecretId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "database-password",
            "Database Password");

        var text = secret.ToString();

        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("database-password-value", text, StringComparison.Ordinal);
    }
}
