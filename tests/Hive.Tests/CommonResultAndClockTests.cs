using Hive.Core;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class CommonResultAndClockTests
{
    [Fact]
    public void Error_TrimsValuesAndPreservesCategory()
    {
        var error = new Error("  test.code  ", ErrorCategory.Validation, "  test message  ");

        Assert.Equal("test.code", error.Code);
        Assert.Equal(ErrorCategory.Validation, error.Category);
        Assert.Equal("test message", error.Message);
    }

    [Fact]
    public void Error_RejectsBlankCodeOrMessage()
    {
        Assert.Throws<ArgumentException>(() => new Error(" ", ErrorCategory.Validation, "message"));
        Assert.Throws<ArgumentException>(() => new Error("code", ErrorCategory.Validation, " "));
    }

    [Fact]
    public void Error_RejectsInvalidCategory()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Error(
                "test.code",
                (ErrorCategory)999,
                "test message"));
    }

    [Fact]
    public void Result_SuccessAndFailurePreserveState()
    {
        var success = Result.Success();
        var failure = Result.Failure(Error.Validation("validation", "bad input"));

        Assert.True(success.IsSuccess);
        Assert.False(success.IsFailure);
        Assert.Null(success.Error);

        Assert.False(failure.IsSuccess);
        Assert.True(failure.IsFailure);
        Assert.NotNull(failure.Error);
        Assert.Equal("validation", failure.Error!.Code);
    }

    [Fact]
    public void Result_FailureRejectsNullError()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string>.Failure(null!));
    }

    [Fact]
    public void ResultOfT_SuccessAndFailurePreserveValueAndError()
    {
        var success = Result<string>.Success("value");
        var failure = Result<string>.Failure(Error.Unsupported("unsupported", "not available"));

        Assert.True(success.IsSuccess);
        Assert.Equal("value", success.Value);
        Assert.Null(success.Error);

        Assert.True(failure.IsFailure);
        Assert.Null(failure.Value);
        Assert.Equal(ErrorCategory.Unsupported, failure.Error!.Category);
    }

    [Fact]
    public void Clock_CanBeSuppliedByCallerForDeterministicTime()
    {
        var expected = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        IClock clock = new FakeClock(expected);

        Assert.Equal(expected, clock.UtcNow);
    }
}
