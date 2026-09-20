# Hive

**Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems — proven first against a real deliverable: automating data entry from documents and images into an existing business application.**

The data-entry pipeline is Hive's **V1 forcing function**, not Hive's permanent definition. It determines what gets built first so the general platform is proven against a real problem instead of an abstract feature list.

Vision, document parsing, structured extraction, validation, business-app integration, and approval are V1 capabilities that emerge from the general Agent/Tool/platform architecture.

## Architecture

```
Host Application
      │
      ├── API integration
      │        or
      └── scoped UI integration when the real host has no usable API
      │
      ▼
+------------------------------------------------------+
|                       Hive                           |
|                                                      |
| Agent (base) ──────────────► Hive (base)            |
|      │                            │                 |
|      ▼                            ▼                 |
| CognitiveAgent               CognitiveHive          |
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

## V1 data-entry pipeline

```
Document / image
      ↓
parse / rasterize
      ↓
text or vision capability
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

The exact integration boundary is decided before the write implementation:

- API/service when the real application exposes a usable API;
- narrowly scoped UI/control integration when it does not.

A generic host-integration framework is deliberately later.

## Core principles

- **C# / .NET 10 only**
- **MAF-first:** reuse MAF for behavior it already owns
- **General-purpose architecture, concrete V1 forcing function**
- **Base Agent and Hive are complete on their own**
- **CognitiveAgent/CognitiveHive are later additive generations**
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

Later areas are added when their owning phase lands: Hive Membership, Governance, Cognition, Learning Review, Knowledge/Skills/Memory, Storage, Runtime Diagnostics, Human Intervention, Resource Inventory, Configuration Import/Export, and generic host-integration diagnostics.

## Example application

`Hive.Example.WinForms` demonstrates the public API and real-host composition.

Examples are developed alongside the feature they demonstrate and are not a substitute for automated tests.

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

No test coverage or verification claim is made until the corresponding test actually exists and has actually been run.

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
