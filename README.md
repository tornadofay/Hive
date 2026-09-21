# Hive

**Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems — proven first against a real deliverable: automating data entry from documents and images into an existing business application.**

The data-entry pipeline is Hive's **V1 forcing function**, not Hive's permanent definition. It determines what gets built first so the general platform is proven against a real problem instead of an abstract feature list.

Vision, document parsing, structured extraction, validation, business-app integration, and approval are V1 capabilities that emerge from the general Agent/Tool/platform architecture.

## Architecture

```
Host Application
      │
      ├── API/service integration
      └── bounded UI integration
           (both may be used for the same WorkItem/operation)
      │
      ▼
+------------------------------------------------------+
|                       Hive                           |
|                                                      |
| Agent (base) ──────────────► Hive (base)            |
|      │                            │                 |
|      ▼                            ▼                 |
| CognitiveAgent               CognitiveHive          |
| complete individual mind      collective cognition  |
| (later, additive)            (later, additive)     |
|                                                      |
| Provider Control Plane / Execution Planning          |
| Management / Authorization / Intervention            |
| Events / Snapshots / Transactional Outbox            |
| Hive Membership / Governance                         |
+------------------------------------------------------+
      │
      ▼
Microsoft Agent Framework
      │
      ├── Agent/workflow execution
      ├── Orchestration
      ├── Checkpoints/resume where applicable
      ├── HITL primitives
      └── Workflow execution infrastructure
      │
      ▼
Provider / Model
```

Hive uses MAF wherever MAF already owns the underlying mechanism. Hive does not become a second orchestration engine.

## Stable Agent generations

```
Agent                       Hive
  │                           │
  └── CognitiveAgent          └── CognitiveHive
       later/additive              later/additive
```

The concrete type is selected when the resource/runtime is created.

A normal Agent does not later turn into a CognitiveAgent, and a CognitiveAgent does not later turn into a normal Agent. Different generations can coexist in the same deployment.

This keeps the base platform stable while allowing future agent/hive generations to introduce new implementation ideas without breaking existing projects.

## Base Agent capability boundary

The base `Agent` is allowed to be capable without being cognitive. It may provide reusable mechanisms such as:

- explicit **Objectives** with lifecycle, completion criteria, priority, deadlines, and dependencies;
- durable **memory infrastructure** for explicitly addressed conversation, execution history, artifacts, tool results, checkpoints, and configuration state;
- a first-class **Question/Answer protocol** with provenance, ownership, status, waiting, and timeout;
- a **Patience / Understanding Gate** that blocks consequential work until required information or confirmation is available;
- bounded **Simulation infrastructure** that can execute and compare hypothetical scenarios in parallel;
- **Delegation/coordination** interfaces for requesting work from other Agents or Hives;
- lifecycle and persistence controls that allow runtime death, dormancy, and later recreation.

These mechanisms become cognitive only when a CognitiveAgent can autonomously interpret, revise, select, or learn from them—for example forming Goals, revising Beliefs, choosing Questions, selecting Dreams, interpreting simulation outcomes, or changing strategy from experience.

## Dynamic Hives and Swarms

A base Agent may sponsor a persistent Hive when a problem requires multiple specialties. The Agent remains an Agent; the Hive becomes a separate managed collective.

A Hive manages membership and may create or reuse specialist Agents of authorized generations. A **Swarm is not another persistent layer**: it is simply the selected Hive members currently collaborating on a bounded problem. When the collaboration ends, the Swarm ceases to be active; the Hive and its Agents remain.

A member Agent inside a Hive normally requests missing specialties from the parent Hive rather than creating child Hives itself. A solo Agent may sponsor a persistent Hive when it needs multiple specialties. Hive sponsorship does not own the Hive's lifetime.

## Workspace

`Hive.Workspace` is the human-facing operational surface over `Hive.Management`.

For **V1**, the Workspace is intentionally limited to the operational path needed to process submitted images:

- submit/attach an image to a WorkItem;
- view WorkItem status and activity;
- view relevant execution/provider status;
- receive WorkItem notifications;
- view PendingApproval;
- Approve / Reject the governed business-app write.

This V1 surface works with a single Agent and does not require a Hive.

Later Workspace extensions are added when their owning platform capabilities exist:

- general **LLM mode** with explicit model/execution-target selection;
- **Agentic mode** with Agent/Hive-selected execution targets;
- Agent and Hive organization/topology;
- active Swarm membership;
- Questions, cognitive state, and other later-generation views.

A business application can register a host context with a bounded API such as:

```csharp
ai.Register(this);
```

Registration may bind or reuse a specialized Agent. It does not automatically create an Agent or Hive, and multiple open forms do not automatically become a Hive merely because they are visible at the same time.

For V1, business-app integration supports **both API/service and bounded UI integration**. They are not mutually exclusive: a WorkItem or individual operation may use the API, the UI, or both. The V1 WinForms UI path can discover the application's Form hierarchy, UserControls, `Control`-derived and custom controls, containers such as Panels and GroupBoxes, nested controls, and relevant runtime/data-source context. Discovery provides context only; it never grants permission to click, edit, invoke, or otherwise mutate a control.

## V1 work-unit semantics

One submitted document is one WorkItem. A batch is multiple WorkItems. WorkItem identity and lifecycle are independent of individual runtime/execution lifetimes.

## Cognitive lifecycle (later generation)

A CognitiveAgent is a persistent cognitive entity whose runtime incarnation may end completely while its identity and cognitive state remain durable:

```
Persistent Agent state
        ↓
   runtime incarnation
        ↓
      operate
        ↓
       Death
        ↓
  no Agent runtime
        │
   ┌────┴────┐
   │         │
 Dreams   human review/edit
   │         │
   └────┬────┘
        ↓
 persistent state
        ↓
   Wake/Reincarnation
        ↓
 new runtime incarnation
```

Dreams are bounded simulations/analyses that can run against persisted state without an active Agent runtime. They may explore multiple hypothetical plans in parallel and compare predicted outcomes. Dream results remain distinguishable from actual experiences.

Questions are explicit cognitive work items. A Hive may route different Questions to Agents according to specialty so the members investigate different aspects of the same user objective; answers remain attributable and can feed collective reasoning.

## V1 data-entry pipeline

```
Image (first V1 input)
      ↓
image preparation / vision
      ↓
structured extraction
      ↓
validation
      ↓
governed business-app write
      ↓
Approve / Reject
```

The business-app write is a governed Tool. Hive never treats its own database as a gateway to the host application's business database.

V1 does not choose between API and UI as an architecture decision. Both integration paths are supported from the start, and the implementation may use either or both per operation according to the real application's capabilities and authorization policy.

The first V1 input is an image. Additional document formats can be added later without redefining Hive.

Generic cross-host integration remains later; the initial UI discovery contract is specifically for the real WinForms host boundary used by V1.

## Core principles

- **C# / .NET 10 only**
- **MAF-first:** reuse MAF for behavior it already owns
- **General-purpose architecture, concrete V1 forcing function**
- **Base Agent and Hive are complete on their own**
- **CognitiveAgent/CognitiveHive are later additive generations**
- **An Agent's persistent cognitive state outlives any one runtime incarnation; runtime death is not Agent deletion**
- **Dreams can process persisted cognition while no Agent runtime is active**
- **Dream results remain simulations/predictions and are never silently treated as actual experience**
- **Questions are first-class, specialty-aware cognitive objects rather than repeated prompt text**
- **CognitiveHive adds collective cognition without replacing or owning member cognition**
- **Swarm is a non-persistent view of the currently collaborating Hive members**
- **Agent generation is explicit at creation; it is never inferred automatically from task complexity**
- **Dream/background processing remains subject to applicable budgets, quotas, concurrency, authorization, and cancellation**
- **No runtime type promotion/demotion**
- **Provider-neutral and capability-aware execution planning**
- **One shared OpenAI-compatible provider transport**
- **Hive state is separate from the host application's business database**
- **Running executions use immutable configuration snapshots**
- **Late provider results cannot overwrite terminal execution state**
- **The LLM proposes/reasons; Hive enforcement boundaries decide**
- **Secrets are encrypted at rest and redacted from diagnostics**
- **Production verification covers concurrency, failure, recovery, security, and edge cases**

## Management surface

`Hive.Management` is the authoritative management facade. WinForms is a thin shell over that facade.

V1 starts with:

1. Providers / Models / Execution Targets
2. Agents
3. V1 WorkItems / Operational Workspace

Later areas are added when their owning phase lands: Hive Membership, Governance, Cognition, Dreams, Questions, Learning Review, Knowledge/Skills/Memory, Storage, Runtime Diagnostics, Human Intervention, Resource Inventory, Configuration Import/Export, and generic host-integration diagnostics.

## WinForms UI foundation

Hive's WinForms surfaces share a common UI foundation established in Phase 0.

ReaLTaiizor is the selected third-party rendering layer. It is isolated behind `Hive.Host.WinForms.UI`; consuming forms do not reference the third-party library directly. Hive owns the theme contract, semantic design tokens, and Hive-specific controls where additional behavior or styling is required.

The foundation starts with Light / Dark / System modes and shared palette, typography, spacing, and visual-state rules. Standard WinForms controls remain valid when their native behavior is sufficient; Hive does not wrap every control merely to rename it.

## Example application

`Hive.Example.WinForms` is a first-class developer-facing application, not a temporary demo. It uses the same UI foundation as the rest of Hive.

Examples are organized as Category → Subcategory → Example through a left-side navigation surface and a replaceable content `UserControl`. Examples are auto-discovered through a small `IHiveExample` contract so adding an example does not require central shell wiring.

The Example host also provides developer test tools that invoke `dotnet test` externally against `Hive.Tests` and stream results. The authoritative test suite remains `Hive.Tests`.

The developer performs manual UI/application testing; no separate UI-automation framework is required by the architecture.

## Automated tests

`Hive.Tests` provides production-oriented unit and contract coverage.

Important categories include:

- identity/scope isolation;
- capability matching;
- execution planning;
- immutable snapshots;
- persistence and outbox;
- provider failure/timeout behavior;
- MAF integration;
- approval/stale-intervention behavior;
- crash/resume;
- security/redaction;
- concurrent runtime isolation;
- later cognitive state and recovery;
- host integration boundaries.

No test coverage or verification claim is made until the corresponding test or manual verification actually exists and has actually been performed.

## Repository structure

```
Hive/
├── docs/
│   ├── architecture.md
│   ├── roadmap.md
│   ├── Hive_Current_Status.md
│   └── Hive_Active_Work.md
├── AGENTS.md
└── README.md
```

## Project status

**Phase 0 — Foundations.**

The repository is currently documentation/architecture only. Implementation status is recorded only in `docs/Hive_Current_Status.md`.

See [Architecture](docs/architecture.md) and [Roadmap](docs/roadmap.md).

## MAF reference

https://learn.microsoft.com/en-us/agent-framework/workflows/

## License

License not selected yet.
