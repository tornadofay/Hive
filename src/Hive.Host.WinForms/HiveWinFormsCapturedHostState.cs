using Hive.Core;

namespace Hive.Host.WinForms;

internal sealed class HiveWinFormsCapturedHostState
{
    public HiveWinFormsCapturedHostState(
        long generation,
        HiveHostContextDescriptor capture,
        IReadOnlyDictionary<string, Control> controlsById,
        IReadOnlyDictionary<string, DataGridView> surfacesById,
        IReadOnlyDictionary<string, string> surfacePathsById,
        IReadOnlyDictionary<string, IReadOnlyList<Control>> targetAncestryById)
    {
        Generation = generation;
        Capture = capture;
        ControlsById = controlsById;
        SurfacesById = surfacesById;
        SurfacePathsById = surfacePathsById;
        TargetAncestryById = targetAncestryById;
    }

    public long Generation { get; }

    public HiveHostContextDescriptor Capture { get; }

    public IReadOnlyDictionary<string, Control> ControlsById { get; }

    public IReadOnlyDictionary<string, DataGridView> SurfacesById { get; }

    public IReadOnlyDictionary<string, string> SurfacePathsById { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<Control>> TargetAncestryById { get; }
}
