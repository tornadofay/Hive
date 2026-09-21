using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed partial class ThemeFoundationExampleForm
{
    private readonly List<CrudExampleItem> _crudExampleItems =
    [
        new(1, "Example provider", "Provider", "Enabled"),
        new(2, "Example agent", "Agent", "Enabled"),
        new(3, "Example resource", "Resource", "Ready"),
        new(4, "Example workspace", "Workspace", "Ready"),
        new(5, "Example execution", "Execution", "Running")
    ];

    private int _nextCrudExampleId = 6;

    private void BuildCrudCompositionExample()
    {
        var crud = new HiveCrudPage<CrudExampleItem>
        {
            Width = 720,
            Height = 400,
            Margin = new Padding(0, 18, 0, 12),
            Title = "Generic CRUD composition",
            Description = "Hive owns list/action orchestration; the consumer supplies columns, load/edit/delete callbacks, and its specialized editor."
        };

        crud.SetColumns(
            new HiveCrudColumn<CrudExampleItem>(
                "Name",
                240,
                item => item.Name),
            new HiveCrudColumn<CrudExampleItem>(
                "Type",
                150,
                item => item.Type),
            new HiveCrudColumn<CrudExampleItem>(
                "Status",
                120,
                item => item.Status));

        crud.LoadItemsAsync = LoadCrudExampleItemsAsync;
        crud.EditItemAsync = EditCrudExampleItemAsync;
        crud.DeleteItemAsync = DeleteCrudExampleItemAsync;
        crud.GetItemDisplayName = item => item.Name;
        crud.OperationFailed += CrudOperationFailed;

        _controlsPage.Controls.Add(crud);
        ThemeManager.Apply(crud);

        _ = crud.RefreshAsync();
    }

    private Task<IReadOnlyList<CrudExampleItem>> LoadCrudExampleItemsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CrudExampleItem> snapshot = _crudExampleItems.ToArray();
        return Task.FromResult(snapshot);
    }

    private Task<CrudExampleItem?> EditCrudExampleItemAsync(
        CrudExampleItem? existing,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var dialog = new CrudExampleEditorForm(
            existing,
            ThemeManager);

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return Task.FromResult<CrudExampleItem?>(null);

        var saved = dialog.Item;

        if (existing is null)
        {
            saved = saved with { Id = _nextCrudExampleId++ };
            _crudExampleItems.Add(saved);
        }
        else
        {
            var index = _crudExampleItems.FindIndex(item => item.Id == existing.Id);
            if (index >= 0)
                _crudExampleItems[index] = saved with { Id = existing.Id };
        }

        return Task.FromResult<CrudExampleItem?>(saved);
    }

    private Task DeleteCrudExampleItemAsync(
        CrudExampleItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _crudExampleItems.RemoveAll(existing => existing.Id == item.Id);
        return Task.CompletedTask;
    }

    private void CrudOperationFailed(
        object? sender,
        HiveCrudOperationFailedEventArgs e)
    {
        HiveMessageBox.ShowError(
            this,
            $"The generic CRUD example could not complete the {e.Operation.ToString().ToLowerInvariant()} operation.",
            "CRUD example",
            ThemeManager);
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
                "The editor remains consumer-owned; Hive only supplies the reusable field layout.",
                new Size(760, 460),
                new Size(620, 380),
                themeManager)
        {
            Item = existing ?? new CrudExampleItem(0, string.Empty, string.Empty, "Ready");

            _editorLayout = new HiveEditorLayout
            {
                Dock = DockStyle.Fill
            };

            _name.Text = Item.Name;
            _type.Text = Item.Type;
            _status.DropDownStyle = ComboBoxStyle.DropDownList;
            _status.Items.AddRange(["Ready", "Enabled", "Disabled", "Running"]);
            _status.SelectedItem = Item.Status;

            _editorLayout.AddField(
                "Name",
                "Display name used by the consumer.",
                _name);
            _editorLayout.AddField(
                "Type",
                "Domain-specific type supplied by the consumer.",
                _type);
            _editorLayout.AddField(
                "Status",
                "Consumer-owned status value.",
                _status);

            var save = _editorLayout.AddActionButton(
                "Save",
                HiveButtonStyle.Primary,
                110);
            var cancel = _editorLayout.AddActionButton(
                "Cancel",
                HiveButtonStyle.Secondary,
                110);

            save.Click += (_, _) => Save();
            cancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            BodyPanel.Padding = new Padding(24);
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
