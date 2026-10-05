using Hive.Core;
using System.Text.Json;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

/// <summary>
/// The seam every provider model-catalog shape is parsed through.
/// </summary>
/// <remarks>
/// One implementation exists per provider catalog format. The transport
/// boundary resolves a parser through <see cref="ModelCatalogParserRegistry"/>
/// and hands it the already-read response body plus the rate-limit evidence
/// observed on the HTTP response, so adding a provider shape means adding a
/// parser file rather than editing a large dispatch switch.
/// </remarks>
internal interface IModelCatalogParser
{
    /// <summary>
    /// Parses the provider model-catalog response body.
    /// </summary>
    Result<OpenAICompatibleModelCatalog> Parse(
        string responseJson,
        int? rateLimitRemaining);
}

/// <summary>
/// The failure shape shared by every model-catalog parser.
/// </summary>
internal static class ModelCatalogFailure
{
    internal static Result<OpenAICompatibleModelCatalog> ModelSerializationFailure() =>
        Result<OpenAICompatibleModelCatalog>.Failure(
            new Error(
                "hive.provider.openai-compatible.malformed-model-catalog",
                ErrorCategory.Serialization,
                "The provider returned a malformed or unsupported model catalog."));
}