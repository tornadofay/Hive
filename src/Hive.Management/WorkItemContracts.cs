using Hive.Core;

namespace Hive.Management;

public sealed record WorkItemActivity(
    EventId EventId,
    DateTimeOffset OccurredAtUtc,
    ResourceVersion Version,
    string EventType,
    WorkItemStatus? Status,
    string? Message,
    CorrelationId CorrelationId,
    CausationId? CausationId);
