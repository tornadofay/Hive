# Phase 1.5 — Base Agent & AgentFactory

Create the base Agent explicitly through `AgentFactory` and keep RuntimeInstance and Execution state isolated.

```csharp
var definition = new AgentDefinition(
    "example-agent",
    "Example Agent",
    AgentGeneration.Base);

var context = new AgentCreationContext(
    new ResourceAccessContext(
        deploymentId,
        tenantId,
        principalId));

var factory = new AgentFactory(authorizer);
var result = factory.Create<Agent>(definition, context);
```

An authorized base Agent can create independent runtime incarnations:

```csharp
var first = result.Value!.CreateRuntimeInstance();
var second = result.Value.CreateRuntimeInstance();

var firstExecution = first.StartExecution();
var secondExecution = second.StartExecution();
```

Execution transitions are immutable. For example, `firstExecution.Value.Complete()` returns a new terminal Execution value and leaves `secondExecution.Value` unchanged.

Rules in this slice:

- Agent generation is selected explicitly in `AgentDefinition`.
- The current factory supports `AgentGeneration.Base` only.
- `AgentGeneration.Cognitive` is reserved for the later CognitiveAgent generation and is rejected here rather than silently promoted.
- Creation requires deployment and principal identity.
- Creation is passed through `IAgentCreationAuthorizer`; unauthorized creation returns a typed `Forbidden` result.
- Runtime identities are distinct per incarnation and remain tied to the creating Agent identity.
- Execution identity is distinct from Runtime identity.
- Stopped runtimes cannot start new executions.
- Terminal executions cannot transition again.
- No persistence, provider transport, MAF execution, cognitive state, Dream, adaptive Question, or learning behavior is introduced by this slice.
