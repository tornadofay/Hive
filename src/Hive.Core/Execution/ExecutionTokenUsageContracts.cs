using System.Collections.ObjectModel;

namespace Hive.Core;

public enum TokenUsageEvidence
{
    Actual,
    Estimated,
    Unknown
}

public sealed record ExecutionTokenUsage
{
    public const int MaxAdditionalCountEntries = 32;
    public const int MaxAdditionalCountKeyLength = 128;

    public ExecutionTokenUsage(
        TokenUsageEvidence evidence,
        long? inputTokenCount = null,
        long? outputTokenCount = null,
        long? totalTokenCount = null,
        long? cachedInputTokenCount = null,
        long? reasoningTokenCount = null,
        IReadOnlyDictionary<string, long>? additionalCounts = null)
    {
        if (!Enum.IsDefined(evidence))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidence),
                evidence,
                "Token usage evidence is invalid.");
        }

        ValidateCount(inputTokenCount, nameof(inputTokenCount));
        ValidateCount(outputTokenCount, nameof(outputTokenCount));
        ValidateCount(totalTokenCount, nameof(totalTokenCount));
        ValidateCount(cachedInputTokenCount, nameof(cachedInputTokenCount));
        ValidateCount(reasoningTokenCount, nameof(reasoningTokenCount));

        var normalizedAdditional = NormalizeAdditionalCounts(additionalCounts);
        var hasKnownValue =
            inputTokenCount is not null ||
            outputTokenCount is not null ||
            totalTokenCount is not null ||
            cachedInputTokenCount is not null ||
            reasoningTokenCount is not null ||
            normalizedAdditional.Count != 0;

        if (evidence == TokenUsageEvidence.Actual && !hasKnownValue)
        {
            throw new ArgumentException(
                "Actual token usage requires at least one reported usage value.",
                nameof(evidence));
        }

        if (evidence == TokenUsageEvidence.Unknown && hasKnownValue)
        {
            throw new ArgumentException(
                "Unknown token usage must not contain a known usage value.",
                nameof(evidence));
        }

        Evidence = evidence;
        InputTokenCount = inputTokenCount;
        OutputTokenCount = outputTokenCount;
        TotalTokenCount = totalTokenCount;
        CachedInputTokenCount = cachedInputTokenCount;
        ReasoningTokenCount = reasoningTokenCount;
        AdditionalCounts = normalizedAdditional;
    }

    public static ExecutionTokenUsage Unknown { get; } =
        new(TokenUsageEvidence.Unknown);

    public TokenUsageEvidence Evidence { get; }

    public long? InputTokenCount { get; }

    public long? OutputTokenCount { get; }

    public long? TotalTokenCount { get; }

    public long? CachedInputTokenCount { get; }

    public long? ReasoningTokenCount { get; }

    public IReadOnlyDictionary<string, long> AdditionalCounts { get; }

    public bool HasKnownUsage =>
        Evidence != TokenUsageEvidence.Unknown;

    private static void ValidateCount(long? value, string parameterName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Token usage counts cannot be negative.");
        }
    }

    private static IReadOnlyDictionary<string, long> NormalizeAdditionalCounts(
        IReadOnlyDictionary<string, long>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>(StringComparer.Ordinal));
        }

        if (values.Count > MaxAdditionalCountEntries)
        {
            throw new ArgumentException(
                $"Additional token usage counts cannot contain more than {MaxAdditionalCountEntries} entries.",
                nameof(values));
        }

        var normalized = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException(
                    "Additional token usage count keys are required.",
                    nameof(values));
            }

            var key = pair.Key.Trim();

            if (key.Length > MaxAdditionalCountKeyLength)
            {
                throw new ArgumentException(
                    $"Additional token usage count keys cannot exceed {MaxAdditionalCountKeyLength} characters.",
                    nameof(values));
            }

            if (pair.Value < 0)
            {
                throw new ArgumentException(
                    $"Additional token usage count '{key}' cannot be negative.",
                    nameof(values));
            }

            if (!normalized.TryAdd(key, pair.Value))
            {
                throw new ArgumentException(
                    $"Duplicate additional token usage count '{key}' is not allowed.",
                    nameof(values));
            }
        }

        return new ReadOnlyDictionary<string, long>(normalized);
    }
}
