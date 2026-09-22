using System.Text;

namespace Hive.Core;

public readonly record struct SecretReference(SecretId Id);

public sealed class SecretMaterial : IDisposable
{
    private const int MaxUtf8ByteCount = 64 * 1024;
    private string? _value;

    private SecretMaterial(string value)
    {
        _value = value;
    }

    public static SecretMaterial Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
            throw new ArgumentException(
                "Secret material cannot be empty.",
                nameof(value));

        if (Encoding.UTF8.GetByteCount(value) > MaxUtf8ByteCount)
        {
            throw new ArgumentException(
                $"Secret material cannot exceed {MaxUtf8ByteCount} UTF-8 bytes.",
                nameof(value));
        }

        return new SecretMaterial(value);
    }

    public int Length
    {
        get
        {
            ThrowIfDisposed();
            return _value!.Length;
        }
    }

    public string Reveal()
    {
        ThrowIfDisposed();
        return _value!;
    }

    public override string ToString() => "[REDACTED]";

    public void Dispose()
    {
        _value = null;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _value is null,
            this);
    }
}

public sealed class Secret
{
    public Secret(
        ResourceEnvelope<SecretId> resource,
        string key,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Kind != ResourceKind.Secret)
        {
            throw new ArgumentException(
                "The resource kind must be Secret.",
                nameof(resource));
        }

        if (resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            throw new ArgumentException(
                "A secret must be active.",
                nameof(resource));
        }

        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
        {
            throw new ArgumentException(
                "Secret key must contain between 1 and 100 characters.",
                nameof(key));
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
        {
            throw new ArgumentException(
                "Secret display name must contain between 1 and 200 characters.",
                nameof(displayName));
        }

        Resource = resource;
        Key = key.Trim();
        DisplayName = displayName.Trim();
    }

    public ResourceEnvelope<SecretId> Resource { get; }

    public SecretId Id => Resource.Identity;

    public string Key { get; }

    public string DisplayName { get; }

    public override string ToString() =>
        $"Secret {Id} ({Key}) [REDACTED]";
}

public sealed record SecretReadResult(
    Secret Secret,
    SecretMaterial Material);
