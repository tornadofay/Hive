using System.Drawing;
using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms;

internal sealed class HiveAgentDefinitionEditorForm : HiveForm
{
    private readonly AgentDefinition? _existing;
    private readonly IReadOnlyList<ExecutionTarget> _targets;
    private readonly IHiveExampleOutput? _output;
    private readonly TextBox _keyTextBox;
    private readonly TextBox _displayNameTextBox;
    private readonly ComboBox _generationComboBox;
    private readonly ComboBox _targetComboBox;
    private readonly HiveButton _saveButton;
    private readonly HiveButton _cancelButton;

    public HiveAgentDefinitionEditorForm(
        AgentDefinition? definition,
        IReadOnlyList<ExecutionTarget> targets,
        IHiveThemeManager themeManager,
        IHiveExampleOutput? output = null)
        : base(
            definition is null ? "New Agent" : "Edit Agent",
            "Agent definition and configured execution target",
            new Size(760, 560),
            new Size(640, 480),
            themeManager)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(themeManager);

        _existing = definition;
        _targets = targets;
        _output = output;

        ConfigureHeader(
            allowMove: true,
            allowClose: true,
            allowMinimize: false,
            allowMaximize: false,
            allowHelp: false,
            allowThemeToggle: true);

        SetBodyPadding(new Padding(20));

        var editor = new HiveEditorLayout();

        _keyTextBox = CreateTextBox();
        _keyTextBox.PlaceholderText = "e.g. support-agent";
        _displayNameTextBox = CreateTextBox();
        _displayNameTextBox.PlaceholderText = "e.g. Customer Support Agent";

        _generationComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var generation in Enum.GetValues<AgentGeneration>())
            _generationComboBox.Items.Add(generation);

        _targetComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        _targetComboBox.Items.Add(
            new TargetChoice(
                null,
                "Unconfigured — no execution target"));

        foreach (var target in _targets)
        {
            var status = target.Resource.Lifecycle.Status;
            var suffix = status == ResourceLifecycleStatus.Active
                ? target.Key
                : $"{target.Key} — {status}";

            _targetComboBox.Items.Add(
                new TargetChoice(
                    target.Id,
                    $"{target.DisplayName} [{suffix}]"));
        }

        _keyTextBox.Text = definition?.Key ?? string.Empty;
        _displayNameTextBox.Text = definition?.DisplayName ?? string.Empty;
        _generationComboBox.SelectedItem =
            definition?.Generation ?? AgentGeneration.Base;
        SelectTarget(definition?.ConfiguredExecutionTargetId);

        if (definition is not null)
        {
            SetReadOnlyVisualState(_keyTextBox, themeManager);
        }

        editor.AddField(
            "Resource key",
            "Stable internal AgentDefinition identifier. It becomes read-only after creation.",
            _keyTextBox);

        editor.AddField(
            "Display name",
            "Human-readable Agent name shown in host Settings and selection.",
            _displayNameTextBox);

        editor.AddField(
            "Generation",
            "Agent generation contract. Runtime promotion or demotion is not performed here.",
            _generationComboBox);

        editor.AddField(
            "Execution target",
            "Optional explicit target reference. Provider, account, endpoint, model, and credentials remain owned by the referenced ExecutionTarget.",
            _targetComboBox,
            72);

        _cancelButton = editor.AddActionButton(
            "Cancel",
            HiveButtonStyle.Secondary,
            96);
        _saveButton = editor.AddActionButton(
            definition is null ? "Create" : "Save",
            HiveButtonStyle.Primary,
            96);

        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _saveButton.Click += (_, _) => Save();

        BodyPanel.Controls.Add(editor);
        ThemeManager.Apply(BodyPanel);
    }

    public AgentDefinition? Definition { get; private set; }

    private void Save()
    {
        try
        {
            var key = _keyTextBox.Text.Trim();
            var displayName = _displayNameTextBox.Text.Trim();

            if (_generationComboBox.SelectedItem is not AgentGeneration generation)
                throw new InvalidOperationException("A valid Agent generation is required.");

            var targetId = _targetComboBox.SelectedItem is TargetChoice choice
                ? choice.Id
                : null;

            Definition = _existing is null
                ? new AgentDefinition(
                    key,
                    displayName,
                    generation,
                    targetId)
                : _existing
                    .WithDisplayName(displayName)
                    .WithGeneration(generation)
                    .WithConfiguredExecutionTarget(targetId);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            HiveUiErrorReporter.Report(
                this,
                exception,
                "Agent",
                "The Agent could not be saved.",
                _output,
                ThemeManager);
        }
    }

    private void SelectTarget(ExecutionTargetId? targetId)
    {
        var index = 0;

        if (targetId is not null)
        {
            for (var i = 1; i < _targetComboBox.Items.Count; i++)
            {
                if (_targetComboBox.Items[i] is TargetChoice choice &&
                    choice.Id == targetId)
                {
                    index = i;
                    break;
                }
            }
        }

        _targetComboBox.SelectedIndex = index;
    }

    private static TextBox CreateTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle
        };

    private static void SetReadOnlyVisualState(
        TextBox textBox,
        IHiveThemeManager themeManager)
    {
        textBox.ReadOnly = true;
        textBox.TabStop = false;
        textBox.Cursor = Cursors.Arrow;
        textBox.BackColor = themeManager.Theme.Palette.DisabledBackground;
        textBox.ForeColor = themeManager.Theme.Palette.DisabledText;
    }

    private sealed record TargetChoice(
        ExecutionTargetId? Id,
        string Display)
    {
        public override string ToString() => Display;
    }
}
