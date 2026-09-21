using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class ControlsCrudExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveCrudPage<CrudExampleItem> _crud;
    private readonly FlowLayoutPanel _controls;
    private readonly Label _description;

    private readonly List<CrudExampleItem> _items =
    [
        new(1, "OpenAI-compatible provider", "Provider", "Enabled"),
        new(2, "Document extraction agent", "Agent", "Enabled"),
        new(3, "Business application", "Resource", "Ready"),
        new(4, "Operations workspace", "Workspace", "Ready"),
        new(5, "Invoice import", "Work item", "Running"),
        new(6, "Vision model target", "Execution target", "Enabled"),
        new(7, "Approval policy", "Policy", "Ready"),
        new(8, "Customer lookup tool", "Tool", "Disabled")
    ];

    private int _nextId = 9;

    public ControlsCrudExampleView(IHiveThemeManager themeManager)
    {
        ArgumentNullException.ThrowIfNull(themeManager);

        _themeManager = themeManager;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        AutoScroll = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 64),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Text = "Native WinForms controls and Hive-specific interaction surfaces share the same theme and state model. The CRUD page below is deliberately sized for a desktop workflow and scrolls cleanly at smaller window sizes."
        };

        _controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0),
            Padding = Padding.Empty
        };

        var input = new TextBox
        {
            Width = 260,
            Height = 30,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Native WinForms input",
            Margin = new Padding(0, 0, 10, 0)
        };

        var enabledCheck = new CheckBox
        {
            AutoSize = true,
            Text = "Enabled",
            Margin = new Padding(0, 7, 14, 0)
        };

        var disabledCheck = new CheckBox
        {
            AutoSize = true,
            Enabled = false,
            Text = "Disabled",
            Margin = new Padding(0, 7, 14, 0)
        };

        var primary = new HiveButton
        {
            Text = "Primary",
            Style = HiveButtonStyle.Primary,
            Width = 110,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
        };

        var secondary = new HiveButton
        {
            Text = "Secondary",
            Style = HiveButtonStyle.Secondary,
            Width = 110,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0)
        };

        _controls.Controls.Add(input);
        _controls.Controls.Add(enabledCheck);
        _controls.Controls.Add(disabledCheck);
        _controls.Controls.Add(primary);
        _controls.Controls.Add(secondary);

        _crud = new HiveCrudPage<CrudExampleItem>
        {
            Dock = DockStyle.Top,
            Width = 860,
            Height = 560,
            MinimumSize = new Size(420, 560),
            Margin = new Padding(0, 20, 0, 12),
            Title = "Generic CRUD composition",
            Description = "Reusable list, search, actions, pagination, selection, keyboard interaction, and editor workflow.",
            PageSize = 5
        };

        _crud.SetColumns(
            new HiveCrudColumn<CrudExampleItem>(
                "Name",
                320,
                item => item.Name),
            new HiveCrudColumn<CrudExampleItem>(
                "Type",
                150,
                item => item.Type),
            new HiveCrudColumn<CrudExampleItem>(
                "Status",
                120,
                item => item.Status));

        _crud.LoadItemsAsync = LoadItemsAsync;
        _crud.EditItemAsync = EditItemAsync;
        _crud.DeleteItemAsync = DeleteItemAsync;
        _crud.GetItemDisplayName = item => item.Name;
        _crud.OperationFailed += CrudOperationFailed;

        root.Controls.Add(_description, 0, 0);
        root.Controls.Add(_controls, 0, 1);
        root.Controls.Add(_crud, 0, 2);

        Controls.Add(root);

        SizeChanged += (_, _) => UpdateCrudWidth();
        _themeManager.Apply(this);
        UpdateCrudWidth();
        _ = _crud.RefreshAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _crud.OperationFailed -= CrudOperationFailed;

        base.Dispose(disposing);
    }

    private void UpdateCrudWidth()
    {
        var availableWidth = ClientSize.Width - 18;
        if (availableWidth > 0)
            _crud.Width = Math.Max(420, availableWidth);
    }

    private Task<IReadOnlyList<CrudExampleItem>> LoadItemsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CrudExampleItem> snapshot = _items.ToArray();
        return Task.FromResult(snapshot);
    }

    private Task<CrudExampleItem?> EditItemAsync(
        CrudExampleItem? existing,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var dialog = new CrudExampleEditorForm(
            existing,
            _themeManager);

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return Task.FromResult<CrudExampleItem?>(null);

        var saved = dialog.Item;

        if (existing is null)
        {
            saved = saved with { Id = _nextId++ };
            _items.Add(saved);
        }
        else
        {
            var index = _items.FindIndex(item => item.Id == existing.Id);
            if (index >= 0)
                _items[index] = saved with { Id = existing.Id };
        }

        return Task.FromResult<CrudExampleItem?>(saved);
    }

    private Task DeleteItemAsync(
        CrudExampleItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.RemoveAll(existing => existing.Id == item.Id);
        return Task.CompletedTask;
    }

    private void CrudOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        HiveMessageBox.Show(
            this,
            new HiveMessageOptions(
                "CRUD operation failed",
                $"The {e.Operation.ToString().ToLowerInvariant()} operation could not be completed.",
                HiveMessageType.Error,
                MessageBoxButtons.OK,
                e.Exception.ToString(),
                DetailsExpanded: true),
            _themeManager);
    }

    private sealed record CrudExampleItem(
        int Id,
        string Name,
        string Type,
        string Status);

    private sealed class CrudExampleEditorForm : HiveForm
    {
        private readonly TextBox _name = new();
        private readonly TextBox _type = new();
        private readonly ComboBox _status = new();
        private readonly HiveEditorLayout _editorLayout;

        public CrudExampleEditorForm(
            CrudExampleItem? existing,
            IHiveThemeManager themeManager)
            : base(
                existing is null ? "Add example item" : "Edit example item",
                "Consumer-owned editor using the shared Hive field rhythm and action alignment.",
                new Size(760, 500),
                new Size(640, 420),
                themeManager)
        {
            Item = existing ??
                new CrudExampleItem(
                    0,
                    string.Empty,
                    string.Empty,
                    "Ready");

            _editorLayout = new HiveEditorLayout
            {
                Dock = DockStyle.Fill
            };

            _name.Text = Item.Name;
            _name.BorderStyle = BorderStyle.FixedSingle;

            _type.Text = Item.Type;
            _type.BorderStyle = BorderStyle.FixedSingle;

            _status.DropDownStyle = ComboBoxStyle.DropDownList;
            _status.Items.AddRange([
                "Ready",
                "Enabled",
                "Disabled",
                "Running"
            ]);
            _status.SelectedItem = Item.Status;

            _editorLayout.AddField(
                "Name",
                "Human-readable name shown in lists and related views.",
                _name);
            _editorLayout.AddField(
                "Type",
                "Consumer-defined resource or capability category.",
                _type);
            _editorLayout.AddField(
                "Status",
                "Current consumer-owned lifecycle or operational state.",
                _status);

            var save = _editorLayout.AddActionButton(
                "Save",
                HiveButtonStyle.Primary);
            var cancel = _editorLayout.AddActionButton(
                "Cancel",
                HiveButtonStyle.Secondary);

            save.Click += (_, _) => Save();
            cancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            BodyPanel.Padding = new Padding(28);
            BodyPanel.Controls.Add(_editorLayout);
            ThemeManager.Apply(_editorLayout);
        }

        public CrudExampleItem Item { get; private set; }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                HiveMessageBox.ShowInformation(
                    this,
                    "Name is required.",
                    "Example item",
                    ThemeManager);
                _name.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(_type.Text))
            {
                HiveMessageBox.ShowInformation(
                    this,
                    "Type is required.",
                    "Example item",
                    ThemeManager);
                _type.Focus();
                return;
            }

            Item = Item with
            {
                Name = _name.Text.Trim(),
                Type = _type.Text.Trim(),
                Status = _status.SelectedItem?.ToString() ?? "Ready"
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
