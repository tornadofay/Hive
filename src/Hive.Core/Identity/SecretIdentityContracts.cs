namespace Hive.Core;

public readonly record struct SecretId
{
    public SecretId(Guid value) => Value = IdentityValue.Require(value, nameof(value));

    public Guid Value { get; }

    public static SecretId New() => new(Guid.NewGuid());

    public static SecretId Parse(string value) =>
        new(IdentityValue.Parse(value, nameof(value), nameof(SecretId)));

    public static bool TryParse(string? value, out SecretId result)
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
