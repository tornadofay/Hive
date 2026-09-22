namespace Hive.Core;

public readonly record struct ProviderId
{
    public ProviderId(Guid value) => Value = IdentityValue.Require(value, nameof(value));

    public Guid Value { get; }

    public static ProviderId New() => new(Guid.NewGuid());

    public static ProviderId Parse(string value) =>
        new(IdentityValue.Parse(value, nameof(value), nameof(ProviderId)));

    public static bool TryParse(string? value, out ProviderId result)
    {
        if (IdentityValue.TryParse(value, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProviderAccountId
{
    public ProviderAccountId(Guid value) => Value = IdentityValue.Require(value, nameof(value));

    public Guid Value { get; }

    public static ProviderAccountId New() => new(Guid.NewGuid());

    public static ProviderAccountId Parse(string value) =>
        new(IdentityValue.Parse(value, nameof(value), nameof(ProviderAccountId)));

    public static bool TryParse(string? value, out ProviderAccountId result)
    {
        if (IdentityValue.TryParse(value, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ExecutionTargetId
{
    public ExecutionTargetId(Guid value) => Value = IdentityValue.Require(value, nameof(value));

    public Guid Value { get; }

    public static ExecutionTargetId New() => new(Guid.NewGuid());

    public static ExecutionTargetId Parse(string value) =>
        new(IdentityValue.Parse(value, nameof(value), nameof(ExecutionTargetId)));

    public static bool TryParse(string? value, out ExecutionTargetId result)
    {
        if (IdentityValue.TryParse(value, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}
