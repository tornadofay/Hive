using Hive.Core;

namespace Hive.Persistence;

public interface IStructuredExtractionBatchStore
{
    Task<Result<StructuredExtractionBatch>> CreateAsync(
        StructuredExtractionBatch batch,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<StructuredExtractionBatch>> GetAsync(
        StructuredExtractionBatchId batchId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<StructuredExtractionBatch>> UpdateAsync(
        StructuredExtractionBatch batch,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);
}
