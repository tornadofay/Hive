# Hive — Architecture (source of truth)

Last updated: 2026-09-21 (rev 22 — production CRUD/editor UX composition)

Status lives only in `Hive_Current_Status.md`. Current work slice lives only in `Hive_Active_Work.md`. The ordered implementation plan lives in `roadmap.md`. This file does not restate implementation status.

## 0. Purpose & Scope

Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems.

### V1 forcing function

V1 has a real forcing function: automate data entry from documents and images into the user's existing business application.

That workflow is **not Hive's definition** and not a product-specific architecture. It is the first real deliverable that determines implementation order and proves that the general platform can solve a concrete problem.

The V1 pipeline is:

```
Document / image
      ↓
parse / rasterize
      ↓
text / vision capability
      ↓
structured extraction
      ↓
validation
      ↓
governed business-app write
      ↓
approval / result
```

Vision and image/document extraction are therefore capabilities that emerge from the general platform. They are not Hive's permanent scope.

Longer-term capabilities such as persistent individual cognition, offline Dream processing, structured Questions, collective cognition, generic host integration, broader governance, and additional host surfaces remain part of Hive's architecture, but they must not become prerequisites for the V1 pipeline.

### Primary design goals

1. A reusable Agent/Hive platform rather than a workflow-specific application.
2. A base `Agent` and base `Hive` that are complete and useful on their own.
3. Later generations such as `CognitiveAgent : Agent` and `CognitiveHive : Hive` that add behavior without changing the base contracts.
4. Provider-neutral, capability-aware execution planning.
5. Host integration sized to the actual current host requirement instead of a speculative universal adapter framework.
6. A reusable Workspace/control surface over authoritative Hive state and human intervention.
7. Production-oriented automated tests for normal paths, edge cases, concurrency, recovery, persistence, and security.
8. Microsoft Agent Framework (MAF) wherever MAF already owns the required mechanism.

### Logical ownership hierarchy

```
Tenant
  └── Users / Principals
       └── Workspaces
            ├── Agents
            │    ├── Runtime Instances
            │    │    └── Executions
            │    └── Resources
            └── Hives
                 └── Members (Agents and/or future Hive generations)
```

Not every deployment must use every level.


### 0.1 Identity and resource foundation

The Phase 0.3 identity/resource contract establishes stable typed identities without creating domain-specific persistence or authorization services prematurely.

The common resource identity set is:

- `DeploymentId`
- `TenantId`
- `PrincipalId`
- `UserId`
- `SessionId`
- `WorkspaceId`
- `AgentId`
- `HiveId`
- `RuntimeId`
- `ExecutionId`
- `WorkItemId`

Each identity is immutable, strongly typed, and non-empty. Identities are references, not mutable state objects.

Every Hive-owned persistent resource is represented through an immutable `ResourceEnvelope<TIdentity>` containing:

- resource identity;
- explicit owner (`PrincipalId`);
- explicit canonical scope (`Global`, `Tenant`, `User`, `Workspace`, `Agent`, `Runtime`, or `Execution`);
- positive resource version;
- provenance (creator principal, creation time, correlation/causation identifiers, and optional source resource reference);
- lifecycle metadata;
- immutable metadata values.

Scope matching is a structural boundary, not an implicit grant. The access context must always contain a `PrincipalId`; missing required identity components fail closed. The scope matrix is:

| Scope | Required context |
|---|---|
| Global | Deployment + Principal |
| Tenant | Deployment + Tenant + Principal |
| User | Deployment + Tenant + User + Principal |
| Workspace | Deployment + Tenant + Workspace + Principal |
| Agent | Deployment + Tenant + Agent + Principal |
| Runtime | Deployment + Tenant + Agent + Runtime + Principal |
| Execution | Deployment + Tenant + Agent + Runtime + Execution + Principal |

Scope matching does not itself grant authorization. Later management/security slices add resource-specific permissions and policy; they consume this explicit identity/scope boundary instead of replacing it.

`WorkItem` is the durable unit of user-visible work. Its identity is independent from Runtime and Execution identities. A WorkItem transition returns a new immutable state with the same WorkItem identity and a higher resource version; provenance and scope are preserved. One submitted document remains one WorkItem, while a batch is multiple independent WorkItems.

---

### 0.4 Persistence bootstrap

Phase 0.4 establishes the Hive-owned SQL Server persistence boundary without putting database dependencies into Hive.Core or the host application.

#### Ownership and dependency boundary

- Hive.Persistence owns all SQL Server connectivity, database bootstrap, migration execution, schema-version checks, and persistence-specific exceptions/results.
- Hive.Core remains dependency-light and has no SQL client, DbUp, or database connection-string dependency.
- Hive.Persistence may reference Hive.Core contracts, but Core never references Persistence.
- Hive's database is a separate database owned by Hive. It is never used as a gateway to the host application's business database.
- Hosts provide database configuration; credentials are not written to the repository, migration scripts, logs, or Hive database metadata.
- `HiveDatabaseOptions` enables database creation by default. Passing `createDatabaseIfMissing: false` is an explicit opt-out when the host requires pre-provisioned databases.

#### Database technology

- SQL Server is the only V1 persistence engine.
- SQL Server LocalDB is the supported local-development deployment of the same SQL Server boundary; it is not a separate persistence provider.
- Microsoft.Data.SqlClient is used for SQL Server connectivity.
- DbUp SQL Server support is used for ordered schema migrations rather than hand-written migration orchestration.
- DbUp migrations are embedded SQL resources in Hive.Persistence, numbered in execution order, and executed transactionally per migration script.

The implementation currently pins dbup-sqlserver 7.2.0 and Microsoft.Data.SqlClient 7.1.0. The first is the current stable DbUp SQL Server package and the second is the current stable Microsoft SQL client at the time this slice is implemented. citeturn544673view0turn598125search0

#### Schema version and migration journal

Hive maintains two separate pieces of migration metadata:

1. DbUp's migration journal records which migration scripts completed successfully.
2. A Hive-owned singleton schema-version row records the current logical Hive database schema version.

The schema-version row is updated inside the same migration transaction as the schema change it describes. A failed migration therefore cannot advance the Hive schema version. A clean database has no Hive schema-version row until the first migration completes successfully.

The code has one supported CurrentSchemaVersion. Before running DbUp, Hive reads the stored logical schema version when present. A stored version greater than the code's supported version is an incompatible future schema and migration/execution is rejected before normal application use. Older supported versions are passed to DbUp for forward migration.

The DbUp journal and schema-version metadata use explicit primary/unique indexes for deterministic lookup. Domain-table indexes are added with the domain persistence slice that introduces each table rather than being guessed during this bootstrap phase.

#### Migration failure and compatibility semantics

Migration execution must be deterministic:

- clean database → ordered migrations → current schema;
- current database → no-op migration run;
- failed migration → failing script is not journaled and the logical schema version remains at the previous successful version;
- stored future schema version → typed incompatible-schema failure before normal migration proceeds;
- malformed or unavailable migration configuration → typed configuration/persistence failure rather than partial-success reporting.

A migration runner exposes the outcome as structured Hive.Persistence state instead of requiring callers to parse DbUp log strings.

#### Initial schema boundary

The bootstrap schema contains only persistence infrastructure required at this phase: Hive schema metadata and the DbUp journal. Agent, Hive, WorkItem, event-log, snapshot, outbox, provider, and other domain tables are introduced by the slices that own their persistence semantics.

This prevents Phase 0.4 from prematurely freezing later aggregate schemas while still establishing a real Hive-owned database, migration history, compatibility protection, and indexed metadata foundation.

---

### 0.5 Test harness

Phase 0.5 establishes reusable verification infrastructure without creating production abstractions that belong to later provider, Agent, or persistence slices.

#### Test boundaries

- `Hive.Tests` remains the authoritative automated test suite.
- Test doubles live in `Hive.Tests` unless a later production contract explicitly requires a reusable public fake package.
- No fake provider contract is added to `Hive.Core` or `Hive.Providers.OpenAICompatible` before the production provider boundary exists.
- Unit tests must not require SQL Server, network access, provider credentials, MAF services, or the WinForms host.
- Persistence integration tests are explicit boundary tests and may use the configured developer SQL Server/LocalDB database strategy.

#### Fake clock

`FakeClock` implements the existing `Hive.Core.IClock` contract. It starts at an explicitly supplied UTC instant and advances only through test-controlled operations. Tests therefore avoid sleeping or depending on wall-clock time for deterministic lifecycle/event assertions.

#### Test-only fake provider

`FakeProvider` is a deterministic test double owned by `Hive.Tests`. It records requests, returns configured responses, can produce configured failures, and honors cancellation without network access. Its request/response types are test-only and are not production provider contracts.

#### Persistence test strategy

Persistence integration tests use an explicit connection string stored in `Hive.Tests/HivePersistenceTestConfiguration.cs`, with the Hive test database names derived by each test. The tests create the database automatically when missing and may reuse a database only when the test explicitly resets the relevant schema. No environment variables or hidden machine-specific prerequisites are required.

#### Event test conventions

Event tests use fixed `DateTimeOffset` values, deterministic typed IDs, and explicit payload JSON. Serialization round-trips compare contract fields rather than incidental JSON property ordering. Upcast tests construct the exact older payload version and verify the normalized current contract.

Test infrastructure is verification support only. It does not become a second runtime, provider, persistence, or orchestration architecture.

### 0.6 WinForms UI/UX Foundation

Phase 0.6 establishes the shared WinForms visual foundation used by `Hive.Host.WinForms` and `Hive.Example.WinForms`. The rendering dependency is an implementation detail of `Hive.Host.WinForms.UI`.

#### Rendering dependency boundary

- `Hive.Host.WinForms.UI` is the only Hive project permitted to reference ReaLTaiizor.
- ReaLTaiizor is pinned to version `3.8.2.1` for this slice. 
- Consuming forms and platform services reference Hive-owned UI contracts only; they do not reference ReaLTaiizor namespaces or controls directly.
- Hive-specific controls that use ReaLTaiizor do so behind composition/adaptation boundaries so the underlying rendering library can be replaced without changing consuming-form contracts.

#### Theme contract

The UI foundation exposes a Hive-owned theme vocabulary:

- `HiveThemeMode`: `Light`, `Dark`, `System`;
- semantic palette roles for application background, surface, elevated surface, text, muted text, border, accent, accent-hover, accent foreground, input, disabled input, disabled text, and selection;
- typography roles for body and heading text;
- spacing tokens for the common 4/8/12/16/24 pixel scale;
- common visual-state tokens for normal, hover, pressed, focused, disabled, and selected states.

Theme resolution is deterministic. `System` resolves from the Windows application-theme preference when available and falls back to Light when the OS setting cannot be read.

The theme manager is stateful but UI-only. Changing the mode raises one theme-change notification. Consumers reapply the effective theme to their attached control trees in response to that notification. The manager does not own application settings, persistence, Agent/Hive state, or host business data.

#### Reusable data-oriented UI composition

Hive reuses presentation and CRUD orchestration mechanics for data-oriented pages without assigning domain meaning to them. The reusable foundation may provide:
- a three-region list-page layout (header, action/filter region, content);
- a structured CRUD toolbar with a clear primary action, contextual edit/delete actions, refresh, and optional client-side search;
- a styled native `ListView`-based tabular/list surface where its native behavior is sufficient, with predictable selection and keyboard interaction;
- explicit loading, empty, and no-match states plus a compact record-count/status footer;
- an optional paging/navigation bar that owns page state and navigation events, not data retrieval;
- a generic `HiveCrudPage<TItem>` that owns Add/Edit/Delete/Refresh UI orchestration, selection, list population, search state, busy-state, empty-state presentation, keyboard interaction, and operation failure notification while receiving load/edit/delete callbacks from the consuming feature;
- reusable editor-layout composition for the repeated label/description + field + action-footer pattern, with consistent field spacing and a dedicated action footer;
- optional master/detail composition where a bounded list and selected-item details are useful.

The reusable page follows a consistent application UX hierarchy: page title/description first, query/filter controls and actions second, primary data surface third, and compact status feedback last. Destructive actions remain secondary and require confirmation. The reusable editor keeps domain validation and authorization outside the layout while providing a consistent field rhythm, readable labels/descriptions, and right-aligned primary/secondary actions.

`HiveCrudPage<TItem>` is a UI/application-boundary primitive, not an ORM or persistence abstraction. The consumer supplies columns/projections, filters, validation, authorization, persistence, and the domain-specific editor through callbacks or composition. The control does not know Provider, Agent, Resource, or any other domain schema, and it never creates or mutates domain state on its own. This lets Provider, Agent, Tool, Policy, and future configuration pages share the same CRUD interaction contract while retaining different columns and specialized edit forms.

`DataGridView` remains available when its richer native tabular behavior is specifically required. Hive does not introduce a generic ORM, repository, or domain CRUD model.
WinForms DPI behavior is delegated to the .NET 10 / WinForms platform rather than duplicated in Hive. Hive does not maintain a custom DPI helper or manual control-tree scaling layer. Normal forms and controls use WinForms' built-in scaling behavior; custom-painted Hive controls keep their own design geometry unless a concrete, measured DPI defect requires a focused exception.


#### Hive-owned controls and window shell

The foundation introduces only consumer-facing Hive contracts:

- `HiveForm` provides the shared rounded, borderless application-window shell, custom header, window movement, and theme-aware body surface.
- `HiveButton` provides a Hive-owned button surface with Primary, Secondary, and Navigation styles while hiding the ReaLTaiizor implementation detail.
- `HiveMessageBox` provides a Hive-owned semantic dialog with Information, Success, Warning, Error, and Question variants, standard `DialogResult` semantics, optional technical details, and copy support. It uses a compact dialog-specific shell rather than the full `HiveForm` application header: rounded surface, thin semantic accent bar, semantic circular icon, clear caption/message hierarchy, optional details panel, and right-aligned action buttons.
- `HiveForm` provides the reusable borderless rounded application-window shell and body surface.
- The custom application header supports title, subtitle, close, optional minimize/help actions, and window movement with a compact visual hierarchy similar to the established HAgent WinForms visual language.
- `HiveMessageBox` may take visual direction from the useful HAgent message-dialog characteristics, but its implementation remains smaller, Hive-owned, theme-driven, and free of HAgent dependencies.
- Semantic message colors and navigation colors are theme tokens rather than form-specific constants.

The visual direction intentionally carries forward the useful HAgent characteristics—rounded windows, a distinctive header, strong semantic accents, compact navigation, and explanatory field labels—without copying HAgent's monolithic UI implementation. Hive keeps the implementation smaller, theme-driven, disposable, and independent of HAgent types.

Ordinary WinForms controls remain first-class. The foundation styles common native controls through the theme manager where practical; it does not create Hive-prefixed wrappers merely to rename every framework control.

#### Representative verification surface

A representative example form in `Hive.Example.WinForms` exercises:

- Light, Dark, and System mode selection;
- a HiveButton;
- a HiveMessageBox;
- representative native WinForms controls such as labels, text input, check boxes, and panels;
- disabled/focused/selected visual states where the control supports them.

The example form is the manual UI verification surface for this phase. No UI automation framework is introduced. Its navigation and content areas must use standard WinForms layout containers rather than overlapping absolute child placement so the example remains readable at its supported window sizes.

#### Replaceability contract

The public consumer surface consists only of Hive-owned theme contracts and the small Hive-specific controls introduced by this phase. The representative example must compile without a ReaLTaiizor namespace/import. Replacing ReaLTaiizor later must therefore be limited to `Hive.Host.WinForms.UI` and must not require unrelated form changes.

#### Scope boundary

Phase 0.6 establishes visual infrastructure only. It does not introduce Hive membership, Swarm, Agent/Hive runtime behavior, cognitive generations, Dreams, Questions, or Workspace feature behavior.
---

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
- WinForms management facade and configuration surface;
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

Current V1 provider configurations include compatible hosted/local targets such as Groq, OpenRouter, Cloudflare, Cerebras, NVIDIA, Google, and local OpenAI-compatible servers. The adapter contract remains vendor-neutral; adding another compatible provider should normally require configuration, not another transport implementation.

Deferred until a measured requirement exists: Temporal, Dapr, PostgreSQL/pgvector, Elasticsearch/OpenSearch, Akka.NET, Orleans, DiskANN, a custom Hive workflow engine, and a separate external secrets-vault architecture.

---

## 3. Agent / Hive Type Hierarchy

The hierarchy is a **creation-time type choice**, not a runtime promotion system.

```
Agent (base)                              Hive (base)
  instructions, tools, context,           membership, roles, communication,
  conversation, execution,                 delegation, authorization,
  resources, objectives,                   population, lifecycle,
  questions, patience,                     coordination via MAF,
  memory/simulation mechanisms             complete and useful alone
  complete and useful alone
       │                                        │
       ▼                                        ▼
CognitiveAgent : Agent                  CognitiveHive : Hive
  adds: Cognitive Kernel,                 adds: collective/shared cognition
  Cognitive Strategy,                     while each member keeps its own
  Reasoning Requirement,                  Agent/CognitiveAgent identity,
  persistent goals/beliefs/               cognition, and state
  intentions/plans,
  experience, self-model,
  death/wake/reincarnation,
  Dreams, and Questions
       │                                        │
       ▼                                        ▼
Future generations remain open-ended and may coexist with older generations.
```

### Type rules

1. A concrete Agent/Hive type is selected when that resource/runtime is created.
2. A normal `Agent` never silently becomes a `CognitiveAgent`.
3. A `CognitiveAgent` never silently becomes a normal `Agent`.
4. There is no runtime promotion/demotion feature in the base architecture.
5. Projects that need a different generation create the desired subtype explicitly.
6. Older and newer generations may coexist in the same deployment.
7. A subtype is strictly additive. It must not redefine, remove, or require changes to the ancestor contract.
8. Base runtime/persistence/execution infrastructure is written against the base contracts and does not need to know which descendant type is being used.
9. A subtype may use additional state and events owned by that subtype, but ancestor-owned state semantics remain stable.
10. A future generation may be introduced without forcing existing Agent/Hive implementations to change.
11. Agent generation is selected explicitly when the Agent is created; it never changes automatically during runtime or reincarnation.
12. A creator may request any supported Agent generation, including a CognitiveAgent, only when explicit authorization/policy permits that generation; generation is never inferred automatically from task complexity.
13. Agent generation and Hive membership are independent. A base Hive may contain CognitiveAgents and a base Agent may create or join Hives without changing type.
14. The Agent that sponsors a Hive is not the Hive's lifecycle owner. Sponsorship is a relationship; sponsor retirement, runtime death, or deletion does not automatically delete or retire the Hive or its members.

This gives Hive long-term flexibility without making type mutation a correctness problem.

### Base Agent mechanisms vs CognitiveAgent semantics

A base `Agent` may be substantially capable without being a `CognitiveAgent`. The boundary is **not** "simple features versus smart features"; it is whether the Agent owns adaptive cognitive interpretation and revision.

Base Agent mechanisms may include:

- **Objectives** = explicit work targets with lifecycle/status, completion criteria, priority, deadline, and dependency metadata. An Objective can make a base Agent objective-driven without giving it autonomous goal formation or reconsideration.
- **Memory infrastructure** = durable storage/retrieval for conversation, execution history, artifacts, tool results, checkpoints, configuration, and other explicitly addressed state. Cognitive memory semantics belong to `CognitiveAgent`.
- **Question protocol** = first-class question/answer transport, provenance, ownership, status, timeout, answer type, and waiting. The base Agent may know what required information is missing without dynamically generating cognitive inquiry strategies.
- **Patience / Understanding Gate** = a deterministic or policy-driven gate that prevents consequential work until required information or explicit confirmation is available. The principle is **understand the minimum required information before trying to solve**; it does not require the Agent to understand everything.
- **Simulation infrastructure** = generic bounded simulation/job execution, scenario inputs, predicted outputs, parallel simulation execution, and result storage. Cognitive `Dreams` are a higher-level use of this infrastructure.
- **Delegation and coordination interfaces** = request work from another Agent/Hive, await answers, and preserve provenance without requiring autonomous cognitive delegation decisions.
- **Lifecycle and persistence** = creation, suspension, death of an execution/incarnation, wake/recreation, durable state, and human management of inactive state.

These mechanisms are intentionally reusable by later CognitiveAgent implementations.

A mechanism becomes cognitive when the Agent can autonomously interpret and revise it as part of its own ongoing cognition—for example: forming/revising Goals, revising Beliefs, selecting and reconsidering Intentions, generating/choosing Questions, deciding what to Dream and why, interpreting simulation results, revising its self-model, or changing strategy from experience.

### Cognitive Kernel vs Cognitive Strategy

Inside `CognitiveAgent` only:

- **Cognitive Kernel** = durable cognitive substrate: identity binding, lifecycle, cognitive-state versioning, event history, recovery, concurrency ownership, and intervention boundaries.
- **Cognitive Strategy** = replaceable adaptive reasoning process: belief revision, attention, goal formation/reconsideration, intention selection, planning, impasse handling, reflection, learning, Dream selection/interpretation, Question generation/selection, and deterministic-vs-reasoning routing.
- **Reasoning Requirement** = what reasoning capability is required.
- **Execution Planning** = where/how the requirement executes.

A cognitive strategy may decide that no model call is necessary.

---

### Dynamic Hives, population, and Swarms

A base Agent may sponsor one or more persistent Hives as a normal delegation/coordination capability. The Agent does not become a Hive and does not need to be cognitively upgraded to create one.

A Hive may:

- add existing Agents of any supported generation when authorized;
- create or reuse Agent instances/definitions for missing specialties when authorized;
- manage membership and role assignment;
- coordinate member Agents;
- become Dormant when no active work requires it;
- later reactivate with its persistent membership and member state intact.

A Hive is a persistent resource. Its sponsor is a relationship, not an implicit lifecycle owner. Sponsor retirement, runtime death, or deletion does not automatically delete the Hive or its independent members.

A **Swarm is not a persistent resource or another hierarchy layer.** It is the temporary set of selected Hive members actively collaborating on a WorkItem, Question, or other bounded problem. It has no separate durable identity, repository, or independent lifecycle. When collaboration ends, the Swarm simply ceases to be active; the Hive and its Agents remain.

The active set may contain one member, several members, or all members of the Hive. A Hive does not require a Swarm to perform ordinary work.

For a solo Agent that is not currently inside a parent Hive:

```
Agent
  ↓
requires multiple specialties
  ↓
creates/sponsors persistent Hive
  ↓
Hive creates/reuses required membership
  ↓
selected members form an active Swarm for the problem
  ↓
work complete
  ↓
selected members leave the active set
  ↓
Hive may become Dormant
```

For an Agent that is already a member of a Hive, the default rule is different:

> A member Agent does not independently create a child Hive during normal Hive-managed work. It requests missing capabilities/specialists from the parent Hive, and the parent Hive remains the population authority.

A Hive may create or reuse a specialist Agent when a required specialty is missing. The created Agent remains an independent Agent entity with its own generation, identity, state, memory, and later lifecycle. A Hive may create a CognitiveAgent specialist when explicitly authorized by its population policy; the generation is not inferred automatically.

The term **Herd** may be used informally for a group's members, but it is not an architectural resource. **Hive** is the persistent collective; **Swarm** describes the currently collaborating subset.

## 4. V1 Document & Business-App Integration

The V1 forcing function is the complete pipeline from the first supported input image to a governed business-app write. The first V1 input is intentionally an image; additional document formats are additive later capabilities.

```
Submitted image
        ↓
image preparation / vision routing
        ↓
structured extraction
        ↓
typed candidate record
        ↓
validation
        ↓
write Tool
        ↓
PendingApproval
        ↓
Approve / Reject
        ↓
business-app result
```

The write remains a governed Tool. Hive's database is never a direct gateway to the host application's business database.

### 4.1 Dual Business-App Integration Contract

V1 supports **both API/service integration and bounded UI integration**. They are not mutually exclusive, and a single WorkItem or operation may use the API, the UI, or both.

**API/service path:** a write Tool may call the real application's supported API/service directly when that capability is available.

**UI path:** a bounded UI integration may inspect or operate on the actual host application's UI when needed, including when no usable API exists. The contract is defined against the real WinForms host rather than a universal UI abstraction.

For the V1 WinForms boundary, discovery can cover the Form hierarchy, `Form` instances, `UserControl` instances, `Control`-derived and custom controls, container controls such as Panels and GroupBoxes, nested descendants, and relevant runtime/data-source context. This discovery exists to provide bounded host context to the Agent; it does not grant permission to click, edit, invoke, or otherwise mutate controls.

API and UI integration may therefore be combined within one workflow—for example, using an API for data retrieval and a UI path for a host operation that has no equivalent API.

Generic cross-host integration remains later. V1 proves the concrete WinForms boundary first, then later phases may generalize proven patterns to other host technologies.


## 5. Workspace and Management Surface

`Hive.Management` is the authoritative management facade. `Workspace` is the authoritative human-facing operational surface over that facade; host UI code does not bypass the facade for management or persistence operations.

For V1, Workspace is intentionally a small operational surface over Hive.Management. It supports:

- image submission/attachments bound to WorkItems;
- WorkItem status and execution/activity;
- relevant execution/provider status;
- WorkItem notifications;
- pending approvals and the Approve / Reject action for the governed business-app write.

This V1 surface works with a single Agent and does not require Hive membership or Swarm state.

Later Workspace extensions are phase-gated by the capabilities that own their underlying state. These include:

- general **LLM mode** with explicit user model selection;
- **Agentic mode** with Agent/Hive-selected execution targets;
- Agent/Hive organization and topology;
- active Swarm membership;
- Questions, cognitive state, Dreams, and other later-generation views.

For later modes:

- In **LLM mode**, the user explicitly selects the model/execution target subject to normal capability and authorization policy, with a configured default available.
- In **Agentic mode**, the Agent/Hive selects an execution target through the normal Execution Planner and policy boundary. The Workspace displays the selected target and relevant diagnostics, but the user is not required to choose the model for every Agent decision.

Workspace is not a cognitive authority. It displays and controls authoritative Agent/Hive state; it does not invent Agent decisions or rewrite cognitive state outside the normal management/authorization contracts. It displays and controls authoritative Agent/Hive state; it does not invent Agent decisions or rewrite cognitive state outside the normal management/authorization contracts.

A business application can register a host context through a bounded public API such as:

```csharp
ai.Register(this);
```

For V1 WinForms integration, the registered context can expose bounded discovery of the complete relevant Form/control hierarchy, including UserControls, custom/inherited controls, Panels, GroupBoxes, other container controls, nested descendants, and relevant runtime/data-source context. The discovery boundary is structural/contextual and must be cycle-safe, bounded, cancellable, and read-oriented unless a separate action is explicitly authorized.

Registration binds host context to Workspace/Hive management and may reuse an existing specialized Agent. Registration does not by itself create a new Agent, create a Hive, or imply that multiple open forms must communicate. Host-specific specialization and lifecycle are explicit policy/configuration.

After Phase 2 establishes Hive membership and Swarm state, the Workspace can display Agent/Hive organization and the current collaborating subset as a Swarm. The visual representation does not itself create a Hive or Swarm; durable creation and membership follow the Agent/Hive contracts.

V1 management areas:

1. Workspace — V1 operational WorkItem/approval surface
2. Providers / Models / Execution Targets
3. Agents

Later areas are added only when their owning phase lands:

- Hive Membership
- Governance
- Agent/Hive organization and Swarm views
- LLM mode / Agentic mode
- Cognition / Dreams / Questions / Learning Review
- Knowledge / Skills / Memory
- Storage
- Runtime Diagnostics
- Human Intervention
- Resource Inventory
- Configuration Import / Export
- Generic Host Integration diagnostics

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

## 10. Cognitive Generations

### CognitiveAgent

`CognitiveAgent : Agent` is the first cognitive generation.

It adds:

- Cognitive Kernel;
- replaceable Cognitive Strategy;
- Reasoning Requirement;
- persistent cognitive state;
- observations and beliefs;
- bounded attention/workspace;
- goals;
- intentions;
- plans and methods;
- deterministic decision path;
- bounded probabilistic escalation;
- typed impasses;
- reconsideration;
- experience/history;
- governed death/postmortem/reincarnation.

It remains compatible with the ordinary Agent execution boundary and MAF.

### CognitiveHive

`CognitiveHive : Hive` is a later generation that adds collective/shared cognition while preserving each member's own Agent/CognitiveAgent identity, cognition, and state.

Collective cognition is additive. A CognitiveAgent remains a complete autonomous cognitive entity when outside a Hive, and a Hive does not become the owner of the member's individual goals, beliefs, plans, memory, self-model, or lifecycle.

It must not turn the base Hive into a requirement for ordinary Agents.

### Cognitive lifecycle

An Agent's persistent identity and cognitive state are distinct from any particular runtime incarnation:

```
Agent identity + persistent cognitive state
                  │
                  ▼
          Incarnation / runtime
                  │
               operates
                  │
                  ▼
                Death
                  │
        runtime no longer exists
                  │
                  ├───────────────┐
                  ▼               ▼
             Dream/analysis   human review/edit
                  │               │
                  └───────┬───────┘
                          ▼
                 persistent state
                          │
                          ▼
                     Wake / Reincarnation
                          │
                          ▼
                  new runtime incarnation
```

**Death** is the complete end of the current runtime/incarnation. It is not deletion of the Agent, its identity, or its persistent cognitive state.

A CognitiveAgent may remain fully usable as persistent state while no Agent runtime is active. The host application may also be completely shut down during this period. A later wake reconstructs a new runtime from the durable state plus any valid human or cognitive updates made while the Agent was inactive.

### Dreams

**Dreams** are bounded cognitive simulations/analyses performed against persistent Agent state without requiring the normal Agent runtime to be active.

Dreams may:

- replay or analyze historical experience;
- generate hypothetical alternatives;
- run multiple bounded simulations in parallel;
- compare predicted outcomes;
- explore plans or strategies before the next wake;
- identify unresolved questions or candidate state changes.

Dream output is never silently treated as an event that actually happened. Persistent records distinguish at least actual observations/experiences from simulations, hypotheses, predictions, and other non-observed results.

A Dream may produce candidate changes to goals, beliefs, plans, memories, self-model, skills, or other cognitive resources, but authoritative state changes remain subject to the same validation, ownership, authorization, provenance, and concurrency rules as other Hive-owned state.

Dream processing is subject to applicable authorization, model/provider quota, token/cost budget, time budget, concurrency/parallelism limits, retrieval/work limits, and other resource-governance rules. Being offline or asleep never bypasses those limits.

### Questions

**Questions** are first-class cognitive objects representing an information gap, uncertainty, decision point, or request for evidence.

A Question carries structured context, specialty/role requirements, provenance, answer state, and any required evidence or answer type. Questions are not merely strings appended to a prompt.

Questions are specialty-aware. In a Hive, different Agents may receive different questions about the same user objective based on their specialization. The system should recognize semantically duplicate questions and avoid redundant work when existing evidence is sufficient.

Answers remain attributable to the Agent, Dream/simulation, source, or other evidence that produced them. Individual Agents may use their own Questions independently; CognitiveHive can coordinate Question assignment, cross-agent evidence, synthesis, conflict handling, and collective reasoning.

### Future generations

Future generations may add different cognitive strategies or different Hive coordination models. They must consume stable base contracts and remain coexistable with earlier generations.

---

## 11. Cognitive Resources

Advanced persistent cognitive resources are delivered after the cognitive lifecycle branch exists. Persistent resources must preserve the distinction between actual experience and simulated/dreamed outcomes.

They include:

- working / episodic / semantic / procedural memory families;
- Knowledge and managed Wiki;
- versioned Skills;
- Learning Candidates and governed promotion;
- applicability/reliability state;
- resource assignments and runtime overrides.

Learning is not model-weight training. Model output produces evidence or a candidate; authoritative resource state changes only through Hive's validation, policy, authorization, and promotion boundaries.

---

## 12. Configuration Portability

Configuration portability is a later platform capability.

Packages serialize Hive's authoritative configuration contracts rather than creating a second model.

Possible contents include:

- Providers / ProviderAccounts;
- Execution Targets;
- Agents / Hives;
- Skills;
- Knowledge / Wiki;
- Memory configuration;
- Learning configuration;
- Tools;
- permissions/policies;
- runtime defaults.

Live executions, synchronization primitives, transient process state, arbitrary host objects, and executable handlers are excluded.

Credentials are excluded by default.

---

## 13. Example Application

`Hive.Example.WinForms` is a first-class public-API example and verification host, not a temporary demonstration.

Examples are added alongside the features they demonstrate. They use the shared WinForms UI foundation and scalable Category → Subcategory → Example navigation.

The Example host includes developer-oriented test execution tools, but it does not replace `Hive.Tests` or become the authoritative test runner.

Every major public feature example should include:

- normal path;
- relevant failure/recovery path;
- complete copyable public-API snippet;
- expected lifecycle/result.

The V1 example should demonstrate the document-to-business-app pipeline using the same public boundaries available to real hosts.

### 13.1 WinForms UI Foundation

The WinForms visual foundation is a platform-level host concern and is established in Phase 0 so later forms do not independently invent themes, controls, message boxes, spacing, or visual states.

Hive uses **ReaLTaiizor** as the selected third-party rendering layer. ReaLTaiizor remains behind `Hive.Host.WinForms.UI`; consuming forms and platform services do not reference it directly. The selected package version is pinned when the UI foundation is implemented.

Hive owns the consumer-facing UI contracts and design vocabulary:

- Light / Dark / System theme modes;
- semantic palette, typography, spacing, and common visual-state tokens;
- Hive-specific controls only where Hive needs additional behavior or styling;
- shared controls such as HiveButton and HiveMessageBox where a Hive-owned contract is useful.

The foundation must not become a complete replacement control toolkit. Standard WinForms controls remain valid when their native behavior and Hive styling are sufficient. A Hive-prefixed control is created because Hive needs a contract or behavior, not merely to rename a framework control.

This layer is replaceable: changing the underlying rendering library must not require unrelated forms to change their public Hive UI contracts.

### 13.2 First-Class Example Host

`Hive.Example.WinForms` is a permanent developer-facing application used to demonstrate public APIs, inspect platform behavior, and reduce developer friction while building Hive.

The shell uses a left-side Category → Subcategory → Example navigation surface and a right-side replaceable `UserControl`. Nested TabPages are not the primary gallery-navigation mechanism.

Examples implement a small contract such as:

```csharp
public interface IHiveExample
{
    string Category { get; }
    string Subcategory { get; }
    string Title { get; }

    UserControl CreateView(IServiceProvider services);
}
```

Only designated example assemblies are scanned. Adding an example should require implementing the contract, not editing the shell's central registration code.

The Example host may provide a developer test panel that invokes `dotnet test` as an external process against `Hive.Tests`. The Example host must not become an xUnit runner and must not embed xUnit runner internals. Example self-checks may provide immediate feedback but are not authoritative test results.

The Example host is a first-class project from repository scaffolding onward, grows with Hive, and uses the same Hive UI foundation as all other WinForms surfaces.

### 13.3 UI Foundation Scope Boundary

The UI foundation is infrastructure, not a mechanism for pulling future platform capabilities into Phase 0. It must not require Hive membership, Swarm, CognitiveAgent, CognitiveHive, Dreams, Questions, or other later-generation behavior.

Phase 1 and later features consume the shared UI foundation instead of creating parallel form/control systems.


## Engineering Standards

Hive is built for production real-world applications. The default coding standard is clean, warning-free, lightweight, and performance-conscious without sacrificing correctness or maintainability.

### Correctness and contracts

- Nullable reference types remain enabled. Nullability mismatches are fixed at the contract boundary; they are not suppressed.
- Affected projects must compile with zero errors and zero new warnings before a slice is considered complete.
- Public APIs expose only required consumer contracts and avoid leaking framework/vendor implementation types.
- One authoritative implementation owns each validation, state transition, serialization rule, calculation, or policy decision.
- Failures use structured/typed classification with useful context. Boundary catches must not silently swallow the underlying cause.

### Performance

- Prefer immutable cached derived state for stable data such as theme definitions, parsed configuration, capability maps, or other repeatedly requested values.
- Avoid unnecessary allocations, repeated parsing/reflection, repeated registry/file/database/network access, and avoidable LINQ/delegate overhead in hot paths.
- Avoid speculative micro-optimization. Optimize measured or contractually important costs such as allocation rate, I/O, UI responsiveness, concurrency, and repeated lookups.
- Avoid hidden I/O or expensive work in property getters, formatting methods, or control rendering paths.
- Keep asynchronous operations cancellation-aware. Do not use sync-over-async, arbitrary sleeps, or Task.Run to mask blocking design. Use ValueTask only where the actual call pattern benefits from its lower-allocation semantics.
- Dispose owned resources deterministically, including streams, database objects, timers, GDI/images, and WinForms controls.

### WinForms

- UI handlers must remain responsive; database, network, filesystem, and other blocking work must not run synchronously on the UI thread.
- Theme and control updates should avoid unnecessary tree traversals, layout passes, repainting, and object creation.
- Hive-specific controls exist only for a real Hive consumer contract, behavior, or styling need. Native WinForms controls remain preferred where they already satisfy the requirement.

### Persistence

- Use parameterized SQL and explicit transaction boundaries where required.
- Repeated lookup paths require appropriate indexes.
- Avoid N+1 queries and hidden database work from property accessors or UI formatting.
- Keep connection/command/reader lifetimes bounded and disposable.

### Tests

- Test code follows the same production-quality standards as runtime code.
- Tests are deterministic, isolated, concurrency-safe, lightweight, and repeatable.
- No arbitrary sleeps, real vendor accounts, hidden environment variables, or accidental machine state.
- Test doubles remain test-only unless a production contract later requires a reusable fake.
- Tests prove normal, invalid, boundary, cancellation, concurrency, recovery, and security behavior where applicable.
- Core contract test files explicitly import Hive.Core and Xunit alongside any additional required namespaces.

## 14. Testing & Production Readiness

Every implementation slice has a completion gate.

Coverage must include, as applicable:

- deterministic unit tests;
- invalid and boundary cases;
- persistence/MAF/HTTP/WinForms contract tests;
- concurrency and cancellation;
- stale-state and lifecycle races;
- recovery/crash behavior;
- authorization/scope/credential security;
- manual developer verification of UI behavior where applicable; no separate UI-automation framework is required by the architecture.

Network-provider tests use fakes/local infrastructure and never real vendor accounts.

No verification claim is valid unless the test or manual verification was actually performed.

"All edge cases" means all known and contract-relevant cases derived from the contract and implementation boundary; it does not claim to exhaust every conceivable future failure.

---

## 15. Core Solution Layout

```
Hive.Core
Hive.Agents
Hive.Persistence
Hive.Coordination
Hive.Tools
Hive.Providers.OpenAICompatible
Hive.Management
Hive.Host.WinForms
Hive.Host.WinForms.UI
Hive.Example.WinForms
Hive.Tests
```

Reference direction:

```
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Management
   ↑
Host.WinForms
   ↑
Host.WinForms.UI

Example.WinForms → Host.WinForms + Host.WinForms.UI + public platform contracts
```

Coordination may depend on Core + Agents + MAF.

Host.WinForms never bypasses Hive.Management.

`Hive.Example.WinForms` is created during Phase 0 and remains a first-class developer-facing project; its test tools are not a substitute for `Hive.Tests`.

---

## 16. Non-Negotiable Architectural Rules

1. Hive is general-purpose, but V1 build order is determined by the real data-entry forcing function.
2. Use MAF where MAF already owns the required mechanism.
3. Never build a second orchestration engine to replace MAF.
4. `Agent` and `Hive` are complete useful base types.
5. `CognitiveAgent` and `CognitiveHive` are optional additive descendants.
6. Concrete type is selected at creation; there is no runtime promotion/demotion.
7. Different generations may coexist without requiring ancestor changes.
8. Host business/domain state remains host-owned.
9. Hive's database is isolated from host business data.
10. Capability state is Supported / Unsupported / Unknown.
11. Capability requirements are Required / Preferred / Optional / Forbidden.
12. Quota, rate, health, capacity, cost, and capability remain separate concerns.
13. Secrets are encrypted at rest and redacted from diagnostics.
14. The LLM is a reasoning/request component, never an authorization authority.
15. Authorization is enforced in code.
16. Running executions use immutable effective configuration snapshots.
17. Terminal execution state is protected from late results.
18. Private runtime state is isolated by explicit ownership.
19. Generic cross-host integration is built only when a second real host requires it; V1 WinForms host discovery is part of the initial concrete integration boundary.
20. Host discovery never grants tool permission.
21. Approval is one intervention action; V1 only needs Approve/Reject at the business-app write.
22. State-changing persistence is append-oriented; snapshots are recovery aids.
23. Outbox work is transactional with its triggering durable state change.
24. No empty catch blocks.
25. Use one JSON serialization stack.
26. Do not duplicate the same computation in multiple layers.
27. Prefer structured error classification over string matching.
28. Timeout and budget limits are explicit and validated.
29. Configuration must actually drive the behavior it configures and have tests.
30. Repeated lookup paths use real indexes.
31. Network-provider tests never use real vendor accounts.
32. Every implementation slice has the required unit/edge/integration/recovery/security coverage for its boundary, plus manual developer verification of user-facing UI behavior where applicable.
33. Do not claim verification that was not actually performed.
34. Update architecture before structural code changes.
35. Complete the active slice before starting future slices.
36. Do not implement future cognitive generations as hidden prerequisites of the base Agent/Hive.
37. Do not silently broaden the V1 host integration boundary.
38. Do not make document/image extraction the permanent definition of Hive.
39. Do not make persistent cognition a prerequisite for the V1 Agent.
40. Do not force every Agent into a Hive.
41. Do not force every Hive member to use the same Agent generation.
42. Descendant-owned state must not alter the semantics of ancestor-owned state.
43. New generations must remain replaceable/coexistable through stable base contracts.
44. The example host uses the same public APIs and enforcement boundaries as real hosts.
45. Status lives only in `Hive_Current_Status.md`.
46. Current work lives only in `Hive_Active_Work.md`.
47. `roadmap.md` is the ordered implementation plan and must match this architecture's phase order.
48. Phase and slice numbers are ordinal, not version numbers.
49. Do not create documentation that contradicts these source-of-truth boundaries.
50. When adding or removing rules in `AGENTS.md`, renumber the whole list and verify that there are no duplicates or gaps.
51. An Agent's persistent cognitive identity/state may outlive every individual runtime incarnation.
52. Death ends the current Agent runtime/incarnation; it does not delete the Agent or its persistent cognitive state.
53. Dream processing may operate while no Agent runtime is active, and may continue across host-application shutdown/restart through durable state.
54. Dream outputs are simulated/predicted/hypothetical evidence and must never be recorded as actual experience.
55. Human edits made while an Agent is inactive are part of versioned persistent state and must be incorporated by the next valid wake/reincarnation path.
56. Questions are first-class, provenance-bearing, specialization-aware cognitive objects; semantically duplicate questions should be avoided when existing evidence is sufficient.
57. CognitiveAgents remain complete autonomous cognitive entities; CognitiveHive adds collective cognition without owning or replacing member cognition.
58. New lifecycle, Dream, Question, or collective-cognition behavior must remain additive to the generation that owns it and must not become an implicit prerequisite of the base Agent/Hive.

---

## 17. Roadmap Summary

- **Phase 0 — Foundations**
- **Phase 1 — Base Agent, Provider Platform, Management UI, and Data-Entry Pipeline (V1)**
- **Phase 2 — Base Hive Membership & Coordination**
- **Phase 3 — Hive Governance Patterns**
- **Phase 4 — CognitiveAgent : Agent**
- **Phase 5 — Cognitive Resources**
- **Phase 6 — CognitiveHive : Hive**
- **Phase 7 — Additional Generic Host Integration**
- **Phase 8 — Multi-Tenancy, Scale, Configuration Portability & Extensibility**
- **Phase 9 — Observability, Operations & Replay**

---

## 18. Deferred Decisions

1. Which authentication provider should Phase 8 support when real multi-user requirements arrive (for example local accounts, Microsoft/Entra, Google, or a company IdP)?
2. Whether a future automated UI-testing tool is warranted after real UI test-maintenance needs appear. This is not required for current development because the developer performs manual testing.

The V1 integration mode is not a deferred decision: Hive explicitly supports both API/service and bounded WinForms UI integration. The first V1 input type is not a deferred decision: it is an image.