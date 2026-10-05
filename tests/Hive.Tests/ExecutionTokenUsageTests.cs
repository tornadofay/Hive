using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class ExecutionTokenUsageTests
{
    [Fact]
    public void Actual_PreservesReportedDimensionsWithoutDerivingTotals()
    {
        var usage = new ExecutionTokenUsage(
            TokenUsageEvidence.Actual,
            inputTokenCount: 120,
            outputTokenCount: 45,
            totalTokenCount: 165,
            cachedInputTokenCount: 20,
            reasoningTokenCount: 10,
            additionalCounts: new Dictionary<string, long>
            {
                ["accepted_prediction_tokens"] = 3
            });

        Assert.Equal(TokenUsageEvidence.Actual, usage.Evidence);
        Assert.Equal(120, usage.InputTokenCount);
        Assert.Equal(45, usage.OutputTokenCount);
        Assert.Equal(165, usage.TotalTokenCount);
        Assert.Equal(20, usage.CachedInputTokenCount);
        Assert.Equal(10, usage.ReasoningTokenCount);
        Assert.Equal(3, usage.AdditionalCounts["accepted_prediction_tokens"]);
        Assert.True(usage.HasKnownUsage);
    }

    [Fact]
    public void Estimated_IsRepresentableButDistinctFromActual()
    {
        var usage = new ExecutionTokenUsage(
            TokenUsageEvidence.Estimated,
            totalTokenCount: 165);

        Assert.Equal(TokenUsageEvidence.Estimated, usage.Evidence);
        Assert.Equal(165, usage.TotalTokenCount);
        Assert.True(usage.HasKnownUsage);
    }

    [Fact]
    public void Unknown_HasNoSyntheticZeroCounts()
    {
        var usage = ExecutionTokenUsage.Unknown;

        Assert.Equal(TokenUsageEvidence.Unknown, usage.Evidence);
        Assert.Null(usage.InputTokenCount);
        Assert.Null(usage.OutputTokenCount);
        Assert.Null(usage.TotalTokenCount);
        Assert.Null(usage.CachedInputTokenCount);
        Assert.Null(usage.ReasoningTokenCount);
        Assert.Empty(usage.AdditionalCounts);
        Assert.False(usage.HasKnownUsage);
    }

    [Fact]
    public void Actual_RequiresAtLeastOneReportedValue()
    {
        Assert.Throws<ArgumentException>(
            () => new ExecutionTokenUsage(TokenUsageEvidence.Actual));
    }

    [Fact]
    public void Unknown_RejectsKnownValue()
    {
        Assert.Throws<ArgumentException>(
            () => new ExecutionTokenUsage(
                TokenUsageEvidence.Unknown,
                inputTokenCount: 1));
    }

    [Fact]
    public void Counts_CannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ExecutionTokenUsage(
                TokenUsageEvidence.Actual,
                inputTokenCount: -1));
    }

    [Fact]
    public void AdditionalCounts_AreBounded()
    {
        var values = Enumerable.Range(
                0,
                ExecutionTokenUsage.MaxAdditionalCountEntries + 1)
            .ToDictionary(
                static index => $"token_{index}",
                static index => (long)index);

        Assert.Throws<ArgumentException>(
            () => new ExecutionTokenUsage(
                TokenUsageEvidence.Actual,
                totalTokenCount: 1,
                additionalCounts: values));
    }

    [Fact]
    public void AdditionalCounts_AreNormalizedToAnImmutableReadOnlyDictionary()
    {
        var values = new Dictionary<string, long>
        {
            [" provider_tokens "] = 7
        };

        var usage = new ExecutionTokenUsage(
            TokenUsageEvidence.Actual,
            totalTokenCount: 7,
            additionalCounts: values);

        Assert.Equal(7, usage.AdditionalCounts["provider_tokens"]);

        values["provider_tokens"] = 99;

        Assert.Equal(7, usage.AdditionalCounts["provider_tokens"]);
        Assert.Throws<NotSupportedException>(
            () => ((ICollection<KeyValuePair<string, long>>)usage.AdditionalCounts)
                .Add(new KeyValuePair<string, long>("x", 1)));
    }
}
