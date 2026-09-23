using System.Drawing;
using System.Windows.Forms;
using Hive.Core;

namespace Hive.Host.WinForms;

public sealed record HiveWinFormsHostContextOptions
{
    public HiveWinFormsHostContextOptions(
        int maxDepth = 8,
        int maxNodes = 512,
        int maxTextLength = 512)
    {
        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));

        if (maxNodes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxNodes));

        if (maxTextLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTextLength));

        MaxDepth = maxDepth;
        MaxNodes = maxNodes;
        MaxTextLength = maxTextLength;
    }

    public int MaxDepth { get; }

    public int MaxNodes { get; }

    public int MaxTextLength { get; }
}

public sealed record HiveWinFormsHostContextProvenance(
    Guid RegistrationId,
    Guid CaptureId,
    DateTimeOffset CapturedAtUtc,
    DeploymentId? DeploymentId,
    TenantId? TenantId,
    PrincipalId? PrincipalId,
    UserId? UserId,
    SessionId? SessionId,
    WorkspaceId? WorkspaceId);

public sealed record HiveWinFormsBindingSnapshot(
    string PropertyName,
    string BindingMember,
    string? DataSourceType);

public sealed record HiveWinFormsControlSnapshot(
    string Path,
    int Depth,
    string RuntimeType,
    string? Name,
    string? AccessibleName,
    string? Text,
    Rectangle Bounds,
    bool Visible,
    bool Enabled,
    bool Focused,
    bool ReadOnly,
    string? DataSourceType,
    string? DisplayMember,
    string? ValueMember,
    string? DataMember,
    IReadOnlyList<HiveWinFormsBindingSnapshot> Bindings);

public sealed record HiveWinFormsHostContextSnapshot(
    HiveWinFormsHostContextProvenance Provenance,
    string RootRuntimeType,
    string? RootName,
    int ControlCount,
    IReadOnlyList<HiveWinFormsControlSnapshot> Controls);

public sealed class HiveWinFormsHostRegistration : IDisposable
{
    private readonly HiveWinFormsHostContext _owner;
    private int _disposed;

    internal HiveWinFormsHostRegistration(
        HiveWinFormsHostContext owner,
        Form root)
    {
        _owner = owner;
        Root = root;
        RegistrationId = Guid.NewGuid();
    }

    internal Form Root { get; }

    internal bool IsDisposed =>
        Volatile.Read(ref _disposed) != 0;

    public Guid RegistrationId { get; }

    public string RootRuntimeType =>
        Root.GetType().FullName ?? Root.GetType().Name;

    public string? RootName =>
        string.IsNullOrWhiteSpace(Root.Name)
            ? null
            : Root.Name;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _owner.Unregister(this);
        GC.SuppressFinalize(this);
    }
}

public sealed class HiveWinFormsHostContext : IDisposable
{
    private readonly object _gate = new();
    private readonly ResourceAccessContext _accessContext;
    private readonly HiveWinFormsHostContextOptions _options;
    private readonly List<HiveWinFormsHostRegistration> _registrations = new();
    private int _disposed;

    public HiveWinFormsHostContext(
        ResourceAccessContext accessContext,
        HiveWinFormsHostContextOptions? options = null)
    {
        _accessContext = accessContext
            ?? throw new ArgumentNullException(nameof(accessContext));

        if (_accessContext.DeploymentId is null ||
            _accessContext.PrincipalId is null)
        {
            throw new ArgumentException(
                "WinForms host context requires deployment and principal identity.",
                nameof(accessContext));
        }

        _options = options ?? new HiveWinFormsHostContextOptions();
    }

    public HiveWinFormsHostRegistration Register(Form root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ThrowIfDisposed();

        if (root.IsDisposed || root.Disposing)
        {
            throw new ArgumentException(
                "The registered WinForms root is disposed.",
                nameof(root));
        }

        var registration = new HiveWinFormsHostRegistration(this, root);

        lock (_gate)
        {
            ThrowIfDisposed();
            _registrations.Add(registration);
        }

        return registration;
    }

    public async Task<Result<HiveWinFormsHostContextSnapshot>> CaptureAsync(
        HiveWinFormsHostRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (!ReferenceEquals(registration.Owner, this))
        {
            return Result<HiveWinFormsHostContextSnapshot>.Failure(
                Error.Forbidden(
                    "hive.host.context.registration-forbidden",
                    "The supplied host-context registration belongs to another context."));
        }

        if (registration.IsDisposed)
        {
            return Result<HiveWinFormsHostContextSnapshot>.Failure(
                Error.Conflict(
                    "hive.host.context.registration-disposed",
                    "The host-context registration has already been disposed."));
        }

        var root = registration.Root;
        if (root.IsDisposed || root.Disposing)
        {
            return Result<HiveWinFormsHostContextSnapshot>.Failure(
                Error.Conflict(
                    "hive.host.context.root-disposed",
                    "The registered WinForms root is no longer available."));
        }

        if (root.InvokeRequired)
        {
            return Result<HiveWinFormsHostContextSnapshot>.Failure(
                Error.Validation(
                    "hive.host.context.ui-thread-required",
                    "WinForms host-context discovery must run on the UI thread."));
        }

        return Result<HiveWinFormsHostContextSnapshot>.Success(
            CaptureCore(registration, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<HiveWinFormsHostContextSnapshot>>> CaptureAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        HiveWinFormsHostRegistration[] registrations;
        lock (_gate)
        {
            registrations = _registrations
                .Where(static registration => !registration.IsDisposed)
                .ToArray();
        }

        var snapshots = new List<HiveWinFormsHostContextSnapshot>(
            registrations.Length);

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await CaptureAsync(
                registration,
                cancellationToken).ConfigureAwait(false);

            if (snapshot.IsFailure)
                return Result<IReadOnlyList<HiveWinFormsHostContextSnapshot>>
                    .Failure(snapshot.Error!);

            snapshots.Add(snapshot.Value!);
        }

        return Result<IReadOnlyList<HiveWinFormsHostContextSnapshot>>.Success(
            snapshots);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        HiveWinFormsHostRegistration[] registrations;
        lock (_gate)
        {
            registrations = _registrations.ToArray();
            _registrations.Clear();
        }

        foreach (var registration in registrations)
            registration.MarkDisposedByOwner();

        GC.SuppressFinalize(this);
    }

    internal void Unregister(HiveWinFormsHostRegistration registration)
    {
        lock (_gate)
        {
            _registrations.Remove(registration);
        }
    }

    private HiveWinFormsHostContextSnapshot CaptureCore(
        HiveWinFormsHostRegistration registration,
        CancellationToken cancellationToken)
    {
        var controls = new List<HiveWinFormsControlSnapshot>();
        var visited = new HashSet<Control>(
            ReferenceEqualityComparer.Instance);

        var stack = new Stack<(Control Control, int Depth, string Path)>();
        stack.Push((registration.Root, 0, "0"));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (control, depth, path) = stack.Pop();

            if (control.IsDisposed || control.Disposing)
            {
                throw new InvalidOperationException(
                    $"The WinForms control at '{path}' was disposed during discovery.");
            }

            if (!visited.Add(control))
            {
                throw new HiveWinFormsHostContextLimitException(
                    "hive.host.context.cycle-detected",
                    "The WinForms control hierarchy contained a repeated control reference.");
            }

            if (controls.Count >= _options.MaxNodes)
            {
                throw new HiveWinFormsHostContextLimitException(
                    "hive.host.context.node-limit-exceeded",
                    $"WinForms host-context discovery exceeded the configured node limit of {_options.MaxNodes}.");
            }

            controls.Add(CreateSnapshot(control, depth, path));

            if (depth >= _options.MaxDepth)
            {
                if (control.Controls.Count > 0)
                {
                    throw new HiveWinFormsHostContextLimitException(
                        "hive.host.context.depth-limit-exceeded",
                        $"WinForms host-context discovery exceeded the configured depth limit of {_options.MaxDepth}.");
                }

                continue;
            }

            for (var index = control.Controls.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var child = control.Controls[index];
                if (child is null)
                    continue;

                stack.Push((
                    child,
                    depth + 1,
                    $"{path}/{index}"));
            }
        }

        return new HiveWinFormsHostContextSnapshot(
            CreateProvenance(registration.RegistrationId),
            registration.RootRuntimeType,
            registration.RootName,
            controls.Count,
            controls);
    }

    private HiveWinFormsControlSnapshot CreateSnapshot(
        Control control,
        int depth,
        string path)
    {
        var bindings = control.DataBindings
            .Cast<Binding>()
            .Select(static binding => new HiveWinFormsBindingSnapshot(
                binding.PropertyName,
                binding.BindingMemberInfo.BindingMember,
                binding.DataSource?.GetType().FullName))
            .ToArray();

        var readOnly = control switch
        {
            TextBoxBase textBox => IsReadOnly(textBox),
            _ => false
        };

        var dataSource = control switch
        {
            ListControl list => list.DataSource,
            DataGridView grid => grid.DataSource,
            _ => null
        };

        var dataSourceType = dataSource?.GetType().FullName;

        var displayMember = control is ListControl listControl &&
                            !string.IsNullOrWhiteSpace(listControl.DisplayMember)
            ? listControl.DisplayMember
            : null;

        var valueMember = control is ListControl valueListControl &&
                          !string.IsNullOrWhiteSpace(valueListControl.ValueMember)
            ? valueListControl.ValueMember
            : null;

        var dataMember = control is DataGridView dataGridView &&
                         dataGridView.DataMember.Length > 0
            ? dataGridView.DataMember
            : null;

        return new HiveWinFormsControlSnapshot(
            path,
            depth,
            control.GetType().FullName ?? control.GetType().Name,
            string.IsNullOrWhiteSpace(control.Name) ? null : control.Name,
            string.IsNullOrWhiteSpace(control.AccessibleName)
                ? null
                : control.AccessibleName,
            GetSafeText(control),
            control.Bounds,
            control.Visible,
            control.Enabled,
            control.Focused,
            readOnly,
            dataSourceType,
            displayMember,
            valueMember,
            dataMember,
            bindings);
    }

    private string? GetSafeText(Control control)
    {
        if (control is TextBox passwordBox &&
            passwordBox.UseSystemPasswordChar)
        {
            return "[redacted]";
        }

        if (control is MaskedTextBox masked &&
            masked.PasswordChar != '\0')
        {
            return "[redacted]";
        }

        var text = control.Text;
        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length <= _options.MaxTextLength
            ? text
            : text[.._options.MaxTextLength] + "…";
    }

    private static bool IsReadOnly(TextBoxBase textBox) =>
        textBox switch
        {
            TextBox singleLine => singleLine.ReadOnly,
            RichTextBox richText => richText.ReadOnly,
            _ => false
        };

    private HiveWinFormsHostContextProvenance CreateProvenance(
        Guid registrationId)
    {
        return new HiveWinFormsHostContextProvenance(
            registrationId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            _accessContext.DeploymentId,
            _accessContext.TenantId,
            _accessContext.PrincipalId,
            _accessContext.UserId,
            _accessContext.SessionId,
            _accessContext.WorkspaceId);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}

public sealed class HiveWinFormsHostContextLimitException : Exception
{
    public HiveWinFormsHostContextLimitException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

internal static class HiveWinFormsHostRegistrationExtensions
{
    public static Guid RegistrationOwnerToken(
        this HiveWinFormsHostRegistration registration) =>
        registration.RegistrationId;
}

internal static class HiveWinFormsHostRegistrationInternals
{
    public static void MarkDisposedByOwner(
        this HiveWinFormsHostRegistration registration)
    {
        registration.MarkDisposed();
    }
}
