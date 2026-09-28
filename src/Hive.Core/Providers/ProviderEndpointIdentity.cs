namespace Hive.Core;

internal static class ProviderEndpointIdentity
{
    public static bool Equals(
        Uri left,
        Uri right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return Uri.Compare(
                   left,
                   right,
                   UriComponents.SchemeAndServer,
                   UriFormat.SafeUnescaped,
                   StringComparison.OrdinalIgnoreCase) == 0
               &&
               Uri.Compare(
                   left,
                   right,
                   UriComponents.PathAndQuery,
                   UriFormat.SafeUnescaped,
                   StringComparison.Ordinal) == 0;
    }
}
