# Hive Architecture — Execution, Provider, Resource, and Durability Boundaries



This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the detailed execution, provider, resource, intervention, and durable-event boundaries.



## 1. MAF Dependency Boundary

**Hard rule: use Microsoft Agent Framework whenever it already owns the required behavior. Hive adds only the semantics and platform contracts MAF does not own.**

### MAF owns

- agent/workflow execution mechanics;
- workflow graph execution;
- supported Sequential / Concurrent / Handoff / Group Chat / Magentic orchestration;
- framework checkpoints and resume where applicable;
- workflow request/response mechanics;
- actual model-service invocation through the selected model client;
- framework-level tool invocation mechanics;
- framework-level workflow events and execution flow.

### Hive owns

- tenant/user/workspace/resource ownership;
- Workspace interaction and operational state;
- stable Agent and Hive identities;
- the Agent/Hive type hierarchy;
- persistent cognitive runtime and cognitive strategy, but only for cognitive generations;
- memory/knowledge/skill/learning resource governance;
- provider control plane and Execution Target selection;
- capability evidence and execution requirements;
- authorization and tool permission policy;
- Hive-owned durable state, event history, snapshots, and outbox semantics;
- cognitive lifecycle, Dream, and Question semantics for the cognitive generations;
- host application integration;
- WinForms management facade and configuration surface, including first-class Provider and Persistence configuration;
- human intervention beyond MAF's lower-level request mechanics;
- Hive membership, roles, governance patterns, and collective cognition;
- Hive-specific budgets, safety, diagnostics, and audit.

Hive must never build a second workflow/orchestration engine merely because Hive adds policy or state around an MAF workflow.

---



## 2. Core Technology & Provider Platform

| Concern | Choice | Boundary |
|---|---|---|
| Language/runtime | C# / .NET 10 only | Single runtime baseline |
| Agent programming model | Microsoft Agent Framework | Reuse MAF execution/orchestration |
| Execution model | Ephemeral execution + durable Agent/Hive state + transactional outbox | Execution objects and Agent incarnations may end; durable state survives; one V1 submitted document is one WorkItem, while a batch is multiple WorkItems |
| Persistence | SQL Server; LocalDB for development | Hive database is isolated from host business data |
| Vector storage | SQL Server `VECTOR` / `VECTOR_DISTANCE` behind `IVectorStore` | Replaceable storage boundary |
| Provider adapter | One shared OpenAI-compatible adapter | Compatible providers are configurations, not new adapter implementations |
| Provider | Vendor/service integration | Transport identity |
| ProviderAccount | Credential/account/project under Provider | Credential/account boundary |
| ExecutionTarget | Provider + ProviderAccount + endpoint + model/deployment | Concrete capability-bearing target |
| Capability state | `Supported` / `Unsupported` / `Unknown` | Unknown never silently becomes supported |
| Capability requirement | `Required` / `Preferred` / `Optional` / `Forbidden` | Independent of provider identity |
| Selection mode | `Auto` / `Preferred` / `Fixed` | Preference is separate from hard pinning |
| Cost policy | `FreeOnly` / `FreePreferred` / `NoRestriction` | Cost is separate from capability |
| Secrets | Encrypted at rest through `ISecretStore`; redacted elsewhere | No unnecessary external vault architecture |

`ProviderAccount` stores only an optional `SecretReference` for provider credential material. Provider connection tests resolve that reference through `Hive.Management` and `ISecretStore`; provider credentials are never stored in ProviderAccount fields, configuration files, diagnostics, or provider-test output.

The Phase 1.12 Settings boundary uses one typed persistence configuration contract containing SQL Server endpoint/port, database identity, authentication mode, non-secret login metadata, optional Secret Store credential reference, connection-security flags, database-initialization policy, and command timeout. `Hive.Management` exposes save/load and connection-test operations. The connection-test boundary must inspect server/database/schema state without creating the database or applying migrations. WinForms settings pages consume only these Management operations.

Current V1 provider configurations include compatible hosted/local targets such as Groq, OpenRouter, Cloudflare, Cerebras, NVIDIA, Google, and local OpenAI-compatible servers. The adapter contract remains vendor-neutral; adding another compatible provider should normally require configuration, not another transport implementation.

Deferred until a measured requirement exists: Temporal, Dapr, PostgreSQL/pgvector, Elasticsearch/OpenSearch, Akka.NET, Orleans, DiskANN, a custom Hive workflow engine, and a separate external secrets-vault architecture.

---



## 6. Generic Resource Model

All Hive-owned persistent resources share a common identity/ownership envelope:

```
Resource {
    Identity,
    Owner,
    Scope,
    Version,
    Provenance,
    Lifecycle,
    Permissions,
    Metadata
}
```

Canonical scopes:

```
Global / Tenant / User / Workspace / Agent / Runtime / Execution
```

Resource examples include Provider, ProviderAccount, ExecutionTarget, AgentDefinition, HiveDefinition, Workspace, WorkItem, Question, Memory, Knowledge, Wiki, Skill, LearningCandidate, and CognitiveState resources.

Assignments are references/policies, not copies of the assigned resource.

Unknown future resource types remain representable through the generic inventory model.

---



## 7. Execution Planning

```
Agent intent or WorkItem
    ↓
Reasoning Requirement
    ↓
capability / policy matching
    ↓
Execution Planner
    ↓
Execution Target
    ↓
MAF execution / model call

LLM mode is a Workspace interaction path where the user chooses the execution target. Agentic mode uses this planner on behalf of the Agent/Hive.
```

A required capability must be explicitly supported. Unknown capability evidence does not qualify for a hard requirement.

Operational state remains separate from configured capability:

- quota;
- rate limits;
- health;
- availability;
- capacity;
- cost.

Planner output must be explainable without exposing credentials.

Running executions use immutable effective configuration snapshots.

Terminal execution state cannot be overwritten by a late provider result.

---



## 8. Human Intervention

### V1 WorkItem semantics

For V1, **one submitted document is one WorkItem**. A batch submission is a collection of independent WorkItems rather than one giant execution. Each WorkItem has its own identity, lifecycle, provenance, status, approvals, and terminal result.

A runtime incarnation is not inherently bound one-to-one to a WorkItem. A runtime may process multiple WorkItems according to its execution policy, and a WorkItem may require multiple executions/steps. The WorkItem is the durable unit of user-visible work; Execution remains the concrete execution/lifecycle unit.

An active Swarm may be represented as the set of Hive members participating in a WorkItem or related Question. The Swarm is derived/session state, not a persistent resource.



Approval is one intervention action, not the entire architecture.

The broader contract may eventually support:

```
Inspect / Approve / Reject / Pause / Resume / Cancel /
Retire / Shutdown / Redirect / Defer / RequestInformation
```

For V1, only **Approve / Reject on the business-app write** is required.

Intervention requests capture the target state/version at request time. A stale request is rejected rather than silently applied to a newer target state.

Intervention never bypasses authorization, capability, budget, or host validation.

---



## 9. Events, Snapshots, and Transactional Outbox

Hive uses append-oriented event history with durable snapshots as recovery aids.

Every durable event carries an explicit event type and **payload schema version**. Event readers/upcasters must be able to translate supported older payload versions to the current contract without rewriting historical events. Database schema versioning and event-payload versioning are separate concerns.

Phase 1.7 establishes the durable persistence primitive inside `Hive.Persistence`:

- an event stream is identified by an existing `ResourceReference`;
- each stream has an explicit positive sequence/version;
- events remain immutable and append-only;
- snapshots store the latest reconstructed state for a stream and its snapshot payload schema version;
- an outbox row is created from the same event that caused the durable state change;
- the event, optional snapshot replacement, and corresponding outbox row commit in one SQL transaction;
- an optimistic expected-version check prevents two writers from silently appending the same stream version;
- unique event and stream-version constraints protect duplicate writes at the database boundary.

The durable event store remains generic. It does not own Agent execution, workflow scheduling, polling, provider transport, or management policy.

Snapshot reconstruction is a separate deterministic contract over `EventEnvelope` values. A registered reducer handles a known event type and current payload schema; older supported payloads are normalized through the existing Core upcaster registry before reduction. Historical events are never rewritten during upcasting or folding.

When deferred follow-up work is required:

```
Event + Snapshot + Outbox
        ↓
one database transaction
```

Required crash-safety properties:

- committed events cannot lose their corresponding outbox work;
- rollback leaves neither the event nor its outbox work;
- duplicate outbox delivery is safe;
- replay is deterministic where the contract requires it;
- terminal execution state cannot be overwritten by late provider completion.

The outbox is not a general distributed message broker.

---