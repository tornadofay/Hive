using System.Windows.Forms;
using Hive.Core;

namespace Hive.Host.WinForms;

internal interface IWinFormsControlValueAdapter
{
    bool CanHandle(Control control);

    bool CanSet(Control control);

    string ValueTypeName { get; }

    HiveHostValue? Read(Control control);

    Result<HiveHostInteractionResult> Set(
        Control control,
        HiveHostInteractionRequest request);
}

internal static class WinFormsControlValueAdapters
{
    private static readonly IWinFormsControlValueAdapter[] Adapters =
    [
        new TextBoxValueAdapter(),
        new CheckBoxValueAdapter(),
        new ComboBoxValueAdapter(),
        new DateTimePickerValueAdapter(),
        new NumericUpDownValueAdapter()
    ];

    internal static bool TryGet(
        Control control,
        out IWinFormsControlValueAdapter adapter)
    {
        foreach (var candidate in Adapters)
        {
            if (candidate.CanHandle(control))
            {
                adapter = candidate;
                return true;
            }
        }

        adapter = null!;
        return false;
    }

    internal static string GetValueTypeName(Control control) =>
        TryGet(control, out var adapter)
            ? adapter.ValueTypeName
            : typeof(string).FullName!;

    internal static HiveHostValue? TryReadValue(Control control) =>
        TryGet(control, out var adapter)
            ? adapter.Read(control)
            : null;

    internal static Result<HiveHostInteractionResult> SetControlValue(
        Control control,
        HiveHostInteractionRequest request) =>
        TryGet(control, out var adapter)
            ? adapter.Set(control, request)
            : Result<HiveHostInteractionResult>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.interaction-unsupported",
                    "The reusable adapter does not support setting this control type."));
}

internal abstract class WinFormsControlValueAdapterBase :
    IWinFormsControlValueAdapter
{
    public abstract bool CanHandle(Control control);

    public abstract bool CanSet(Control control);

    public abstract string ValueTypeName { get; }

    public abstract HiveHostValue? Read(Control control);

    public abstract Result<HiveHostInteractionResult> Set(
        Control control,
        HiveHostInteractionRequest request);

    protected static Result<HiveHostInteractionResult> Unsupported() =>
        Result<HiveHostInteractionResult>.Failure(
            Error.Unsupported(
                "hive.host.winforms.interaction-unsupported",
                "The reusable adapter does not support setting this control type."));

    protected static Result<HiveHostInteractionResult> Success(
        Control control,
        HiveHostInteractionRequest request) =>
        Result<HiveHostInteractionResult>.Success(
            new HiveHostInteractionResult(
                request.CorrelationId,
                request.Kind,
                WinFormsControlValueAdapters.TryReadValue(control)));
}

internal sealed class TextBoxValueAdapter : WinFormsControlValueAdapterBase
{
    public override bool CanHandle(Control control) =>
        control is TextBoxBase;

    public override bool CanSet(Control control) =>
        control is TextBoxBase &&
        !IsReadOnly(control);

    public override string ValueTypeName =>
        typeof(string).FullName!;

    public override HiveHostValue? Read(Control control)
    {
        if (control is TextBox passwordTextBox &&
            (passwordTextBox.UseSystemPasswordChar ||
             passwordTextBox.PasswordChar != '\0'))
        {
            return null;
        }

        if (control is MaskedTextBox maskedTextBox &&
            maskedTextBox.PasswordChar != '\0')
        {
            return null;
        }

        return control is TextBoxBase textBox
            ? HiveHostValue.FromString(textBox.Text)
            : null;
    }

    public override Result<HiveHostInteractionResult> Set(
        Control control,
        HiveHostInteractionRequest request)
    {
        if (IsReadOnly(control))
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.control-read-only",
                    "The requested WinForms control is read-only."));
        }

        if (control is not TextBoxBase textBox)
        {
            return Unsupported();
        }

        if (request.Value is not { } value ||
            value.Kind != HiveHostValueKind.String)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.value-type-invalid",
                    "A string value is required for a text control."));
        }

        if (control is TextBox password &&
            (password.UseSystemPasswordChar ||
             password.PasswordChar != '\0'))
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.password-write-unsupported",
                    "Password controls are not handled by the reusable value adapter."));
        }

        textBox.Text = value.AsString()!;

        return Success(control, request);
    }

    private static bool IsReadOnly(Control control) =>
        control switch
        {
            TextBox textBox => textBox.ReadOnly,
            RichTextBox richTextBox => richTextBox.ReadOnly,
            MaskedTextBox maskedTextBox => maskedTextBox.ReadOnly,
            _ => false
        };

}

internal sealed class CheckBoxValueAdapter : WinFormsControlValueAdapterBase
{
    public override bool CanHandle(Control control) =>
        control is CheckBox;

    public override bool CanSet(Control control) =>
        control is CheckBox;

    public override string ValueTypeName =>
        typeof(bool).FullName!;

    public override HiveHostValue? Read(Control control) =>
        control is CheckBox checkBox
            ? HiveHostValue.FromBoolean(checkBox.Checked)
            : null;

    public override Result<HiveHostInteractionResult> Set(
        Control control,
        HiveHostInteractionRequest request)
    {
        if (control is not CheckBox checkBox)
            return Unsupported();

        if (request.Value is not { } value ||
            !value.TryGetBoolean(out var boolean))
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.value-type-invalid",
                    "A boolean value is required for a check box."));
        }

        checkBox.Checked = boolean;

        return Success(control, request);
    }

}

internal sealed class ComboBoxValueAdapter : WinFormsControlValueAdapterBase
{
    public override bool CanHandle(Control control) =>
        control is ComboBox;

    public override bool CanSet(Control control) =>
        control is ComboBox comboBox &&
        comboBox.DropDownStyle != ComboBoxStyle.DropDownList;

    public override string ValueTypeName =>
        typeof(string).FullName!;

    public override HiveHostValue? Read(Control control) =>
        control is ComboBox comboBox
            ? HiveHostValue.FromString(comboBox.Text)
            : null;

    public override Result<HiveHostInteractionResult> Set(
        Control control,
        HiveHostInteractionRequest request)
    {
        if (control is not ComboBox comboBox)
            return Unsupported();

        if (comboBox.DropDownStyle == ComboBoxStyle.DropDownList)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.combo-selection-requires-lookup",
                    "Selection in a drop-down list must use the bounded lookup contract."));
        }

        if (request.Value is not { } value ||
            value.Kind != HiveHostValueKind.String)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.value-type-invalid",
                    "A string value is required for a combo box."));
        }

        comboBox.Text = value.AsString()!;

        return Success(control, request);
    }

}

internal sealed class DateTimePickerValueAdapter : WinFormsControlValueAdapterBase
{
    public override bool CanHandle(Control control) =>
        control is DateTimePicker;

    public override bool CanSet(Control control) =>
        control is DateTimePicker;

    public override string ValueTypeName =>
        typeof(DateTime).FullName!;

    public override HiveHostValue? Read(Control control) =>
        control is DateTimePicker dateTimePicker
            ? HiveHostValue.FromDateTime(dateTimePicker.Value)
            : null;

    public override Result<HiveHostInteractionResult> Set(
        Control control,
        HiveHostInteractionRequest request)
    {
        if (control is not DateTimePicker dateTimePicker)
            return Unsupported();

        if (request.Value is not { } value ||
            !value.TryGetDateTime(out var dateTime))
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.value-type-invalid",
                    "A date/time value is required for a date-time control."));
        }

        if (dateTime < dateTimePicker.MinDate ||
            dateTime > dateTimePicker.MaxDate)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.datetime-range-invalid",
                    "The requested date/time value is outside the host control range."));
        }

        dateTimePicker.Value = dateTime;

        return Success(control, request);
    }

}

internal sealed class NumericUpDownValueAdapter : WinFormsControlValueAdapterBase
{
    public override bool CanHandle(Control control) =>
        control is NumericUpDown;

    public override bool CanSet(Control control) =>
        control is NumericUpDown numericUpDown &&
        !numericUpDown.ReadOnly;

    public override string ValueTypeName =>
        typeof(decimal).FullName!;

    public override HiveHostValue? Read(Control control) =>
        control is NumericUpDown numericUpDown
            ? HiveHostValue.FromDecimal(numericUpDown.Value)
            : null;

    public override Result<HiveHostInteractionResult> Set(
        Control control,
        HiveHostInteractionRequest request)
    {
        if (control is not NumericUpDown numericUpDown)
            return Unsupported();

        if (numericUpDown.ReadOnly)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.control-read-only",
                    "The requested WinForms control is read-only."));
        }

        if (request.Value is not { } value ||
            !value.TryGetDecimal(out var decimalValue))
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.value-type-invalid",
                    "A decimal value is required for a numeric control."));
        }

        if (decimalValue < numericUpDown.Minimum ||
            decimalValue > numericUpDown.Maximum)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.numeric-range-invalid",
                    "The requested numeric value is outside the host control range."));
        }

        numericUpDown.Value = decimalValue;

        return Success(control, request);
    }

}
