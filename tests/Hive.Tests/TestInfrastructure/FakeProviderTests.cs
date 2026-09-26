using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class FakeProviderTests
{
    [Fact]
    public async Task FakeProvider_RecordsRequestAndReturnsConfiguredResponse()
    {
        var provider = new FakeProvider
        {
            Response = new FakeProviderResponse("answer")
        };

        var result = await provider.ExecuteAsync(
            new FakeProviderRequest("complete", "hello"));

        Assert.Equal("answer", result.Output);
        var request = Assert.Single(provider.Requests);
        Assert.Equal("complete", request.Operation);
        Assert.Equal("hello", request.Input);
    }

    [Fact]
    public async Task FakeProvider_ReturnsConfiguredFailureWithoutNetworkAccess()
    {
        var provider = new FakeProvider
        {
            Failure = new InvalidOperationException("configured failure")
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.ExecuteAsync(
                new FakeProviderRequest("complete", "hello")));

        Assert.Equal("configured failure", exception.Message);
        Assert.Single(provider.Requests);
    }

    [Fact]
    public async Task FakeProvider_HonorsCancellation()
    {
        var provider = new FakeProvider
        {
            Delay = TimeSpan.FromSeconds(10)
        };

        using var cancellation = new CancellationTokenSource();
        var task = provider.ExecuteAsync(
            new FakeProviderRequest("complete", "hello"),
            cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Single(provider.Requests);
    }
}
