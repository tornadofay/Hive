using System.Security.Cryptography;
using System.Text;
using Hive.Core;

namespace Hive.Host.WinForms;

internal static class HiveWinFormsCapabilityIdentity
{
    public static HiveHostCapabilityDescriptor Create(
        string key,
        HiveHostCapabilityKind kind,
        string name,
        HiveHostActionKind? action = null) =>
        new(
            CreateId(key),
            kind,
            name,
            action: action);

    public static Guid CreateId(string key)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                "Hive.Host.WinForms.Capability.V1|" + key));

        return new Guid(bytes.AsSpan(0, 16));
    }
}
