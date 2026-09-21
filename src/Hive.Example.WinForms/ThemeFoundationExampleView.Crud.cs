using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed partial class ThemeFoundationExampleView
{
    private readonly List<CrudExampleItem> _crudExampleItems =
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

    private int _nextCrudExampleId = 9;

    private void BuildCrudCompositionExample()
    {
        var crud = new HiveCrudPage<CrudExampleItem>
        {
            Dock = DockStyle.Top,
            Width = 780,
            Height = 452,
            MinimumSize = new Size(420, 420),
            Margin = new Padding(0, 18, 0, 12),
            Title = "Generic CRUD composition",
            Description = "Hive provides the reusable page workflow and interaction model; the consuming feature provides its data, rules, and editor.",
            PageSize = 5
        };

        crud.SetColumns(
            new HiveCrudColumn<CrudExampleItem>(
                "Name",
                190,
                item => item.Name),
            new HiveCrudColumn<CrudExampleItem>(
                "Type",
                110,
                item => item.Type),
            new HiveCrudColumn<CrudExampleItem>(
                "Status",
                90,
                item => item.Status));

        crud.LoadItemsAsync = LoadCrudExampleItemsAsync;
        crud.EditItemAsync = EditCrudExampleItemAsync;
        crud.DeleteItemAsync = DeleteCrudExampleItemAsync;
        crud.GetItemDisplayName = item => item.Name;
        crud.OperationFailed += CrudOperationFailed;

        _controlsPage.Controls.Add(crud);
        _controlsPage.Resize += (_, _) =>
        {
            var availableWidth = _controlsPage.ClientSize.Width - 12;
            if (availableWidth > 0)
                crud.Width = Math.Max(520, availableWidth);
        };
        _themeManager.Apply(crud);

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
            _themeManager);

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
                "The editor remains consumer-owned; Hive supplies the shared field rhythm and action alignment.",
                new Size(760, 500),
                new Size(640, 420),
                themeManager)
        {
            Item = existing ?? new CrudExampleItem(0, string.Empty, string.Empty, "Ready");

            _editorLayout = new HiveEditorLayout
            {
                Dock = DockStyle.Fill
            };

            _name.Text = Item.Name;
            _name.BorderStyle = BorderStyle.FixedSingle;

            _type.Text = Item.Type;
            _type.BorderStyle = BorderStyle.FixedSingle;

            _status.DropDownStyle = ComboBoxStyle.DropDownList;
            _status.Items.AddRange(["Ready", "Enabled", "Disabled", "Running"]);
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
