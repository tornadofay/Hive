using Hive.Host.WinForms.UI.Controls;
using Xunit;

namespace Hive.Tests;

public sealed class HiveUiExceptionDiagnosticsTests
{
    [Fact]
    public void Format_RedactsCommonCredentialForms()
    {
        var exception = new InvalidOperationException(
            "server=https://user:secret-pass@example.test; " +
            "Password=super-secret; api_key=api-secret; " +
            "access_token: token-secret; Authorization: Bearer bearer-secret; " +
            "payload={\"password\":\"json-secret\"}",
            new InvalidOperationException("secret='inner-secret'"));

        var details = HiveUiExceptionDiagnostics.Format(exception);

        Assert.Contains("InvalidOperationException", details, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-pass", details, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("json-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("inner-secret", details, StringComparison.Ordinal);
        Assert.Equal(7, Count(details, "[REDACTED]"));
    }

    [Fact]
    public void Format_DoesNotExposeStackTraceOrRawExceptionObject()
    {
        var exception = new InvalidOperationException(
            "password=top-secret");

        var details = HiveUiExceptionDiagnostics.Format(exception);

        Assert.DoesNotContain("at Hive.", details, StringComparison.Ordinal);
        Assert.DoesNotContain(exception.ToString(), details, StringComparison.Ordinal);
        Assert.Contains("password=[REDACTED]", details, StringComparison.Ordinal);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
