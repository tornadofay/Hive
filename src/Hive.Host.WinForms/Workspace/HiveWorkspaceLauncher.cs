namespace Hive.Host.WinForms;

/// <summary>
/// Opens the Workspace window for a host and keeps a single instance: a second request activates
/// the window that is already open.
/// </summary>
public sealed class HiveWorkspaceLauncher : IDisposable
{
    private readonly HiveWorkspaceContext _context;
    private readonly IReadOnlyList<IHiveWorkspaceModule>? _modules;
    private HiveWorkspaceForm? _form;

    public HiveWorkspaceLauncher(HiveWorkspaceContext context)
        : this(context, null)
    {
    }

    internal HiveWorkspaceLauncher(
        HiveWorkspaceContext context,
        IReadOnlyList<IHiveWorkspaceModule>? modules)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _modules = modules;
    }

    public bool IsOpen => _form is { IsDisposed: false };

    public HiveWorkspaceForm Show(IWin32Window? owner = null)
    {
        if (_form is { IsDisposed: false } existing)
        {
            if (existing.WindowState == FormWindowState.Minimized)
                existing.WindowState = FormWindowState.Normal;

            existing.Activate();
            return existing;
        }

        var form = _modules is null
            ? new HiveWorkspaceForm(_context)
            : new HiveWorkspaceForm(_context, _modules);

        form.FormClosed += FormOnFormClosed;
        _form = form;

        if (owner is null)
            form.Show();
        else
            form.Show(owner);

        return form;
    }

    public void Close() => _form?.Close();

    public void Dispose()
    {
        Close();
        _form = null;
    }

    private void FormOnFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is not HiveWorkspaceForm closed)
            return;

        closed.FormClosed -= FormOnFormClosed;

        if (ReferenceEquals(_form, closed))
            _form = null;
    }
}
