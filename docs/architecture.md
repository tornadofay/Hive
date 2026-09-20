# Hive — Architecture & Roadmap (Living Document)

Last updated: 2026-09-20 (rev 7 — general-purpose platform scope; persistent cognition, first-class resources, host integration, intervention, portability, management, examples, and production-test requirements)

Treat this file as the architectural source of truth. Update it before any structural code change.

Status lives only in `Hive_Current_Status.md`.
The current implementation slice lives only in `Hive_Active_Work.md`.
The ordered implementation plan lives in `roadmap.md`.

## 0. Purpose & Scope

Hive is a **general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems**.

Hive is not defined by one workflow or one host application.

A concrete workload such as:

```text
document/image
    → extraction
    → validation
    → business-app interaction
```

is simply one workload that can emerge naturally from Hive's generic capabilities:

- host context and UI integration;
- agent execution;
- provider/model selection;
- structured output;
- tools;
- authorization;
- memory / knowledge / skills;
- persistent cognition;
- human intervention;
- coordination;
- persistence and recovery;
- observability.

Document/image extraction is therefore a **capability and example workload**, not Hive's purpose.

### Primary design goals

Hive must provide:

1. A reusable agent/runtime platform rather than a workflow-specific application.
2. Persistent cognitive runtimes whose state survives execution boundaries and process restarts.
3. First-class, governable resources for Skills, Knowledge, Wiki, Memory, Learning Candidates, capability assignments, and runtime overrides.
4. Provider-neutral capability-aware execution planning.
5. A generic host integration boundary capable of working with APIs, desktop applications, WinForms controls, bindings, native data sources, and bounded host objects.
6. General human intervention beyond simple approval.
7. Portable Hive configuration without introducing a second configuration model.
8. A reusable WinForms management surface over authoritative Hive state.
9. Production-oriented automated tests covering normal paths, concurrency, recovery, stale state, security boundaries, and edge cases.
10. MAF-based orchestration where MAF already provides the required mechanism, without reimplementing it.

### Architectural hierarchy

```text
Tenant
  └── Users / Principals
       └── Workspaces
            └── Agents
                 ├── Runtime Instances
                 │    └── Executions
                 ├── Resources
                 └── Hives
```

The hierarchy is a logical ownership model. Not every deployment must use every level.

---

## 1. MAF Dependency Boundary

**Hard rule: use Microsoft Agent Framework whenever it already owns the required behavior. Hive adds only the semantics and platform contracts MAF does not own.**

The boundary is reviewed against Microsoft's current Agent Framework workflow documentation:
https://learn.microsoft.com/en-us/agent-framework/workflows/

Current MAF workflow capabilities include agent participation, sequential/concurrent/handoff/group-chat/Magentic orchestration, human-in-the-loop requests, checkpoints/resume, workflow events, and observability.

### MAF owns

- agent/workflow execution mechanics;
- workflow graph execution;
- Sequential / Concurrent / Handoff / Group Chat / Magentic orchestration;
- workflow checkpoints and resume where applicable;
- workflow-level request/response mechanics;
- actual model-service invocation through the selected model client;
- framework-level tool invocation mechanics;
- framework-level workflow events and execution flow.

### Hive owns

- tenant/user/workspace/resource ownership;
- agent identity and lifecycle semantics;
- persistent cognitive runtime;
- cognitive state and cognitive strategy contracts;
- memory / knowledge / wiki / skills / learning governance;
- resource inventory and capability assignment;
- runtime capability overrides;
- provider control plane;
- Execution Target selection policy;
- provider capability evidence;
- authorization and tool permission policy;
- generic human intervention lifecycle;
- durable Hive state, event history, snapshots, and outbox semantics;
- host application integration;
- WinForms UI Context / Control Adapters;
- configuration portability;
- management facade;
- Hive membership, roles, governance patterns, and later collective cognition;
- Hive-specific observability, budgets, diagnostics, audit, and safety policy.

Hive must never build a second workflow/orchestration engine merely because Hive needs additional semantics around an MAF workflow.

A future MAF feature may replace a Hive implementation only after an explicit migration decision and architectural review.

---

## 2. Core Tech Stack & Provider Platform

| Concern | Choice | Why |
|---|---|---|
| Language / runtime | C# / .NET 10 only | Clean modern baseline |
| Agent programming model | Microsoft Agent Framework | Reuse framework agent/workflow machinery |
| Execution model | Ephemeral execution + durable Hive state + transactional outbox | Crash-safe platform behavior without introducing a distributed orchestrator into the core runtime |
| Persistence | SQL Server; LocalDB for development | Hive-owned database, isolated from host business databases |
| Vector storage | SQL Server `VECTOR` / `VECTOR_DISTANCE` behind `IVectorStore` | Keeps vector storage behind a replaceable contract |
| Provider adapter | One shared OpenAI-compatible adapter for compatible providers and local servers | One wire-format implementation, many configurations |
| Provider | Vendor/service integration | Connection and transport identity |
| ProviderAccount | Credential/account/project under a Provider | Separates vendor integration from credentials |
| Execution Target | Provider + ProviderAccount + endpoint + model/deployment | Concrete capability-bearing execution environment |
| Capability state | `Supported` / `Unsupported` / `Unknown` | Missing evidence remains unknown |
| Selection requirement | `Required` / `Preferred` / `Optional` / `Forbidden` | Capability need is independent of provider identity |
| Selection mode | `Auto` / `Preferred` / `Fixed` | Separates preference from hard target pinning |
| Cost policy | `FreeOnly` / `FreePreferred` / `NoRestriction` | Cost is separate from capability |
| Secrets | Encrypted at rest, dedicated secret-store contract, redacted elsewhere | One security boundary without unnecessary vault infrastructure |
| Local development | SQL Server LocalDB | Simple development setup |

### Provider control plane

```text
Provider
   ↓
ProviderAccount
   ↓
Execution Target
   ├── capability evidence
   ├── operational evidence
   ├── quota/rate information
   ├── cost state
   └── availability/health state
```

No model or provider is silently assumed to support a capability because the provider is known.

### Explicitly deferred unless a real requirement appears

Temporal, Dapr, PostgreSQL/pgvector, Elasticsearch/OpenSearch, Akka.NET, Orleans, DiskANN, a custom Hive workflow engine, and a separate external secrets-vault architecture.

---

## 3. Persistent Cognitive Runtime

Persistent cognition is a **core Hive subsystem**, not a late optional feature.

A Runtime Instance represents a continuing agent identity. Individual Executions are temporary work units owned by that runtime.

```text
Agent Definition
      ↓
Runtime Instance
      ↓
Persistent Cognitive State
      ├── observations/events
      ├── beliefs
      ├── attention/workspace
      ├── goals
      ├── intentions
      ├── plans
      ├── memory links
      ├── experience
      ├── cognitive decisions
      ├── impasses
      └── recovery/checkpoint state
      ↓
Execution / Tool / Workflow
      ↓
Outcome / Experience
      ↓
State revision
```

### Cognitive Kernel

The Cognitive Kernel owns the stable runtime substrate:

- cognitive identity;
- lifecycle;
- state version/revision;
- event activation;
- persistence boundaries;
- recovery;
- concurrency ownership;
- provenance;
- intervention boundaries.

The Cognitive Kernel does not impose one universal reasoning algorithm.

### Cognitive Strategy

A Cognitive Strategy is replaceable.

It may use:

- deterministic rules;
- state/policy/memory lookup;
- bounded reasoning;
- model calls;
- tools;
- planning;
- hybrid combinations.

A strategy must be able to decide that **no model call is necessary**.

### Reasoning Requirement

Cognition determines **what reasoning capability is required**.

Execution planning determines **where/how that capability should execute**.

These are separate decisions.

### Death and reincarnation

"Death" is not deletion.

```text
Execution death
Runtime death
Agent lifecycle death
Cognitive identity death
```

Durable event/state history survives according to lifecycle policy.

Postmortem produces evidence and candidate changes. It does not silently rewrite authoritative cognitive state.

Reincarnation creates a new runtime incarnation from durable state plus accepted governed updates.

---

## 4. First-Class Resource Model

Every major Hive-managed capability is represented as a Resource.

```text
Resource
 ├── Provider
 ├── ProviderAccount
 ├── ExecutionTarget
 ├── AgentDefinition
 ├── HiveDefinition
 ├── Skill
 ├── Knowledge
 ├── Wiki
 ├── Memory
 ├── LearningCandidate
 ├── CapabilityAssignment
 ├── Conversation
 ├── AgentState
 ├── CognitiveState
 └── future resource types
```

Canonical shape:

```text
Resource {
    Identity
    Owner
    Scope
    Version
    Provenance
    Lifecycle
    Permissions
    Metadata
}```

### Canonical scopes

`Global`, `Tenant`, `User`, `Workspace`, `Agent`, `Runtime`, `Execution`.

Missing identity is valid only where a subsystem explicitly permits anonymous/single-user operation. Identity-required boundaries fail closed.

### Capability assignments

Assignments connect agents/runtimes to reusable resources.

```text
Agent
  ├── Skill assignment
  ├── Knowledge assignment
  ├── Wiki assignment
  ├── Memory-family policy
  └── Tool permission assignment
```

The assignment is distinct from the resource itself.

### Runtime overrides

Runtime Instances can override profile defaults using tri-state semantics:

```text
Inherit
Enabled
Disabled
```

Overrides are runtime-scoped and become part of the immutable execution snapshot.

They never silently mutate the persistent Agent Definition.

---

## 5. Memory / Knowledge / Skills / Learning

These are first-class systems, not prompt attachments.

### Skills

Reusable, versioned capability/procedure definitions with:

- identity/version;
- description and contract;
- dependencies;
- constraints;
- provenance;
- lifecycle;
- capability assignments.

Executable handlers are runtime-owned registrations and are never serialized into resource configuration.

### Knowledge

Knowledge is the general reusable information abstraction.

A Wiki is one managed persistent knowledge source under Knowledge.

Knowledge may include:

- structured facts;
- documents;
- relationships;
- summaries;
- indexed text;
- embeddings;
- source/provenance metadata.

### Memory

Memory is explicitly scoped.

At minimum Hive supports contracts for:

- working/execution memory;
- episodic memory;
- semantic memory;
- procedural memory;
- future memory families.

Private runtime memory remains isolated.

Shared memory requires explicit scope and authorization.

### Learning Candidates

Learning creates typed candidates rather than directly changing authoritative resources.

```text
Experience
   ↓
Learning analysis
   ↓
LearningCandidate
   ├── proposed target
   ├── evidence
   ├── confidence
   ├── provenance
   ├── source runtime/execution
   └── proposed scope
   ↓
validation / authorization / review / policy
   ↓
promotion or rejection
```

Learning modes and capability enablement are separate concepts.

Learning is not model-weight training.

---

## 6. Rich Execution Planning

Hive separates:

```text
Agent intent
    ↓
Reasoning Requirement
    ↓
Capability matching
    ↓
Execution Planner
    ↓
Execution Plan
    ↓
Execution Target
    ↓
MAF/model execution
```

### Capability requirements

Each capability requirement declares one of:

- Required;
- Preferred;
- Optional;
- Forbidden.

A required capability must be explicitly supported.

Unknown is never silently promoted to Supported for a required requirement.

### Selection policy

The planner supports:

- Auto;
- Preferred;
- Fixed.

### Cost policy

Cost is independent from capability:

- FreeOnly;
- FreePreferred;
- NoRestriction.

Unknown cost does not qualify as free under FreeOnly.

### Operational state

The planner may consider, separately:

- authorization;
- capability;
- quota;
- rate limits;
- health;
- availability;
- capacity;
- cost.

Observed operational state is not the same thing as configured provider capability.

The planner produces explainable selection diagnostics without exposing credentials or sensitive payloads.

---

## 7. Host Identity Propagation

Hive does not authenticate users itself, but it carries trusted host identity context through the system.

Canonical identity layers:

```text
Deployment
  → Tenant
    → Principal
      → User
        → Session
          → Workspace
            → Agent
              → Runtime
                → Execution
```

These identities remain distinct.

An execution receives an immutable identity snapshot.

Identity propagates where applicable through:

- authorization;
- tools;
- memory;
- knowledge;
- skills;
- learning;
- policy;
- events;
- tracing;
- evaluation;
- persistence;
- workspaces;
- human intervention;
- configuration import/export.

Identity provides context.

Identity is not authorization.

---

## 8. Host Integration and UI Context / Control Adapters

Hive can integrate with applications that expose an API **or** applications whose useful state exists inside their UI.

The host integration boundary is generic and UI-agnostic in Core. WinForms-specific behavior belongs in the WinForms integration assembly.

### Host integration layers

```text
Host API / service
       or
WinForms UI
       ↓
Hive Host Integration Contract
       ↓
bounded context representation
       ↓
Agent / Tool / Cognitive Runtime
```

### WinForms UI Context

The WinForms integration supports the host's native data representations rather than forcing them into one canonical representation.

Common supported representations include:

- Forms;
- UserControls;
- control trees;
- data bindings;
- BindingSource;
- DataTable / DataView / DataSet;
- arrays;
- IList / IReadOnlyList / IBindingList and similar collection contracts;
- dictionaries / key-value collections;
- POCOs and records;
- application-owned object graphs;
- bounded projections;
- explicit Control Adapters.

There is no Hive-wide canonical DataTable model.

A DataTable is simply one supported .NET representation. Hive preserves the native source when the host needs identity, binding behavior, editing, or type information, while also supporting a bounded normalized projection when an agent context needs normalization.

### UI Context rules

- Prefer the host's native/bound source when it already exposes the needed information.
- Do not scrape visible pixels or text when a native source is available.
- Do not require the host to convert a source into DataTable, JSON, dictionaries, or another intermediate representation merely so Hive can consume it.
- Preserve source-specific semantics where they matter to the host.
- Use bounded projections only at the context boundary that actually needs normalization.
- Object discovery is bounded, read-oriented, cycle-safe, cancellation-aware, and non-executable.
- Discovery reports evidence; it does not invent business meaning.
- Host-defined semantics and authorization can enrich or override discovery.
- Sensitive data must be redacted according to host/Hive policy.
- Context limits must prevent unbounded traversal or accidental capture of the host application.
- Discoverability never grants authority or tool permission.

### Data-source and control adapters

Hive uses adapters to understand host structures without forcing every host into one representation.

Examples:

```text
DataGridView       → rows/columns + bound source metadata
BindingSource      → bounded current/list view
DataTable          → native table + schema/row projection
DataView           → filtered/sorted native view + bounded projection
IList<T>           → bounded typed collection view
IEnumerable<T>     → bounded enumeration snapshot
POCO / record      → bounded property graph
Dictionary         → bounded key/value projection
TextBox            → bounded text/value observation
ComboBox           → selected value + bounded options
Custom control     → explicit adapter contract
```

The adapter system is not a collection of special cases hidden behind one data type. It is a common inspection/projection contract plus representation-specific adapters where necessary.

This subsystem is a general host-integration capability. Document/image extraction is only one possible workload built on top of it.
---

## 9. General Human Intervention

Hive distinguishes **approval** from **intervention**.

Approval is one intervention action.

The generic intervention system supports actions such as:

```text
Inspect
Approve
Reject
Pause
Resume
Cancel
Retire
Shutdown
Redirect
Defer
RequestInformation
```

An intervention request records:

- target identity;
- target kind;
- requested action;
- observed state;
- observed revision/version;
- requester identity;
- correlation/causation;
- reason/metadata;
- lifecycle status;
- responder identity;
- resolution metadata.

### Stale intervention protection

A request is evaluated against the target state/version captured when the request was created.

If the target changed, the old request becomes stale rather than silently applying to the new state.

Competing resolutions are serialized per target where required.

The intervention system never bypasses authorization, capability checks, budgets, or host validation.

---

## 10. Events, State, Snapshots, and Transactional Outbox

Hive uses append-oriented state history.

State-changing actions produce events.

Durable snapshots accelerate recovery.

When a transition requires deferred follow-up work:

```text
Event + Snapshot + Outbox
        ↓
one database transaction
```

The outbox is processed after commit.

Crash safety requirements:

- a committed event cannot lose its corresponding outbox work;
- a rolled-back transaction leaves neither;
- replay is deterministic where the contract requires it;
- duplicate delivery is safe;
- terminal execution state cannot be overwritten by a late provider response.

Hive does not turn the outbox into a general distributed message broker.

---

## 11. Generic Resource Inventory

Management and host APIs expose a generic resource inventory.

Each inventory entry can expose:

- resource ID/type;
- display metadata;
- owner/scope;
- lifecycle state;
- version;
- enabled/disabled/effective state;
- provenance;
- source;
- relationships/dependencies;
- permission summary.

Known resource types may have specialized pages.

Unknown future resource types remain visible through the generic inventory instead of requiring a new hard-coded Agent model property.

This lets Hive evolve without repeatedly redesigning the Agent Definition.

---

## 12. Configuration Portability

Configuration portability is a first-class Hive capability.

A package serializes Hive's authoritative configuration contracts rather than creating a parallel configuration schema.

Configuration packages can contain, as applicable:

- general/system settings;
- Providers;
- ProviderAccounts where permitted;
- Models;
- Execution Targets;
- Agents;
- Hives;
- Skills and versions;
- Knowledge;
- Wiki;
- Memory configuration/policy;
- Learning configuration/policy;
- Tools and tool definitions;
- capability assignments;
- permissions/policies;
- runtime defaults and override policy;
- other Hive-owned configuration resources.

The package does **not** contain:

- live runtime objects;
- active executions;
- synchronization primitives;
- transient process state;
- executable handlers;
- arbitrary host objects.

Packages are versioned, compatibility checked, and explicit about conflicts.

Credentials are excluded by default. Credential-bearing export is explicit and protected.

Import must validate schema/version compatibility and require explicit conflict behavior.

---

## 13. Management Surface

`Hive.Management` is the authoritative management facade.

The WinForms host is a thin presentation shell over this facade.

### Configuration shell

`HiveSettingsForm` contains:

- navigation surface;
- content surface;
- registration of configuration areas;
- shared `HiveConfigurationContext`.

The shell contains no feature-specific persistence or business logic.

### Required management areas

1. Providers / Models / Execution Targets
2. Agents
3. Cognition
4. Learning Review
5. Knowledge / Wiki
6. Skills
7. Storage
8. Runtime Diagnostics
9. Human Intervention
10. Generic Resource Inventory
11. Configuration Import / Export
12. Host Integration / UI Context diagnostics

Every area is a separate page/folder and uses the same management facade.

---

## 14. Example Application

Hive includes a dedicated example application that demonstrates the public architecture instead of becoming a hidden second implementation.

The example application is a development/documentation surface and integration host, not a replacement for automated tests and not a separate implementation phase.

Examples are added alongside the feature they demonstrate. The relevant implementation slice should add the appropriate automated tests, example scenario, complete public-API snippet, and manual smoke path when the capability has meaningful UI/host behavior.


The example application must have:

- clear separation between reusable library code and example code;
- one independently navigable example per major capability;
- complete, copyable code snippets for every example;
- an explanation of inputs, expected output, lifecycle, and relevant contracts;
- examples of both normal and failure/recovery paths where appropriate.

Example categories include:

- basic agent execution;
- multiple agent orchestration;
- persistent cognitive runtime;
- Skills;
- Knowledge / Wiki;
- Memory;
- Learning Review;
- execution planning;
- human intervention;
- configuration export/import;
- WinForms UI Context / Control Adapters;
- runtime diagnostics;
- multi-runtime isolation;
- crash/resume;
- document/image extraction as one workload example.

The example application must consume the same public APIs used by real hosts. Example-only shortcuts that bypass management, policy, persistence, or authorization are prohibited.

---

## 15. Testing & Production Readiness

Testing is part of the architecture, not a final cleanup task.

### Unit tests

Cover deterministic logic and small contracts:

- serialization;
- resource scope/ownership;
- identity value semantics;
- planner rules;
- capability matching;
- intervention state transitions;
- stale detection;
- cognitive state transitions;
- learning governance;
- runtime override resolution;
- resource inventory rules;
- configuration package validation;
- redaction;
- error classification.

### Contract/integration tests

Cover boundaries where unit tests are not enough:

- SQL persistence;
- migrations;
- transactional event/outbox behavior;
- snapshot recovery;
- provider adapters through fake/local infrastructure;
- MAF integration boundaries;
- configuration import/export;
- host integration contracts;
- real binding/data-source behavior.

### System / end-to-end tests

Use the assembled Hive components with fake/local external dependencies to verify important cross-boundary paths:

- host input;
- identity;
- resource policy;
- execution planning;
- MAF workflow/agent execution;
- persistence;
- intervention;
- recovery;
- resulting state.

A whole-system automated test is valuable, but it is not a unit test merely because it runs automatically. Keep system tests targeted and deterministic rather than duplicating every unit assertion at end-to-end level.

### Concurrency/recovery tests

Must cover:

- concurrent runtime instances;
- concurrent executions;
- competing interventions;
- stale requests;
- late provider completions;
- cancellation vs completion races;
- process crash/restart;
- outbox duplicate delivery;
- snapshot recovery;
- configuration changes during active executions.

### Security tests

Must cover:

- unauthorized resource access;
- cross-scope access;
- private-memory isolation;
- tool permission violations;
- credential leakage;
- diagnostic redaction;
- unsafe configuration import;
- host-object boundary violations.

### WinForms tests

The WinForms host requires focused UI smoke/automation coverage for:

- configuration navigation;
- provider configuration;
- agent selection;
- cognitive diagnostics;
- learning review;
- intervention;
- resource inventory;
- UI Context / Control Adapter discovery;
- import/export;
- example workflows.

The majority of correctness coverage belongs in library/contract tests. UI tests verify composition and critical user paths rather than reproducing all domain logic through the UI.

No test claim may be recorded without an actual automated test.

---

## 16. Non-Negotiable Engineering Rules

1. Hive is a general-purpose platform; no single workload defines the architecture.
2. Use MAF for behavior MAF already owns; do not reimplement equivalent orchestration.
3. Every agent runtime owns its mutable cognitive state; shared infrastructure is reused only through concurrency-safe contracts.
4. State-changing persistence is append-oriented; mutable projections/snapshots are derived recovery aids.
5. Running executions use immutable effective configuration snapshots.
6. Capability state is explicitly Supported / Unsupported / Unknown.
7. Capability requirements are explicit Required / Preferred / Optional / Forbidden.
8. Rate limits, quota, health, capacity, and cost are separate operational concerns.
9. The LLM is a requester/reasoning component, never an authority.
10. Authorization, approval, intervention, budgets, and host validation are enforcement boundaries.
11. Human approval is one intervention type, not the entire intervention system.
12. Identity is context, not authentication and not authorization.
13. Private runtime state and memory are isolated by explicit ownership.
14. Runtime overrides never silently mutate persistent agent configuration.
15. No secrets in logs, traces, event payloads, diagnostics, planner explanations, or example output.
16. No empty catch blocks.
17. One JSON serialization stack.
18. No duplicated implementation of the same computation.
19. Structured error classification is preferred over string matching.
20. Timeout and budget settings are explicit and validated.
21. Configuration surfaces must actually drive behavior and have tests.
22. Repeated ID lookups use real indexes.
23. Terminal state is protected from late completions.
24. Network-provider automated tests never call a real vendor.
25. Host UI discovery is bounded and never grants authority.
26. Discovered objects are context until explicitly exposed through an authorized tool boundary.
27. Configuration portability serializes authoritative Hive contracts rather than creating a second model.
28. Example code must use the same public boundaries as production hosts.
29. Every major feature has normal-path and edge-case automated tests.
30. Status lives only in `Hive_Current_Status.md`.
31. Current work lives only in `Hive_Active_Work.md`.
32. Phases are plain integers.
33. Complete the active slice fully without building ahead into future slices.
34. Do not claim verification that was not actually performed.

---

## 17. Core Solution Layout

The initial solution is:

```text
Hive.Core
Hive.Agents
Hive.Persistence
Hive.Coordination
Hive.Tools
Hive.Providers.OpenAICompatible
Hive.Management
Hive.Host.WinForms
Hive.Example.WinForms
Hive.Tests
```

Reference direction:

```text
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Management
   ↑
Host.WinForms / Example.WinForms

Coordination may depend on Core + Agents and MAF.

Tests reference the contracts/components under test.

Host.WinForms and Example.WinForms never bypass Hive.Management for management/persistence operations.
```

---

## 18. Roadmap

### Phase 0 — Foundations

Solution, resource contracts, identity, persistence bootstrap, serialization, error model, clocks, event envelope, test harness, engineering rules.

### Phase 1 — Provider Platform & Execution

Provider / ProviderAccount / Execution Target, secret storage, provider adapter, capability evidence, rich execution planner, immutable execution snapshots, execution lifecycle, budgets, terminal-state protection.

### Phase 2 — Persistent Cognition & First-Class Resources

Cognitive Kernel, Cognitive Strategy, Reasoning Requirement, persistent cognitive state, goals, beliefs, intentions, plans, experience, Memory, Knowledge, Wiki, Skills, Learning Candidates, capability assignments, runtime overrides, generic resource inventory.

### Phase 3 — Host Integration

Generic host context boundary, host identity propagation, WinForms UI Context, Control Adapters, native data sources, bindings, bounded object discovery, DataTable/projections, safe context ingestion.

### Phase 4 — Management, Intervention & Portability

WinForms management shell, Providers/Models, Agents, Cognition, Learning Review, Knowledge/Wiki, Skills, Storage, Runtime Diagnostics, Resource Inventory, Human Intervention, configuration import/export, example application.

### Phase 5 — Multi-Agent Hive Coordination

Hive Definition, membership, roles, coordination through MAF, workspace/message boundaries, supervisor controls, shared claims/provenance, configurable membership.

### Phase 6 — Governance Patterns

Manager-led, Democratic/Voting, Adversarial/Critique, conflict resolution, strategy selection and policy.

### Phase 7 — Cognitive Safety

Goal drift detection, evidence/confidence controls, credit assignment, belief revision, learning safety, cognitive intervention boundaries.

### Phase 8 — Multi-Tenancy, Scale & Extensibility

Authentication boundary, broader deployment concerns, externalized/distributed execution decision if justified, MCP/tool extensibility, future host surfaces.

### Phase 9 — Observability, Operations & Replay

Metrics taxonomy, dashboards, CI/CD, replay regression, resilience testing, production diagnostics, UI automation coverage, example verification.

---

## 19. Open Questions

- Which authentication provider should Phase 8 support first?
- Which business-app integration protocols should receive first-class adapters?
- Which first voting/conflict-resolution rules should Phase 6 expose?
- Which WinForms UI-automation framework should be used?
- Which additional host UI technology should follow WinForms?
- Which knowledge/vector workloads justify specialized indexing beyond the SQL Server vector contract?
