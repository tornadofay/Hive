# Hive

**Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems — proven first against a real deliverable: automating data entry from supported input sources into an existing business application.**

The data-entry pipeline is Hive's **V1 forcing function**, not Hive's permanent definition. It determines what gets built first so the general platform is proven against a real problem instead of an abstract feature list. Input preparation and interpretation (including vision when appropriate), structured candidate extraction, validation, business-app integration, and approval are V1 capabilities that emerge from the general Agent/Tool/platform architecture.

## Project status

- **Phase 0 — Foundations:** complete.
- **Phase 1 — Base Agent, Provider Platform, Management UI, and Data-Entry Pipeline (V1):** in progress. Verified implementation is complete through Phase 1.18A (Embedded Persistence Profile) and Phase 1.19A (Execution Target Preferences). Phase 1.20 Slice 1 (Workspace Foundation) closed on 2026-10-23 with developer-reported verification of 818/818 tests passing; Slices 2-4 are not started and require explicit authorization. Vector retrieval is deferred to reassessment with Phase 5.1.

The present-day state and the active verification gate are owned by [Current Status](docs/Hive_Current_Status.md) and [Active Work](docs/Hive_Active_Work.md). Completed-work evidence lives in the [verification archive](docs/verification/).

## Standalone preview (Milestone A) [Target]

Hive's first public deliverable is a **standalone Hive Workbench** — a self-contained WinForms application that demonstrates Hive's autonomous multi-agent value without requiring a host business application. It reuses the existing Workspace foundation and the neutral Management contracts; it does not fork the Workspace or build a separate coordination engine.

**In scope:** onboarding (provider key via Windows DPAPI, or a local OpenAI-compatible endpoint; Embedded/SQLite persistence; project files; reproducible samples; quickstart; packaging); Workspace UX redesign; Agent definition extension (specialty profile, responsibilities, exclusions, expected output, tool capabilities, success criteria); governed Tools + authorization boundary; autonomous specialist creation with population guards; budget/cost controls; narrow manager-led coordination and one critic role.

**Deferred:** build/test execution (Preview 2, behind isolation — a project-directory restriction alone is not a sandbox); screen observation / UI automation (post-V1); the full advanced guard panel (Preview 2); the host-integrated business write path (1.23–1.28, sequenced after the preview, not dropped).

**Autonomy profiles:** Guided / Balanced (default) / High Autonomy (renamed from "Unrestricted"; hard floors and authorization remain intact), plus an Advanced view ("Show advanced") exposing the configurable keys with their allowed ranges. The full guard-authoring panel (custom policy authoring, per-specialist overrides, simulation/what-if, guard auditing UI) is deferred to Preview 2. Guard decisions and configuration changes are recorded as attributable evidence.

Details: [roadmap](docs/roadmap.md) (Milestone A section), [agents-and-hives](docs/architecture/agents-and-hives.md), [v1-host-and-management](docs/architecture/v1-host-and-management.md).

---
## Getting started

**Requirements**

- Windows (the host is WinForms and secrets use Windows DPAPI).
- The .NET 10 SDK.
- Nothing else for a normal installation: the **Embedded** persistence profile (SQLite) needs no database server.
- SQL Server or LocalDB only if you choose the SQL Server persistence profile, or to run the SQL Server persistence integration tests (see `tests/Hive.Tests/HivePersistenceTestConfiguration.cs` and the test-harness section of [docs/architecture/foundations.md](docs/architecture/foundations.md)).

**Build, run, test**

```text
dotnet build Hive.sln
dotnet run --project src/Hive.Example.WinForms
dotnet test tests/Hive.Tests
```

`Hive.Example.WinForms` is the developer-facing Example Host. Warnings are treated as errors repository-wide (`Directory.Build.props`). Verification is currently developer-run; see [AGENTS.md](AGENTS.md) for the workflow and gates.

Secrets are protected at rest with Windows DPAPI at current-user scope, so V1 treats a Hive database as single-Windows-principal for secret purposes (details in the foundations architecture document).

## Architecture at a glance

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
      ▼
Provider / Model
```

Hive uses Microsoft Agent Framework (MAF) wherever MAF already owns the underlying mechanism and does not become a second orchestration engine. The architecture index is [docs/architecture.md](docs/architecture.md); detailed domain documents are under [docs/architecture/](docs/architecture/).

## Agents, Hives, and later generations

- The base `Agent` and base `Hive` are complete and useful on their own. A base Agent can be capable without being cognitive: it has Objectives, durable work state, a Question/Answer protocol, an Understanding Gate, delegation interfaces, and lifecycle controls.
- `CognitiveAgent` and `CognitiveHive` are later, additive generations. The concrete type is chosen explicitly when a resource is created; there is no runtime promotion or demotion.
- A Hive is a persistent collective with membership. A Swarm is only the subset of members currently collaborating on a bounded problem; it is not a persistent layer.
- A future CognitiveAgent adds persistent cognitive state that outlives any one runtime, offline Dream processing, specialty-aware Questions, and governed learning from evaluated outcomes. Dream results are simulations and are never treated as actual experience.

Details: [agents-and-hives](docs/architecture/agents-and-hives.md) and [cognitive resources](docs/architecture/cognitive-resources-and-portability.md).

## V1 data-entry pipeline

```
Input submission
      ↓
input-specific preparation / routing
      ↓
structured candidate
      ↓
validation
      ↓
business-operation proposal
      ↓
authorization / approval when required
      ↓
governed business-app write
      ↓
durable operation attempt / receipt
      ↓
policy-governed verification / Review
```

Image is the first V1 input boundary. Supported inputs are prepared and routed by type: images through a vision-capable ExecutionTarget, `.xlsx` workbooks through worksheet and row mapping, and `.txt` text files. All of them converge on one structured-candidate boundary.

A single submission may produce one or many independent WorkItems. A WorkItem is the durable unit of user-visible work and represents one logical business operation when one is required. Batch grouping never replaces a WorkItem's identity, lifecycle, provenance, authorization, receipt, or Review state.

The business write is a governed Tool invocation over an authorized host capability. Approval (before the write) and Review (after the write) are separate concepts. Hive's database is never a gateway to the host application's business database. The proposal, write, receipt, and Review stages belong to later V1 phases; see the [roadmap](docs/roadmap.md).

## Workspace and host integration

The **Workspace** is the human-facing operational surface over `Hive.Management`. It is implemented in `Hive.Host.WinForms`; there is no separate `Hive.Workspace` project.

Currently implemented: the WorkItem surface (image input, listing and paging, activity, approval actions) and the direct LLM conversation view with explicit ExecutionTarget selection (Phase 1.20 Slice 1, awaiting verification). Planned: Agent interaction and application/form-associated Agents (Phase 1.21), governed Tools and generalized intervention (Phase 1.22), and later business-operation execution, receipts, post-write Review, multi-Agent execution, recovery, diagnostics, and observability.

Business-app integration supports both API/service and bounded UI integration, and a WorkItem or operation may use either or both. A WinForms host registers a root and captures bounded, immutable metadata:

```csharp
using var registration = hostContext.Register(form);
var snapshot = await hostContext.CaptureAsync(registration, cancellationToken);
```

Discovery provides context only; it never grants permission to click, edit, invoke, or otherwise mutate a control. Registration does not create an Agent or Hive. Details: [v1-business-app-integration](docs/architecture/v1-business-app-integration.md) and [v1-host-and-management](docs/architecture/v1-host-and-management.md).

## Management, UI foundation, examples, and tests

- **Management:** `Hive.Management` is the authoritative facade; WinForms is a thin shell over it. Settings covers Providers (Add Provider, Refresh, Advanced Provider Configuration), Agents, and Persistence (Database Setup and Data Migration between SQL Server and Embedded).
- **UI foundation:** Hive-owned controls, native WinForms, and custom `System.Drawing` rendering behind `Hive.Host.WinForms.UI`, with Light / Dark / System themes. See [docs/ui/](docs/ui/).
- **Example Host:** `Hive.Example.WinForms` is a first-class developer-facing application. Examples are auto-discovered through the `IHiveExample` contract and organized as Category → Subcategory → Example.
- **Tests:** `Hive.Tests` is the authoritative automated suite. No coverage or verification claim is made until the test or manual check has actually been performed.

## Core principles

- C# / .NET 10 only; MAF-first.
- General-purpose architecture with a concrete V1 forcing function.
- Base Agent and Hive are complete on their own; cognitive generations are additive and chosen at creation.
- The LLM proposes and reasons; Hive enforcement boundaries decide. Authorization is enforced in code, never by prompts or UI visibility.
- Hive state is separate from the host application's business database.
- Provider-neutral, capability-aware execution planning; one shared OpenAI-compatible provider transport.
- Running executions use immutable configuration snapshots; late provider results cannot overwrite terminal state.
- Secrets are protected at rest and redacted from diagnostics.
- Production verification covers concurrency, failure, recovery, security, and edge cases.

## Documentation map

- [AGENTS.md](AGENTS.md) — repository operating rules, authorization, and verification gates.
- [docs/architecture.md](docs/architecture.md) and [docs/architecture/](docs/architecture/) — architecture index and detailed domain documents.
- [docs/roadmap.md](docs/roadmap.md) and [docs/plan/](docs/plan/) — ordered work and detailed plans.
- [docs/Hive_Current_Status.md](docs/Hive_Current_Status.md) and [docs/Hive_Active_Work.md](docs/Hive_Active_Work.md) — current status and the active slice.
- [docs/verification/](docs/verification/) — historical verification evidence.
- [docs/ui/](docs/ui/) and [docs/examples/](docs/examples/) — UI and public-API usage guidance.
- [.agents/skills/](.agents/skills/) — reusable agent workflow.

## Repository structure

```
Hive/
├── AGENTS.md
├── Directory.Build.props
├── Hive.sln
├── README.md
├── .agents/skills/
├── docs/
│   ├── architecture.md
│   ├── architecture/
│   ├── roadmap.md
│   ├── plan/
│   ├── Hive_Current_Status.md
│   ├── Hive_Active_Work.md
│   ├── verification/
│   ├── ui/
│   ├── examples/
│   └── off-work/            (preserved notes, non-authoritative)
├── src/
│   ├── Hive.Core/
│   ├── Hive.Agents/
│   ├── Hive.Persistence/
│   ├── Hive.Coordination/
│   ├── Hive.Tools/
│   ├── Hive.Providers.OpenAICompatible/
│   ├── Hive.Management/
│   ├── Hive.Host.WinForms/
│   ├── Hive.Host.WinForms.UI/
│   └── Hive.Example.WinForms/
└── tests/
    └── Hive.Tests/
```

The solution and project files are the implementation source for the actual project graph.

## MAF reference

https://learn.microsoft.com/en-us/agent-framework/workflows/

## License

Hive is released under the [MIT License](LICENSE), allowing free use, modification, distribution, and commercial use subject to the license terms.

Copyright (c) 2026 Hani.

## Author

**Hani**

- Email: [tornadofay@gmail.com](mailto:tornadofay@gmail.com)
- LinkedIn: [linkedin.com/in/ehani](https://www.linkedin.com/in/ehani/)
