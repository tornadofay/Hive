using System.Text.Json;

namespace Hive.Core;

public interface IEventStateReducer<TState>
{
    EventType EventType { get; }

    EventPayloadVersion CurrentPayloadSchemaVersion { get; }

    TState Apply(
        TState state,
        EventEnvelope envelope,
        JsonElement payload);
}

public sealed class EventStateReducerRegistry<TState>
{
    private readonly Dictionary<EventType, IEventStateReducer<TState>> _reducers = new();

    public void Register(IEventStateReducer<TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);

        if (!_reducers.TryAdd(reducer.EventType, reducer))
        {
            throw new InvalidOperationException(
                $"An event reducer is already registered for event type '{reducer.EventType}'.");
        }
    }

    public bool TryGet(
        EventType eventType,
        out IEventStateReducer<TState>? reducer) =>
        _reducers.TryGetValue(eventType, out reducer);
}

public sealed class EventSnapshotFolder<TState>
{
    private readonly JsonEventSerializer _serializer;
    private readonly EventStateReducerRegistry<TState> _reducers;

    public EventSnapshotFolder(
        EventStateReducerRegistry<TState> reducers,
        JsonEventSerializer? serializer = null)
    {
        _reducers = reducers ?? throw new ArgumentNullException(nameof(reducers));
        _serializer = serializer ?? new JsonEventSerializer();
    }

    public Result<TState> Fold(
        TState initialState,
        IReadOnlyList<EventEnvelope> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var state = initialState;

        foreach (var envelope in events)
        {
            if (!_reducers.TryGet(envelope.EventType, out var reducer))
            {
                return Result<TState>.Failure(
                    Error.Unsupported(
                        "event.reducer.missing",
                        $"No snapshot reducer is registered for event '{envelope.EventType}'."));
            }

            try
            {
                var payload = _serializer.UpcasterRegistry.UpcastTo(
                    envelope.EventType,
                    envelope.PayloadSchemaVersion,
                    reducer!.CurrentPayloadSchemaVersion,
                    envelope.Payload);

                state = reducer.Apply(state, envelope, payload);
            }
            catch (EventSerializationException exception)
            {
                return Result<TState>.Failure(exception.Error);
            }
            catch (Exception exception)
            {
                return Result<TState>.Failure(
                    new Error(
                        "event.reducer.failed",
                        ErrorCategory.Internal,
                        $"Snapshot reducer for event '{envelope.EventType}' failed: {exception.Message}"));
            }
        }

        return Result<TState>.Success(state);
    }
}
