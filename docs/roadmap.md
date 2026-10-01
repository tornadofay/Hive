# Hive — Roadmap

This file is the **ordered implementation index** for Hive. Detailed phase and slice planning lives under [`docs/plan/`](plan/).

`docs/architecture.md` is the architectural source of truth. `docs/Hive_Active_Work.md` is the implementation authorization boundary. `docs/Hive_Current_Status.md` records current status. This roadmap defines order, not authorization.

## Slice completion gate

Every implementation slice is incomplete until the required verification has actually been performed.

Depending on the slice boundary, required coverage includes:

1. unit tests for normal, invalid, and boundary cases;
2. contract/integration tests for persistence, MAF, HTTP/provider, configuration, or WinForms boundaries;
3. concurrency/cancellation/recovery/stale-state tests for mutable runtime and durable state;
4. security tests for authorization, scope, secrets, and unsafe inputs;
5. manual developer verification for user-facing UI behavior where applicable;
6. public-API example verification for externally meaningful capabilities.

No build, test, or verification claim may be recorded unless it was actually run.

A decision gate may have documentation/architecture acceptance instead of automated tests when it intentionally produces a design decision rather than runtime behavior.

## Phase 0 — Foundations

[Detailed phase plan](plan/Phase0_Foundations.md)

- 0.1 — Solution & project scaffolding
- 0.2 — Common infrastructure
- 0.3 — Identity, WorkItem & Resource foundation
- 0.4 — Persistence bootstrap
- 0.5 — Test harness
- 0.6 — WinForms UI/UX Foundation
- 0.7 — First-Class Example Host Shell
## Phase 1 — Base Agent, Provider Platform, Management UI, and Data-Entry Pipeline (V1)

[Detailed phase plan](plan/Phase1_BaseAgent_V1.md)

- 1.1 — Provider / ProviderAccount / ExecutionTarget
- 1.2 — Secret Store
- 1.3 — OpenAI-compatible Provider Adapter
- 1.4 — Capability-aware Execution Target Selection
- 1.5 — Base Agent & AgentFactory
- 1.6 — Base Agent Work Protocols
- 1.7 — Event Log, Snapshots & Transactional Outbox
- 1.8 — Outbox Poller
- 1.9 — First Real Agent Execution
- 1.10 — Hive.Management Facade
- 1.11 — Initial Workspace & WorkItem Operations
- 1.12 — Global Hive Settings & Host Configuration
- 1.13 — Image Input & WinForms Host Context
- 1.14 — Dual Business-App Integration Contract
- 1.15 — Input Preparation & Routing
- 1.16 — Provider / Model Capability Discovery & Operational Metadata
- 1.16 UI — Provider Configuration, Discovery & Target Reconciliation
- 1.16 Follow-Up — Complete Provider Model Metadata Discovery
- 1.17 — Structured Extraction & Validation
- 1.18 — Durable Base-Agent Work State
- 1.19 — V1 Vector Retrieval Infrastructure
- 1.20 — V1 Workspace Foundation
- 1.21 — V1 Agent Interaction & Application/Form Agents
- 1.22 — Governed Tools, Policy, Permissions & Human Intervention
- 1.23 — Business-App Proposal & Governed Write
- 1.24 — Business Operation Receipt & Reconciliation
- 1.25 — Post-Write Review
- 1.26 — MAF Sequential V1 Pipeline
- 1.27 — Multi-Agent Work Assignment & Concurrent Execution
- 1.28 — Full-Pipeline Crash/Resume
- 1.29 — Resource Inventory & Runtime Diagnostics
- 1.30 — Metrics, Budget Cap & OpenTelemetry
## Phase 2 — Base Hive Membership & Coordination

[Detailed phase plan](plan/Phase2_BaseHive_Membership_Coordination.md)

- 2.1 — HiveDefinition & Membership
- 2.2 — Configurable Agent Composition
- 2.3 — Hive ↔ Agent Communication
- 2.4 — Shared Claims with Provenance
- 2.5 — Supervisor Controls
- 2.6 — Agent-Owned Hive Creation & Hive Lifecycle
- 2.7 — Specialty-Driven Population & Swarm Participation
- 2.8 — Hive Coordination Workspace & Swarm Extensions
## Phase 3 — Hive Governance Patterns

[Detailed phase plan](plan/Phase3_Hive_Governance.md)

- 3.1 — Manager-led Strategy
- 3.2 — Democratic/Voting Strategy
- 3.3 — Adversarial/Critique Strategy
- 3.4 — Governance Strategy Selection UI
## Phase 4 — CognitiveAgent : Agent

[Detailed phase plan](plan/Phase4_CognitiveAgent.md)

- 4.1 — Cognitive Kernel
- 4.2 — Cognitive Strategy
- 4.3 — Reasoning Requirement
- 4.4 — Persistent Cognitive State
- 4.5 — Experience, Outcome Evaluation & Cognitive Event History
- 4.6 — Death / Wake / Reincarnation Lifecycle
- 4.7 — Postmortem & Dream Processing
- 4.8 — Questions
- 4.9 — Cognitive State Reconciliation
## Phase 5 — Cognitive Resources

[Detailed phase plan](plan/Phase5_CognitiveResources.md)

- 5.1 — Memory Resource Families
- 5.2 — Knowledge / Wiki
- 5.3 — Skills
- 5.4 — Learning Candidates & Governance
## Phase 6 — CognitiveHive : Hive

[Detailed phase plan](plan/Phase6_CognitiveHive.md)

- 6.1 — Collective Cognitive State
- 6.2 — Collective Strategy
- 6.3 — Collective Questions & Specialty Routing
- 6.4 — Cross-Agent Evidence & Synthesis
- 6.5 — Collective Conflict & Consensus
## Phase 7 — Additional Generic Host Integration

[Detailed phase plan](plan/Phase7_GenericHostIntegration.md)

- 7.1 — Generic Host Context
- 7.2 — Cross-Host Data-Source & Control Adapters
- 7.3 — Cross-Host Bounded Object Discovery
## Phase 8 — Multi-Tenancy, Scale, Configuration Portability & Extensibility

[Detailed phase plan](plan/Phase8_Scale_Portability_Extensibility.md)

- 8.1 — Authentication Boundary
- 8.2 — Distributed Execution Decision Point
- 8.3 — Configuration Import/Export
- 8.4 — MCP / Tool Extensibility
- 8.5 — Additional Host Surfaces
- 8.6 — Lightweight / Embedded Hive Deployment Profile
## Phase 9 — Observability, Operations & Replay

[Detailed phase plan](plan/Phase9_Operations_Observability_Replay.md)

- 9.1 — Full Metrics Taxonomy
- 9.2 — Dashboards & Operational Views
- 9.3 — CI/CD
- 9.4 — Event-Log Replay Regression
- 9.5 — Long-Running Resilience
- 9.6 — Production Diagnostics & Support Tooling

## Ordering invariant

The order is intentional. Base Agent contracts reserve reusable mechanisms such as Objectives, Question transport, patience/understanding gates, memory infrastructure, simulation interfaces, delegation, and Hive sponsorship. Implementation is pulled into the earliest phase only when the current V1 boundary requires it. The cognitive lifecycle, Dreams, and adaptive Questions remain CognitiveAgent-generation capabilities; CognitiveHive later extends them with cross-agent coordination without moving individual cognition into the Hive.

```
Foundations
   ↓
Base Agent + V1 data-entry pipeline
   ↓
Base Hive coordination
   ↓
Hive governance
   ↓
CognitiveAgent
   ↓
Cognitive resources
   ↓
CognitiveHive
   ↓
Generic future host integration
   ↓
Scale / portability / extensibility
   ↓
Operations / replay
```

The existence of a later architectural concept never makes it an implicit prerequisite for an earlier phase.

The CognitiveAgent outcome/learning branch is intentionally contained within Phases 4–5:

```
actual experience
    ↓
outcome evaluation
    ↓
Mistake / Success / Partial / Unknown
    ↓
Risk/Fear/Confidence + attribution
    ↓
Question / Dream / Hive assistance
    ↓
Learning Candidate
    ↓
validation / reconciliation
    ↓
future strategy
```

This branch does not alter Base Agent execution semantics or make CognitiveAgent state a prerequisite for V1.
