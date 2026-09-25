using System.Windows.Forms;

namespace Hive.Host.WinForms.UI.Controls;

public class HiveTextBox : TextBox, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HiveComboBox : ComboBox, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HiveCheckBox : CheckBox, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HiveDateTimePicker : DateTimePicker, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HiveNumericUpDown : NumericUpDown, IHiveWinFormsFieldControl
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsFieldMetadata HiveField { get; } = new();
}

public class HiveDataGridView : DataGridView, IHiveWinFormsDataSurface
{
    public HiveWinFormsControlMetadata HiveIntegration { get; } = new();

    public HiveWinFormsDataSurfaceMetadata HiveDataSurface { get; } = new();
}
