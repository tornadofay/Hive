using Hive.Core;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelCatalogFailure;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

/// <summary>
/// Maps an <see cref="OpenAICompatibleModelCatalogFormat"/> to the parser that
/// owns that provider catalog shape.
/// </summary>
/// <remarks>
/// The transport boundary performs a registry lookup instead of a large
/// dispatch switch, so supporting another provider catalog shape means adding a
/// parser file and one registry entry rather than editing the adapter.
/// </remarks>
internal static class ModelCatalogParserRegistry
{
    private static readonly Dictionary<
        OpenAICompatibleModelCatalogFormat,
        IModelCatalogParser> Parsers = new()
    {
        [OpenAICompatibleModelCatalogFormat.Standard] =
            new StandardModelCatalogParser(),

        [OpenAICompatibleModelCatalogFormat.OpenRouter] =
            new StandardModelCatalogParser("USD", 1m),

        [OpenAICompatibleModelCatalogFormat.CerebrasOpenRouter] =
            new StandardModelCatalogParser("USD", 1m),

        [OpenAICompatibleModelCatalogFormat.CloudflareOpenRouter] =
            new WrappedModelCatalogParser("result", "USD", 1m),

        [OpenAICompatibleModelCatalogFormat.Gemini] =
            new GeminiModelCatalogParser(),

        [OpenAICompatibleModelCatalogFormat.LmStudio] =
            new LmStudioModelCatalogParser(),

        [OpenAICompatibleModelCatalogFormat.Ollama] =
            new OllamaModelCatalogParser()
    };

    /// <summary>
    /// Resolves the parser for a catalog format, or fails closed when the
    /// format has no registered parser.
    /// </summary>
    public static Result<IModelCatalogParser> Resolve(
        OpenAICompatibleModelCatalogFormat format)
    {
        if (Parsers.TryGetValue(format, out var parser))
            return Result<IModelCatalogParser>.Success(parser);

        return Result<IModelCatalogParser>.Failure(
            new Error(
                "hive.provider.openai-compatible.model-catalog-format-unsupported",
                ErrorCategory.Unsupported,
                "The requested provider model-catalog format has no registered parser."));
    }
}