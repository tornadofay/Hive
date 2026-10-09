namespace Hive.Management;

/// <summary>
/// Coordinates ordinary management operations with an exclusive persistence migration.
/// </summary>
public interface IHiveManagementOperationGate
{
    /// <summary>
    /// Tries to register an ordinary operation. Returns <see langword="null"/> while
    /// an exclusive migration has quiesced the active management facade.
    /// </summary>
    IAsyncDisposable? TryEnterOperation();
}
