namespace Hive.Host.WinForms.UI.Controls;

public interface IHiveExampleOutput
{
    void Clear();

    void Write(
        string title,
        string value);

    void Append(string value);
}
