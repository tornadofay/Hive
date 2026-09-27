using Hive.Core;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms;

internal sealed class HiveWinFormsTargetResolver
{
    private readonly Control _root;
    private readonly HiveWinFormsHostContextOptions _options;

    public HiveWinFormsTargetResolver(
        Control root,
        HiveWinFormsHostContextOptions options)
    {
        _root = root;
        _options = options;
    }

    public Control? FindControlByPath(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "0")
            return null;

        Control current = _root;

        for (var index = 1; index < parts.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!int.TryParse(
                    parts[index],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var childIndex) ||
                childIndex < 0 ||
                childIndex >= current.Controls.Count)
            {
                return null;
            }

            current = current.Controls[childIndex];
        }

        return current;
    }

    public Control? FindControlById(
        string id,
        CancellationToken cancellationToken) =>
        FindControlById(
            id,
            cancellationToken,
            out _);

    public Control? FindControlById(
        string id,
        CancellationToken cancellationToken,
        out string? currentPath)
    {
        currentPath = null;

        const string prefix = "control:";

        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var key = id[prefix.Length..];

        var stack = new Stack<(Control Control, int Depth, string Path)>();
        stack.Push((_root, 0, "0"));

        var explicitMatch = new List<Control>();
        var namedMatch = new List<Control>();
        var visited = new HashSet<Control>(
            ReferenceEqualityComparer.Instance);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (control, depth, traversalPath) = stack.Pop();

            if (depth > _options.MaxDepth ||
                visited.Count >= _options.MaxNodes)
            {
                continue;
            }

            if (!visited.Add(control))
                continue;

            if (GetExplicitControlId(control) is { } explicitId &&
                string.Equals(
                    explicitId,
                    key,
                    StringComparison.Ordinal))
            {
                explicitMatch.Add(control);
            }

            if (string.Equals(
                    control.Name,
                    key,
                    StringComparison.Ordinal))
            {
                namedMatch.Add(control);
            }

            if (control.IsDisposed || control.Disposing)
                continue;

            if (depth >= _options.MaxDepth)
                continue;

            for (var index = control.Controls.Count - 1; index >= 0; index--)
            {
                stack.Push((
                    control.Controls[index],
                    depth + 1,
                    traversalPath + "/" + index));
            }
        }

        if (explicitMatch.Count == 1)
        {
            var result = explicitMatch[0];
            currentPath = FindControlPath(
                result,
                cancellationToken);
            return result;
        }

        if (explicitMatch.Count > 1 ||
            namedMatch.Count > 1)
        {
            return null;
        }

        if (namedMatch.Count == 1)
        {
            var result = namedMatch[0];
            currentPath = FindControlPath(
                result,
                cancellationToken);
            return result;
        }

        if (key.StartsWith("0", StringComparison.Ordinal) &&
            key.Contains('/', StringComparison.Ordinal))
        {
            var result = FindControlByPath(
                key,
                cancellationToken);
            currentPath = result is null ? null : key;
            return result;
        }

        return null;
    }

    public DataGridView? FindDataSurfaceById(
        string surfaceId,
        CancellationToken cancellationToken,
        out string? currentPath)
    {
        currentPath = null;

        const string prefix = "surface:";
        if (!surfaceId.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var key = surfaceId[prefix.Length..];
        var stack = new Stack<(Control Control, int Depth, string Path)>();
        stack.Push((_root, 0, "0"));

        var visited = new HashSet<Control>(
            ReferenceEqualityComparer.Instance);
        var entries =
            new List<(DataGridView Grid, string Name, string Path, string? ExplicitId)>();

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (control, depth, traversalPath) = stack.Pop();

            if (depth > _options.MaxDepth ||
                visited.Count >= _options.MaxNodes)
            {
                continue;
            }

            if (!visited.Add(control) ||
                control.IsDisposed ||
                control.Disposing)
            {
                continue;
            }

            if (control is DataGridView grid)
            {
                entries.Add(
                    (
                        grid,
                        grid.Name,
                        traversalPath,
                        grid is IHiveWinFormsDataSurface hiveSurface
                            ? HiveWinFormsText.CleanOptional(
                                hiveSurface.HiveDataSurface.SurfaceId)
                            : null));
            }

            if (depth >= _options.MaxDepth)
                continue;

            for (var index = control.Controls.Count - 1; index >= 0; index--)
            {
                stack.Push(
                    (
                        control.Controls[index],
                        depth + 1,
                        traversalPath + "/" + index));
            }
        }

        var namedCounts = entries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(
                static entry => entry.Name,
                StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Count(),
                StringComparer.Ordinal);

        var matches = entries
            .Where(entry =>
            {
                var identity = entry.ExplicitId is not null
                    ? "surface:" + entry.ExplicitId
                    : "surface:" +
                      (!string.IsNullOrWhiteSpace(entry.Name) &&
                       namedCounts[entry.Name] == 1
                          ? entry.Name.Trim()
                          : entry.Path);

                return string.Equals(
                    identity,
                    surfaceId,
                    StringComparison.Ordinal);
            })
            .Select(static entry => entry.Grid)
            .Take(2)
            .ToArray();

        if (matches.Length != 1)
            return null;

        currentPath = entries
            .Where(entry => ReferenceEquals(entry.Grid, matches[0]))
            .Select(entry => entry.Path)
            .Single();

        return matches[0];
    }

    public Error? ValidateCaptureBinding(
        HiveHostInteractionRequest request,
        HiveWinFormsCapturedHostState? state,
        CancellationToken cancellationToken)
    {
        if (_root.InvokeRequired)
        {
            return Error.Validation(
                "hive.host.winforms.ui-thread-required",
                "WinForms host interaction must run on the UI thread.");
        }

        if (_root.IsDisposed ||
            _root.Disposing)
        {
            return Error.Conflict(
                "hive.host.winforms.root-disposed",
                "The registered WinForms host is no longer available.");
        }

        if (request.CaptureId is not { } captureId)
        {
            return Error.Validation(
                "hive.host.winforms.capture-required",
                "A host capture identity is required for this host interaction.");
        }

        if (state is null)
        {
            return Error.Conflict(
                "hive.host.winforms.capture-unavailable",
                "No current host capture is available for the requested interaction.");
        }

        var currentCapture = state.Capture;

        if (currentCapture.Provenance.CaptureId != captureId)
        {
            return Error.Conflict(
                "hive.host.winforms.capture-stale",
                "The requested host interaction was created from a stale host capture.");
        }

        var capabilityKind = request.Kind switch
        {
            HiveHostInteractionKind.ReadControl =>
                HiveHostCapabilityKind.ReadControl,
            HiveHostInteractionKind.SetControlValue =>
                HiveHostCapabilityKind.SetControlValue,
            HiveHostInteractionKind.AddRow =>
                HiveHostCapabilityKind.AddRow,
            HiveHostInteractionKind.EditRow =>
                HiveHostCapabilityKind.EditRow,
            HiveHostInteractionKind.DeleteRow =>
                HiveHostCapabilityKind.DeleteRow,
            HiveHostInteractionKind.InvokeAction =>
                HiveHostCapabilityKind.InvokeAction,
            _ => throw new InvalidOperationException(
                "The interaction does not require a fresh capture.")
        };

        HiveHostCapabilityDescriptor? capability;

        if (request.Kind is
            HiveHostInteractionKind.ReadControl or
            HiveHostInteractionKind.SetControlValue)
        {
            capability = currentCapture.Controls
                .Where(control =>
                    string.Equals(
                        control.Id,
                        request.ControlId,
                        StringComparison.Ordinal))
                .SelectMany(control => control.Capabilities)
                .SingleOrDefault(candidate =>
                    candidate.Id == request.CapabilityId &&
                    candidate.Kind == capabilityKind);

            if (capability is not null &&
                request.ControlId is not null &&
                state.ControlsById.TryGetValue(
                    request.ControlId,
                    out var capturedControl))
            {
                var currentControl = FindControlById(
                    request.ControlId,
                    cancellationToken,
                    out var currentControlPath);

                var capturedControlDescriptor = currentCapture.Controls
                    .Single(control => string.Equals(
                        control.Id,
                        request.ControlId,
                        StringComparison.Ordinal));

                if (!state.TargetAncestryById.TryGetValue(
                        request.ControlId,
                        out var capturedControlAncestry) ||
                    !ReferenceEquals(currentControl, capturedControl) ||
                    !string.Equals(
                        capturedControlDescriptor.Path,
                        currentControlPath,
                        StringComparison.Ordinal) ||
                    !AreSameControlAncestry(
                        capturedControlAncestry,
                        currentControl))
                {
                    return Error.Conflict(
                        "hive.host.winforms.target-stale",
                        "The requested WinForms control instance is no longer the one captured for this interaction.");
                }
            }
        }
        else if (request.Kind is
            HiveHostInteractionKind.AddRow or
            HiveHostInteractionKind.EditRow or
            HiveHostInteractionKind.DeleteRow)
        {
            capability = currentCapture.DataSurfaces
                .Where(surface =>
                    string.Equals(
                        surface.Id,
                        request.SurfaceId,
                        StringComparison.Ordinal))
                .SelectMany(surface => surface.Capabilities)
                .SingleOrDefault(candidate =>
                    candidate.Id == request.CapabilityId &&
                    candidate.Kind == capabilityKind);

            if (capability is not null &&
                request.SurfaceId is not null)
            {
                var currentSurface = FindDataSurfaceById(
                    request.SurfaceId,
                    cancellationToken,
                    out var currentSurfacePath);

                if (!state.SurfacesById.TryGetValue(
                        request.SurfaceId,
                        out var capturedSurface) ||
                    !state.SurfacePathsById.TryGetValue(
                        request.SurfaceId,
                        out var capturedSurfacePath) ||
                    !state.TargetAncestryById.TryGetValue(
                        request.SurfaceId,
                        out var capturedSurfaceAncestry) ||
                    !ReferenceEquals(
                        currentSurface,
                        capturedSurface) ||
                    !string.Equals(
                        capturedSurfacePath,
                        currentSurfacePath,
                        StringComparison.Ordinal) ||
                    !AreSameControlAncestry(
                        capturedSurfaceAncestry,
                        currentSurface))
                {
                    return Error.Conflict(
                        "hive.host.winforms.target-stale",
                        "The requested WinForms data surface instance is no longer the one captured for this interaction.");
                }
            }
        }
        else
        {
            IEnumerable<HiveHostCapabilityDescriptor> candidates;

            if (request.ControlId is not null)
            {
                candidates = currentCapture.Controls
                    .Where(control =>
                        string.Equals(
                            control.Id,
                            request.ControlId,
                            StringComparison.Ordinal))
                    .SelectMany(control => control.Capabilities);
            }
            else if (request.SurfaceId is not null)
            {
                candidates = currentCapture.DataSurfaces
                    .Where(surface =>
                        string.Equals(
                            surface.Id,
                            request.SurfaceId,
                            StringComparison.Ordinal))
                    .SelectMany(surface => surface.Capabilities);
            }
            else
            {
                candidates = currentCapture.Controls
                    .SelectMany(control => control.Capabilities)
                    .Concat(
                        currentCapture.DataSurfaces
                            .SelectMany(surface => surface.Capabilities));
            }

            capability = candidates.SingleOrDefault(candidate =>
                candidate.Id == request.CapabilityId &&
                candidate.Kind == capabilityKind);
        }

        if (capability is null)
        {
            return new Error(
                "hive.host.winforms.capability-mismatch",
                ErrorCategory.Forbidden,
                "The requested capability is not exposed by the current host capture for the supplied target.");
        }

        if (!capability.Supported)
        {
            return Error.Unsupported(
                "hive.host.winforms.capability-unsupported",
                "The requested host capability is currently unsupported.");
        }

        if (request.Kind == HiveHostInteractionKind.InvokeAction)
        {
            if (request.ControlId is not null)
            {
                var currentControl = FindControlById(
                    request.ControlId,
                    cancellationToken,
                    out var currentControlPath);

                if (!state.ControlsById.TryGetValue(
                        request.ControlId,
                        out var capturedControl) ||
                    !state.TargetAncestryById.TryGetValue(
                        request.ControlId,
                        out var capturedControlAncestry) ||
                    !ReferenceEquals(
                        currentControl,
                        capturedControl) ||
                    !string.Equals(
                        currentCapture.Controls.Single(control => string.Equals(
                            control.Id,
                            request.ControlId,
                            StringComparison.Ordinal)).Path,
                        currentControlPath,
                        StringComparison.Ordinal) ||
                    !AreSameControlAncestry(
                        capturedControlAncestry,
                        currentControl))
                {
                    return Error.Conflict(
                        "hive.host.winforms.target-stale",
                        "The requested WinForms action target is no longer the one captured for this interaction.");
                }
            }

            if (request.SurfaceId is not null)
            {
                var currentSurface = FindDataSurfaceById(
                    request.SurfaceId,
                    cancellationToken,
                    out var currentSurfacePath);

                if (!state.SurfacesById.TryGetValue(
                        request.SurfaceId,
                        out var capturedSurface) ||
                    !state.SurfacePathsById.TryGetValue(
                        request.SurfaceId,
                        out var capturedSurfacePath) ||
                    !state.TargetAncestryById.TryGetValue(
                        request.SurfaceId,
                        out var capturedSurfaceAncestry) ||
                    !ReferenceEquals(
                        currentSurface,
                        capturedSurface) ||
                    !string.Equals(
                        capturedSurfacePath,
                        currentSurfacePath,
                        StringComparison.Ordinal) ||
                    !AreSameControlAncestry(
                        capturedSurfaceAncestry,
                        currentSurface))
                {
                    return Error.Conflict(
                        "hive.host.winforms.target-stale",
                        "The requested WinForms action surface is no longer the one captured for this interaction.");
                }
            }
        }

        return null;
    }

    internal static IReadOnlyList<Control> CaptureControlAncestry(
        Control control)
    {
        var ancestry = new List<Control>();

        for (var current = control.Parent;
             current is not null;
             current = current.Parent)
        {
            ancestry.Add(current);
        }

        ancestry.Reverse();
        return ancestry;
    }

    private static bool AreSameControlAncestry(
        IReadOnlyList<Control> capturedAncestry,
        Control? currentControl)
    {
        if (currentControl is null)
            return false;

        var currentAncestry = CaptureControlAncestry(currentControl);

        if (capturedAncestry.Count != currentAncestry.Count)
            return false;

        for (var index = 0; index < capturedAncestry.Count; index++)
        {
            if (!ReferenceEquals(
                    capturedAncestry[index],
                    currentAncestry[index]))
            {
                return false;
            }
        }

        return true;
    }

    private string? FindControlPath(
        Control target,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<(Control Control, int Depth, string Path)>();
        stack.Push((_root, 0, "0"));

        var visited = new HashSet<Control>(
            ReferenceEqualityComparer.Instance);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (control, depth, currentPath) = stack.Pop();

            if (depth > _options.MaxDepth ||
                visited.Count >= _options.MaxNodes)
            {
                continue;
            }

            if (!visited.Add(control))
                continue;

            if (ReferenceEquals(control, target))
                return currentPath;

            if (control.IsDisposed ||
                control.Disposing ||
                depth >= _options.MaxDepth)
            {
                continue;
            }

            for (var index = control.Controls.Count - 1; index >= 0; index--)
            {
                stack.Push((
                    control.Controls[index],
                    depth + 1,
                    currentPath + "/" + index));
            }
        }

        return null;
    }

    private static string? GetExplicitControlId(Control control) =>
        control is IHiveWinFormsControl hiveControl
            ? HiveWinFormsText.CleanOptional(
                hiveControl.HiveIntegration.ControlId)
            : null;
}
