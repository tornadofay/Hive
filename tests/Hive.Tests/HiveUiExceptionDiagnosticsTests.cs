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
            "access token: token-secret; client_secret=client-secret; " +
            "Authorization: Bearer bearer-secret; Authorization: Basic basic-secret; " +
            "payload={\"password\":\"json-secret\"}; " +
            "escaped={\"password\":\"escaped\\\"json-secret-tail\"}; " +
            "double-quote=\"foo'quoted-secret\"; single-quote='foo\"single-secret'; " +
            "password=\"unterminated-secret; secret='single-secret-value",
            new InvalidOperationException("secret='inner-secret'"));

        var details = HiveUiExceptionDiagnostics.Format(exception);

        Assert.Contains("InvalidOperationException", details, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-pass", details, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("client-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("basic-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("json-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("json-secret-tail", details, StringComparison.Ordinal);
        Assert.DoesNotContain("quoted-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("single-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("unterminated-secret", details, StringComparison.Ordinal);
        Assert.DoesNotContain("single-secret-value", details, StringComparison.Ordinal);
        Assert.DoesNotContain("inner-secret", details, StringComparison.Ordinal);
        Assert.Contains(
            "payload={\"password\":\"[REDACTED]\"}",
            details,
            StringComparison.Ordinal);
        Assert.Contains(
            "escaped={\"password\":\"[REDACTED]\"}",
            details,
            StringComparison.Ordinal);
        Assert.Equal(14, Count(details, "[REDACTED]"));
    }

    [Fact]
    public void SanitizeMessage_RedactsCredentialContentAndPreservesSafeText()
    {
        var message = "Save failed for user=alice; password=top secret passphrase; client_secret=client value.";

        var sanitized = HiveUiExceptionDiagnostics.SanitizeMessage(message);

        Assert.Contains("Save failed for user=alice", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("top secret passphrase", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("client value", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeMessage_RedactsBraceWrappedCredentialsAndEqualsAuthorization()
    {
        var message =
            "Password={super;secret}; " +
            "secret={nested-secret-value}; " +
            "Authorization=Bearer bearer-secret; " +
            "Authorization=Basic basic-secret; safe=value";

        var sanitized = HiveUiExceptionDiagnostics.SanitizeMessage(message);

        Assert.Contains("Password={[REDACTED]}", sanitized, StringComparison.Ordinal);
        Assert.Contains("secret={[REDACTED]}", sanitized, StringComparison.Ordinal);
        Assert.Contains("Authorization=Bearer [REDACTED]", sanitized, StringComparison.Ordinal);
        Assert.Contains("Authorization=Basic [REDACTED]", sanitized, StringComparison.Ordinal);
        Assert.Contains("safe=value", sanitized, StringComparison.Ordinal);

        Assert.DoesNotContain("super;secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("nested-secret-value", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("basic-secret", sanitized, StringComparison.Ordinal);
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
