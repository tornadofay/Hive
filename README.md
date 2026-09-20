# Hive

**Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems.**

Hive is built on the Microsoft Agent Framework (MAF) where MAF already owns the underlying behavior. Hive adds the platform-level semantics and infrastructure around that foundation:

- persistent cognitive runtimes;
- first-class Skills, Knowledge, Wiki, Memory, and Learning Candidates;
- capability assignments and runtime overrides;
- provider/account/model execution control;
- capability-aware execution planning;
- host identity propagation;
- generic host integration;
- WinForms UI Context / Control Adapters;
- general human intervention;
- durable state, snapshots, and transactional outbox;
- resource ownership and generic resource inventory;
- configuration portability;
- authorization and governance;
- WinForms management;
- diagnostics and production-focused testing;
- multi-agent Hives and later governance patterns.

A document/image extraction workflow is only one example workload. It is not the definition of Hive.

## Architecture

```text
Host Application
      │
      ├── API / service integration
      └── WinForms UI Context / Control Adapters
      │
      ▼
+------------------------------------------------------+
|                       Hive                           |
|                                                      |
|  Identity / Tenancy / Resource Ownership             |
|  Agent Definitions / Runtime Instances               |
|  Persistent Cognitive Runtime                       |
|  Memory / Knowledge / Wiki / Skills / Learning      |
|  Provider Control Plane / Execution Planning         |
|  General Human Intervention                          |
|  Tools / Authorization / Governance                  |
|  Management / Diagnostics / Portability              |
|  Events / Snapshots / Transactional Outbox           |
|  Hive Membership / Coordination                      |
+------------------------------------------------------+
      │
      ▼
Microsoft Agent Framework
      │
      ├── Agent execution
      ├── Workflow orchestration
      ├── Checkpoints / resume
      ├── Human-in-the-loop primitives
      └── Workflow execution infrastructure
      │
      ▼
Provider / Model
```

Hive does not become an orchestration engine where MAF already provides the required mechanism.

## Core principles

- **C# / .NET 10 only**
- **MAF-first:** use MAF for behavior it already owns
- **Provider-neutral Core**
- **Persistent cognition is a first-class capability**
- **Resources are first-class:** Skills, Knowledge, Wiki, Memory, Learning Candidates, assignments, overrides, and future resource types
- **Execution planning is capability-aware and explainable**
- **Identity is explicit and propagated**
- **Human intervention is broader than approval**
- **Host integration can work with APIs or desktop UI**
- **WinForms UI Context / Control Adapters are bounded, non-authoritative context mechanisms**
- **Hive state is separate from the host application's business database**
- **Running executions use immutable configuration snapshots**
- **Terminal outcomes cannot be overwritten by late provider responses**
- **Secrets are encrypted at rest and redacted from diagnostics**
- **The LLM proposes/reasons; Hive enforcement boundaries remain authoritative**
- **Unknown provider capability is never silently treated as supported**
- **Production tests cover concurrency, failure, recovery, security, and edge cases**

## Host integration

Hive is designed to integrate with real applications, including desktop software.

The host integration boundary can consume:

- Forms;
- UserControls;
- bindings;
- BindingSource;
- native collections;
- DataTable;
- bounded application objects;
- explicit Control Adapters;
- API/service contracts.

Discovery is bounded and read-oriented. Discovering an object never grants permission to execute against it.

This lets workloads such as document/image extraction, business-app automation, developer tooling, simulations, and other agent-driven systems use the same underlying Hive infrastructure.

## Management surface

The WinForms management application is organized around authoritative Hive state:

1. Providers / Models / Execution Targets
2. Agents
3. Cognition
4. Learning Review
5. Knowledge / Wiki
6. Skills
7. Storage
8. Runtime Diagnostics
9. Human Intervention
10. Resource Inventory
11. Configuration Import / Export
12. Host Integration / UI Context diagnostics

The UI is a thin shell over `Hive.Management`.

## Example application

`Hive.Example.WinForms` is a separate example/verification host.

Every example is isolated from reusable implementation code and includes a complete public-API code snippet that can be copied and run as documentation.

Examples cover the platform itself: execution, planning, cognition, memory, knowledge, skills, learning, intervention, portability, host integration, diagnostics, and one document/image workload.

## Automated tests

`Hive.Tests` is intended to provide production-oriented unit and contract coverage.

Important test categories include:

- identity and scope isolation;
- capability matching;
- execution planning;
- immutable snapshots;
- cognitive state/recovery;
- memory ownership;
- learning promotion/rejection;
- intervention races and stale requests;
- terminal-state protection;
- provider failures and timeouts;
- transactional outbox;
- crash/resume;
- configuration portability;
- secret redaction;
- host object discovery limits;
- tool authorization;
- concurrent runtime isolation.

No test coverage claim is made until the corresponding automated test actually exists.

## Repository structure

```text
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

The repository is currently documentation/scaffolding only. Implementation status and test claims are recorded only in `docs/Hive_Current_Status.md`.

See [Architecture](docs/architecture.md) and [Roadmap](docs/roadmap.md).

## Official MAF reference

Hive's MAF boundary should be checked against Microsoft's current Agent Framework documentation as MAF evolves:

https://learn.microsoft.com/en-us/agent-framework/workflows/

## License

License not selected yet.
