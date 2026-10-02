using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

public class HostComboBox : ComboBox, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}


public class HostTextBox : TextBox, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HostCheckBox : CheckBox, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HostDateTimePicker : DateTimePicker, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HostNumericUpDown : NumericUpDown, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HostDataGridView : DataGridView, IHiveWinFormsDataSurface
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsDataSurfaceMetadata HiveDataSurface { get; } = new();
}
