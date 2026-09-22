using System.Diagnostics;
using Hive.Core;

namespace Hive.Providers.OpenAICompatible;

public sealed class OpenAICompatibleProviderConnectionTester : IProviderConnectionTester
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    public async Task<Result<ProviderConnectionTestResult>> TestAsync(
        Provider provider,
        ProviderAccount account,
        ExecutionTarget target,
        SecretMaterial? credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(target);

        if (!string.Equals(
                provider.TransportKind,
                "openai-compatible",
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<ProviderConnectionTestResult>.Failure(
                Error.Unsupported(
                    "hive.provider.connection-test.unsupported-transport",
                    $"Provider transport '{provider.TransportKind}' does not use the OpenAI-compatible connection tester."));
        }

        if (target.Model is null && target.Deployment is null)
        {
            return Result<ProviderConnectionTestResult>.Failure(
                Error.Validation(
                    "hive.provider.connection-test.model-required",
                    "The execution target must define a model or deployment."));
        }

        var model = target.Model ?? target.Deployment!;

        using var httpClient = new HttpClient
        {
            Timeout = TestTimeout
        };

        var options = new OpenAICompatibleProviderOptions(
            target.Endpoint,
            credential,
            TestTimeout);

        var adapter = new OpenAICompatibleProviderAdapter(
            httpClient,
            options);

        var request = new OpenAICompatibleChatRequest(
            model,
            [
                new OpenAICompatibleMessage(
                    OpenAICompatibleMessageRole.User,
                    "Reply with OK.")
            ]);

        var stopwatch = Stopwatch.StartNew();

        var result = await adapter
            .CompleteChatAsync(
                request,
                cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        if (result.IsFailure)
            return Result<ProviderConnectionTestResult>.Failure(result.Error!);

        return Result<ProviderConnectionTestResult>.Success(
            new ProviderConnectionTestResult(
                provider.Key,
                target.Key,
                result.Value!.Model,
                stopwatch.Elapsed,
                "OpenAI-compatible provider connection and model request succeeded."));
    }
}
