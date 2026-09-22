# Phase 1.6 — Base Agent Work Protocols

Phase 1.6 adds the reusable non-cognitive work mechanisms exposed by a base `Agent` runtime.

## Runtime-owned protocol bundle

A `RuntimeInstance` exposes `RuntimeWorkProtocols` through its `Work` property:

```csharp
var runtime = agent.CreateRuntimeInstance();

var objective = runtime.Work.Objectives.Create(
    runtimeContext,
    agent.Id,
    runtime.Id,
    "Prepare invoice",
    new ObjectiveUpdate(
        "Customer and amount are validated.",
        10,
        DateTimeOffset.UtcNow.AddHours(1),
        []),
    DateTimeOffset.UtcNow);
```

Each runtime receives independent Objective and Memory stores and an independent Question transport by default. A stopped runtime preserves its protocol bundle so its in-memory state can be inspected without changing the Runtime identity.

## Objective lifecycle

`Objective` is an explicit work target. It has:

- `ObjectiveId`;
- Agent and Runtime ownership;
- title;
- completion criteria;
- priority;
- optional deadline;
- dependency identities;
- optional `WorkItemBinding`;
- `Active`, `Completed`, or `Cancelled` status.

Updates and terminal transitions return new immutable Objective values. `ObjectiveStore` uses an immutable-value plus compare/update pattern so concurrent stale updates return a typed concurrency failure rather than silently overwriting newer state.

The base protocol never creates, revises, prioritizes, or cancels objectives autonomously.

## WorkItem binding and provenance

The existing Core `WorkItem` remains authoritative. Phase 1.6 does not introduce another work-item identity.

`RuntimeWorkProtocols.BindWorkItem(...)` creates a `WorkItemBinding` containing:

- the existing `WorkItemId`;
- the WorkItem resource version observed at binding time;
- Agent and Runtime ownership;
- provenance with the WorkItem as the source resource.

Binding requires the protocol caller to satisfy the Runtime scope and to be the WorkItem owner. Scope is a structural boundary only; it is not itself an authorization grant.

## Memory infrastructure

`IAgentMemoryStore` is the replaceable storage boundary for explicitly addressed runtime memory.

The Phase 1.6 implementation is in-memory only:

```csharp
var stored = runtime.Work.Memory.Store(
    runtimeContext,
    agent.Id,
    runtime.Id,
    "customer.name",
    "Alice",
    DateTimeOffset.UtcNow);

var entries = runtime.Work.Memory.Retrieve(
    runtimeContext,
    agent.Id,
    runtime.Id,
    "customer.name");
```

Memory entries are Runtime-scoped `ResourceEnvelope<MemoryId>` resources. Retrieval is restricted to the owning Agent/Runtime boundary. Another RuntimeInstance cannot retrieve the entry merely because it belongs to the same Agent.

Persistence of memory is not part of Phase 1.6.

## Question / Answer transport

`IQuestionTransport` provides first-class questions for a runtime:

```csharp
var question = runtime.Work.Questions.Ask(
    runtimeContext,
    agent.Id,
    runtime.Id,
    "Is the invoice ready?",
    TimeSpan.FromMinutes(5),
    DateTimeOffset.UtcNow);

var answer = runtime.Work.Questions.Answer(
    responderContext,
    question.Value!.Id,
    responderAgent.Id,
    responderRuntime.Id,
    "Yes",
    DateTimeOffset.UtcNow);
```

Questions have `Waiting`, `Answered`, `TimedOut`, and `Cancelled` states. Only the owning RuntimeInstance may wait for or cancel its Question. A responder is separately identified and authenticated through its own Runtime access context. An explicitly shared Question transport may therefore support cross-runtime responders without making sharing implicit.

Waiting is asynchronous and cancellation-aware. Timeout processing is deterministic: the transport receives an `IClock`, and the owner explicitly calls `ExpireDue()` to complete Questions whose deadlines have passed. This keeps Phase 1.6 free of a second scheduler/workflow engine.

## Patience / Understanding Gate

`IUnderstandingGate` evaluates an explicit minimum-understanding policy:

```csharp
var policy = new UnderstandingGatePolicy(
    ["customer.name", "invoice.total"],
    confirmationRequired: true);

var blocked = gate.Evaluate(
    policy,
    ["customer.name"],
    confirmationReceived: false);

var satisfied = gate.Evaluate(
    policy,
    ["customer.name", "invoice.total"],
    confirmationReceived: true);
```

The result is deterministic and reports missing information plus confirmation state. The gate does not generate Questions, infer missing facts, or make a cognitive decision.

## Delegation

`IDelegationChannel` carries explicit requests between Agent RuntimeInstances:

```csharp
var request = DelegationRequest.Create(
    requesterContext,
    requesterAgent.Id,
    requesterRuntime.Id,
    delegateAgent.Id,
    delegateRuntime.Id,
    "Validate invoice total.",
    DateTimeOffset.UtcNow);
```

The request records requester/delegate Runtime identities and `ResourceProvenance`. An explicit shared `InMemoryDelegationChannel` lets the requesting and delegated runtimes read the request. Other runtimes are rejected.

The Phase 1.6 delegation boundary does not schedule, execute, retry, or orchestrate the delegated task.

## Generation boundary

These mechanisms are part of the base `Agent`. They do not turn a base Agent into a `CognitiveAgent`, infer goals, revise beliefs, generate adaptive Questions, choose Dreams, or learn from outcomes.

Later cognitive generations may build adaptive behavior over these stable mechanisms.
