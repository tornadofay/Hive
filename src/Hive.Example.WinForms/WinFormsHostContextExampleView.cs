using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class WinFormsHostContextExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;
    private readonly HiveListView _list;
    private readonly Label _status;
    private readonly HiveButton _discoverButton;
    private readonly HiveButton _imageButton;

    public WinFormsHostContextExampleView(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output ?? throw new ArgumentNullException(nameof(output));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1000, 80),
            Margin = Padding.Empty,
            Text = "This example demonstrates the concrete V1 WinForms host-context boundary and the existing image submission contract. Discovery returns bounded read-only metadata snapshots; it never exposes control references or action authority."
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 12),
            Padding = Padding.Empty
        };

        _discoverButton = new HiveButton
        {
            Text = "Discover host context",
            Style = HiveButtonStyle.Primary,
            Width = 156,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
        };

        _imageButton = new HiveButton
        {
            Text = "Validate image fixture",
            Style = HiveButtonStyle.Secondary,
            Width = 156,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
        };

        _status = new Label
        {
            AutoSize = false,
            Width = 480,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        _discoverButton.Click += DiscoverButtonClick;
        _imageButton.Click += ValidateImageFixture;

        actions.Controls.Add(_discoverButton);
        actions.Controls.Add(_imageButton);
        actions.Controls.Add(_status);

        _list = new HiveListView
        {
            Dock = DockStyle.Fill,
            AccessibleName = "WinForms host context",
            AccessibleRole = AccessibleRole.Table
        };
        _list.Columns.Add("Path", 80);
        _list.Columns.Add("Runtime type", 260);
        _list.Columns.Add("Name", 160);
        _list.Columns.Add("Text", 220);
        _list.Columns.Add("Visible", 72);
        _list.Columns.Add("Enabled", 72);
        _list.Columns.Add("Data source", 220);

        root.Controls.Add(description, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(_list, 0, 2);

        Controls.Add(root);

        _themeManager.Apply(this);
        _status.Text = "Ready. No host context has been captured.";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _discoverButton.Click -= DiscoverButtonClick;
            _imageButton.Click -= ValidateImageFixture;
        }

        base.Dispose(disposing);
    }

    private async void DiscoverButtonClick(object? sender, EventArgs e) =>
        await DiscoverAsync();

    private async Task DiscoverAsync()
    {
        SetBusy(true);

        try
        {
            using var fixture = CreateFixtureForm();
            using var context = new HiveWinFormsHostContext(
                new ResourceAccessContext(
                    DeploymentId.New(),
                    TenantId.New(),
                    PrincipalId.New()));

            using var registration = context.Register(fixture);
            var result = await context.CaptureAsync(registration);

            if (result.IsFailure)
            {
                _status.Text = $"{result.Error!.Code}: {result.Error.Message}";
                return;
            }

            var snapshot = result.Value!;
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();

                foreach (var control in snapshot.Controls)
                {
                    var row = new ListViewItem(control.Path);
                    row.SubItems.Add(control.RuntimeType);
                    row.SubItems.Add(control.Name ?? "—");
                    row.SubItems.Add(control.Text ?? "—");
                    row.SubItems.Add(control.Visible ? "Yes" : "No");
                    row.SubItems.Add(control.Enabled ? "Yes" : "No");
                    row.SubItems.Add(control.DataSourceType ?? "—");
                    _list.Items.Add(row);
                }
            }
            finally
            {
                _list.EndUpdate();
            }

            _status.Text =
                $"{snapshot.ControlCount} controls discovered. Capture {snapshot.Provenance.CaptureId:N}.";
            _output.Write(
                "WinForms context",
                $"Discovered {snapshot.ControlCount} controls from {snapshot.RootRuntimeType}.");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Discovery cancelled.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Discovery failed: {exception.Message}";
            _output.Write(
                "WinForms context error",
                exception.ToString());
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ValidateImageFixture(object? sender, EventArgs e)
    {
        try
        {
            var content = ReadEmbeddedFixture();
            var submission = new WorkItemImageSubmission(
                "Phase13Sample.svg",
                "image/svg+xml",
                content);

            _status.Text =
                $"Image fixture valid: {submission.Content.Length:N0} bytes.";

            _output.Write(
                "Image input",
                $"Valid image submission: {submission.FileName}, {submission.MediaType}, {submission.Content.Length:N0} bytes.");
        }
        catch (Exception exception)
        {
            _status.Text = $"Image fixture invalid: {exception.Message}";
            _output.Write(
                "Image input error",
                exception.ToString());
        }
    }

    private void SetBusy(bool busy)
    {
        _discoverButton.Enabled = !busy;
        _imageButton.Enabled = !busy;
        Cursor = busy
            ? Cursors.WaitCursor
            : Cursors.Default;
    }

    private static byte[] ReadEmbeddedFixture()
    {
        var assembly = typeof(WinFormsHostContextExampleView).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .First(name => name.EndsWith(
                "Fixtures.Phase13Sample.svg",
                StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                "The Phase 1.13 image fixture could not be loaded.");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Form CreateFixtureForm()
    {
        var form = new Form
        {
            Name = "hostContextFixture",
            Text = "Host Context Fixture"
        };

        var container = new Panel
        {
            Name = "invoicePanel"
        };

        var textBox = new TextBox
        {
            Name = "invoiceNumber",
            Text = "INV-001"
        };

        var password = new TextBox
        {
            Name = "credential",
            UseSystemPasswordChar = true,
            Text = "not-exposed"
        };

        var grid = new DataGridView
        {
            Name = "invoiceGrid"
        };

        var binding = new BindingSource
        {
            DataSource = new[]
            {
                new
                {
                    Id = 1,
                    Name = "Invoice"
                }
            }
        };
        grid.DataSource = binding;

        container.Controls.Add(textBox);
        container.Controls.Add(password);
        container.Controls.Add(grid);
        form.Controls.Add(container);

        return form;
    }
}
