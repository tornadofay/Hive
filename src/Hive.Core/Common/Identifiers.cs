namespace Hive.Core;

public readonly record struct EventId(Guid Value)
{
    public static EventId New() => new(Guid.NewGuid());

    public static EventId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? new(parsed)
            : throw new FormatException($"Invalid EventId: '{value}'.");

    public static bool TryParse(string? value, out EventId result)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Guid.NewGuid());

    public static CorrelationId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? new(parsed)
            : throw new FormatException($"Invalid CorrelationId: '{value}'.");

    public static bool TryParse(string? value, out CorrelationId result)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CausationId(Guid Value)
{
    public static CausationId New() => new(Guid.NewGuid());

    public static CausationId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? new(parsed)
            : throw new FormatException($"Invalid CausationId: '{value}'.");

    public static bool TryParse(string? value, out CausationId result)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}
