using System.Collections.Concurrent;

namespace Hive.Tests.TestInfrastructure;

internal sealed record FakeProviderRequest(
    string Operation,
    string Input);

internal sealed record FakeProviderResponse(
    string Output);

internal sealed class FakeProvider
{
    private readonly ConcurrentQueue<FakeProviderRequest> _requests = new();

    public IReadOnlyList<FakeProviderRequest> Requests => _requests.ToArray();

    public FakeProviderResponse Response { get; set; } =
        new("fake-response");

    public Exception? Failure { get; set; }

    public TimeSpan Delay { get; set; }

    public async Task<FakeProviderResponse> ExecuteAsync(
        FakeProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _requests.Enqueue(request);

        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (Failure is not null)
            throw Failure;

        return Response;
    }
}
