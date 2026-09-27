namespace Hive.Host.WinForms;

public sealed class HiveWinFormsIntegrationException : Exception
{
    public HiveWinFormsIntegrationException(
        string code,
        string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
