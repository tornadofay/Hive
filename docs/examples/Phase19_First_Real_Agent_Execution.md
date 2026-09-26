# Phase 1.9 — First Real Agent Execution

This example connects a Hive Base Agent to Microsoft Agent Framework for one request, using the selected OpenAI-compatible ExecutionTarget and the existing Hive event store.

```csharp
using var httpClient = new HttpClient();

var service = new AgentExecutionService(
    eventStore,
    httpClient);

var result = await service.ExecuteAsync(
    new AgentExecutionRequest(
        agent,
        runtime,
        selectedTarget,
        accessContext,
        "Say hello.",
        apiKey: secretMaterial),
    cancellationToken);
```

The request creates the Hive `Execution`, records `agent.execution.started`, runs the MAF agent through Hive's OpenAI-compatible `IChatClient` bridge, and records one terminal event:

- `agent.execution.succeeded`
- `agent.execution.failed`
- `agent.execution.cancelled`

The same `CorrelationId` is retained across the lifecycle; the terminal event uses the started event as its causation.

The selected `ExecutionTarget` must be active and inside the supplied access scope. The provider credential is resolved by the caller; this service does not persist or log secret material.

The Phase 1.9 Example Host uses a local fake HTTP provider, so it does not require vendor credentials or an external network call.
