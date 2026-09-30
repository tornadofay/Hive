namespace Hive.Core;

public readonly record struct EventId
{
    public EventId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static EventId New() => new(Guid.NewGuid());

    public static EventId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? new(parsed)
            : throw new FormatException($"Invalid EventId: '{value}'.");

    public static bool TryParse(string? value, out EventId result)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CorrelationId
{
    public CorrelationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CorrelationId cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static CorrelationId New() => new(Guid.NewGuid());

    public static CorrelationId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? new(parsed)
            : throw new FormatException($"Invalid CorrelationId: '{value}'.");

    public static bool TryParse(string? value, out CorrelationId result)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CausationId
{
    public CausationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CausationId cannot be empty.", nameof(value));

        Value = value;
    }

    public Guid Value { get; }

    public static CausationId New() => new(Guid.NewGuid());

    public static CausationId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? new(parsed)
            : throw new FormatException($"Invalid CausationId: '{value}'.");

    public static bool TryParse(string? value, out CausationId result)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}
