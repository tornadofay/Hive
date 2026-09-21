using System.Windows.Forms;

namespace Hive.Example.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        Application.Run(new ThemeFoundationExampleForm());
    }

    private static void OnThreadException(
        object? sender,
        ThreadExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(e.Exception.ToString());

        using var dialog = new UnhandledExceptionDialog(e.Exception);
        dialog.ShowDialog();
    }

    private sealed class UnhandledExceptionDialog : Form
    {
        public UnhandledExceptionDialog(Exception exception)
        {
            Text = "Hive Example — Unhandled UI exception";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(760, 460);
            MinimumSize = new Size(600, 360);

            var message = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9f),
                Text = exception.ToString(),
                WordWrap = false,
                Margin = Padding.Empty
            };

            var close = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Text = "Close",
                DialogResult = DialogResult.OK
            };

            Controls.Add(message);
            Controls.Add(close);
            AcceptButton = close;
        }
    }
}
