using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Host.WinForms.UI.Controls;

internal sealed class HiveCrudPageOperationController : IDisposable
{
    private readonly Control _owner;
    private readonly ListView _list;
    private readonly TextBox _searchBox;
    private readonly ComboBox _statusFilterBox;
    private readonly HivePaginationBar _pagination;
    private readonly Action _updateActionState;
    private readonly Func<IHiveThemeManager?> _themeManagerProvider;

    private CancellationTokenSource? _operationCancellation;
    private bool _busy;

    internal HiveCrudPageOperationController(
        Control owner,
        ListView list,
        TextBox searchBox,
        ComboBox statusFilterBox,
        HivePaginationBar pagination,
        Action updateActionState,
        Func<IHiveThemeManager?> themeManagerProvider)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _searchBox = searchBox ?? throw new ArgumentNullException(nameof(searchBox));
        _statusFilterBox = statusFilterBox ?? throw new ArgumentNullException(nameof(statusFilterBox));
        _pagination = pagination ?? throw new ArgumentNullException(nameof(pagination));
        _updateActionState = updateActionState ?? throw new ArgumentNullException(nameof(updateActionState));
        _themeManagerProvider = themeManagerProvider ?? throw new ArgumentNullException(nameof(themeManagerProvider));
    }

    internal event EventHandler<HiveCrudOperationFailedEventArgs>? OperationFailed;

    internal bool IsBusy => _busy;

    private async Task ExecuteAsync(
        HiveCrudOperation operation,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = Interlocked.Exchange(
            ref _operationCancellation,
            source);
        previous?.Cancel();
        SetBusy(true);

        try
        {
            await action(source.Token);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (!IsDisposed && !Disposing)
                SetStatus("Cancelled.", HiveStatusTone.Warning);
        }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing)
            {
                SetStatus("Operation failed.", HiveStatusTone.Error);
                RaiseOperationFailed(operation, exception);
            }
        }
        finally
        {
            var isCurrentOperation = ReferenceEquals(
                _operationCancellation,
                source);

            if (isCurrentOperation)
                _operationCancellation = null;

            source.Dispose();

            if (isCurrentOperation && !IsDisposed && !Disposing)
                SetBusy(false);
        }
    }


    private void RaiseOperationFailed(
        HiveCrudOperation operation,
        Exception exception)
    {
        var handler = OperationFailed;
        if (handler is not null)
        {
            handler(
                this,
                new HiveCrudOperationFailedEventArgs(
                    operation,
                    exception));
            return;
        }

        // OperationFailed is an extension point, not a requirement for safe
        // reusable-control behavior. Without a subscriber, keep the failure
        // inside the UI operation instead of allowing an exception from a
        // button/event path to escape as an unhandled async exception.
        System.Diagnostics.Debug.WriteLine(exception.ToString());

        var owner = FindForm();
        if (owner is not null && !owner.IsDisposed)
        {
            HiveUiErrorReporter.Report(
                owner,
                exception,
                "CRUD operation failed",
                $"The {operation.ToString().ToLowerInvariant()} operation could not be completed.",
                null,
                ThemeManager());
        }
    }


    private void SetBusy(bool busy)
    {
        _busy = busy;
        _list.Enabled = !busy;
        _searchBox.Enabled = !busy;
        _statusFilterBox.Enabled = !busy;

        if (FindForm() is HiveForm hiveForm)
        {
            var theme = hiveForm.ThemeManager.Theme;
            _searchBox.BackColor = busy
                ? theme.Palette.DisabledBackground
                : theme.Palette.InputBackground;
            _searchBox.ForeColor = busy
                ? theme.Palette.DisabledText
                : theme.Palette.Text;
            _statusFilterBox.BackColor = busy
                ? theme.Palette.DisabledBackground
                : theme.Palette.InputBackground;
            _statusFilterBox.ForeColor = busy
                ? theme.Palette.DisabledText
                : theme.Palette.Text;
        }

        _pagination.Enabled = !busy;
        UpdateActionState();
    }


    internal void Dispose()
    {
        var operationCancellation = Interlocked.Exchange(
            ref _operationCancellation,
            null);
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        OperationFailed = null;
    }
}