using Hive.Core;

namespace Hive.Providers.OpenAICompatible;

public sealed class OpenAICompatibleProviderException : Exception
{
    public OpenAICompatibleProviderException(Error error)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message)
    {
        Error = error;
    }

    public Error Error { get; }
}
