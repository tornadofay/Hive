using System;
using System.Drawing;

namespace Hive.Host.WinForms.UI.Controls;

public sealed record HiveCrudColumn<TItem> where TItem : class
{
    public HiveCrudColumn(
        string header,
        int width,
        Func<TItem, string?> valueSelector,
        Func<TItem, Color?>? foregroundColorSelector = null)
    {
        if (string.IsNullOrWhiteSpace(header))
            throw new ArgumentException("Column header is required.", nameof(header));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentNullException.ThrowIfNull(valueSelector);

        Header = header;
        Width = width;
        ValueSelector = valueSelector;
        ForegroundColorSelector = foregroundColorSelector;
    }

    public string Header { get; }

    public int Width { get; }

    public Func<TItem, string?> ValueSelector { get; }

    public Func<TItem, Color?>? ForegroundColorSelector { get; }
}
