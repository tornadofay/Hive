# Hive — Roadmap

This is the ordered slice-level implementation plan. `docs/architecture.md` is the architectural source of truth; this file defines implementation order. Status belongs in `Hive_Current_Status.md`.

## Slice completion gate

Every implementation slice is incomplete until the required verification has actually been performed.

Depending on the slice boundary, required coverage includes:

1. unit tests for normal, invalid, and boundary cases;
2. contract/integration tests for persistence, MAF, HTTP/provider, configuration, or WinForms boundaries;
3. concurrency/cancellation/recovery/stale-state tests for mutable runtime and durable state;
4. security tests for authorization, scope, secrets, and unsafe inputs;
5. UI smoke/automation for user-facing behavior that cannot be proven elsewhere;
6. public-API example verification for externally meaningful capabilities.

No build, test, or verification claim may be recorded unless it was actually run.

A decision gate may have documentation/architecture acceptance instead of automated tests when it intentionally produces a design decision rather than runtime behavior.

---

# Phase 0 — Foundations

## 0.1 — Solution & project scaffolding
Objective: create `Hive.Core`, `Hive.Agents`, `Hive.Persistence`, `Hive.Coordination`, `Hive.Tools`, `Hive.Providers.OpenAICompatible`, `Hive.Management`, `Hive.Host.WinForms`, and `Hive.Tests`, with correct dependency direction.
Verify: solution builds; forbidden references are absent.

## 0.2 — Common infrastructure
Objective: IDs, immutable value objects, typed errors/results, `IClock`, event envelope, correlation/causation IDs, and one JSON serialization stack.
Verify: normal/invalid/boundary unit tests and JSON round-trip.

## 0.3 — Identity & Resource foundation
Objective: Deployment/Tenant/Principal/User/Session/Workspace/Agent/Runtime/Execution identity, Resource envelope, ownership, scope, provenance, lifecycle/version metadata.
Verify: scope matrix, missing-identity fail-closed cases, immutable identity snapshots.

## 0.4 — Persistence bootstrap
Objective: Hive-owned SQL Server database, LocalDB development setup, DbUp migrations, schema-version tracking, indexes.
Verify: clean install, repeat migration, failed migration, incompatible future schema.

## 0.5 — Test harness
Objective: xUnit scaffolding, fake provider infrastructure, fake clock, test-database strategy, deterministic event-test conventions.
Verify: baseline tests pass and automated tests make no real vendor/network calls.

---

# Phase 1 — Base Agent, Provider Platform, Management UI, and Data-Entry Pipeline (V1)

Nothing in Phases 4–6 is required to complete this phase.

## 1.1 — Provider / ProviderAccount / ExecutionTarget
Objective: concrete provider resources, account boundary, execution targets, persistence, and three-state capabilities.
Verify: CRUD, ownership/scope, duplicate identity, malformed state.

## 1.2 — Secret Store
Objective: DPAPI-backed secret storage, secure replacement/deletion, redaction.
Verify: no plaintext persistence, redacted diagnostics, replacement invalidates prior credential.

## 1.3 — OpenAI-compatible Provider Adapter
Objective: one shared adapter parametrized by base URL and credentials for compatible hosted/local targets.
Verify: local fake-server tests for success, malformed response, timeout, cancellation, authentication failure, rate limit, transport failure, and structured-output failure; one real-provider manual smoke test when available.

## 1.4 — Capability-aware Execution Target Selection
Objective: required-capability filtering and explainable selection diagnostics.
Verify: supported match, unsupported exclusion, unknown exclusion for hard requirements, no-qualifying-target, fixed-target failure.

## 1.5 — Base Agent & AgentFactory
Objective: implement `Agent`, `AgentDefinition`, `RuntimeInstance`, `Execution`, and `AgentFactory.Create<TAgent>()` against base contracts. The selected type is fixed for the created resource/runtime.
Verify: multiple runtimes from one definition remain isolated; the factory does not require cognitive types.

## 1.6 — Event Log, Snapshots & Transactional Outbox
Objective: append-only event log, snapshot fold, and atomic event+snapshot+outbox persistence.
Verify: rollback leaves neither event nor outbox; replay of supported base events is deterministic.

## 1.7 — Outbox Poller
Objective: process committed unhandled outbox rows after transaction commit.
Verify: duplicate delivery safety and crash-before-processing recovery.

## 1.8 — First Real Agent Execution
Objective: connect a base Agent to MAF and the Hive provider boundary for one request, with correlation and durable lifecycle events.
Verify: fake-provider automated path plus manual real-provider smoke test.

## 1.9 — Hive.Management Facade
Objective: CRUD facade for Providers, ProviderAccounts, ExecutionTargets, and AgentDefinitions.
Verify: service-level validation, authorization/scope cases, persistence integration.

## 1.10 — HiveSettingsForm & Providers Page
Objective: thin WinForms shell, shared configuration context, provider/account setup and connection test.
Verify: UI smoke path; management logic remains outside the form.

## 1.11 — Document Parsing
Objective: text extraction from Word, Excel, and text-native PDFs for the first chosen V1 document types.
Verify: checked-in sample fixtures, malformed/corrupt input, bounded extraction.

## 1.12 — Business-App Integration Boundary Decision
Type: architecture decision gate.
Objective: determine API/service integration versus UI-level integration for the real business application and define only the required V1 contract.
Verify: documented decision, boundary contract, authorization model, test strategy, and exact host surface.

## 1.13 — Vision Routing
Objective: rasterize/prepare non-text-extractable pages and route them to a Vision-capable execution target.
Verify: fixed scanned/image sample, unsupported-capability failure, bounded page/image handling.

## 1.14 — Structured Extraction & Validation
Objective: structured-output extraction to typed candidate data with required-field/type/domain validation.
Verify: valid sample, missing fields, invalid types, malformed model output, rejection path.

## 1.15 — Business-App Write Tool
Objective: propose a write, hold `PendingApproval`, and perform the write only after explicit approval.
Verify: pending blocks execution; rejection prevents side effect; approval reaches a fake client; duplicate approval cannot duplicate the write.

## 1.16 — MAF Sequential V1 Pipeline
Objective: wire ingest → extract → validate → write as one MAF Sequential workflow.
Verify: end-to-end fake-host path plus one controlled real sample/manual smoke path.

## 1.17 — Full-Pipeline Crash/Resume
Objective: prove event/outbox/recovery behavior across the complete V1 pipeline.
Verify: process termination at several checkpoints, restart, resume without duplicate terminal writes.

## 1.18 — Metrics, Budget Cap & OpenTelemetry
Objective: request/success/failure/timeout counters, token/cost tracking, hard per-runtime budget, and console OpenTelemetry.
Verify: configured limit produces typed stop; telemetry contains correlation data and no secrets.

---

# Phase 2 — Base Hive Membership & Coordination

## 2.1 — HiveDefinition & Membership
Define Hive identity, membership, roles, and membership lifecycle.

## 2.2 — Configurable Agent Composition
Add/remove/reorder Agents through Hive.Management without hard-coded application wiring.

## 2.3 — Hive ↔ Agent Communication
Use explicit message/DTO contracts with correlation/provenance.

## 2.4 — Shared Claims with Provenance
Provide governed shared claims without making the shared store authoritative over host domain state.

## 2.5 — Supervisor Controls
Observe/pause/stop members through Hive/MAF-supported mechanisms.

All coordination uses MAF orchestration primitives where applicable; Hive does not become a second workflow engine.

---

# Phase 3 — Hive Governance Patterns

## 3.1 — Manager-led Strategy
Define manager/supervisor selection and authority policy.

## 3.2 — Democratic/Voting Strategy
Add the first explicit voting rule and deterministic tie/insufficient-vote behavior.

## 3.3 — Adversarial/Critique Strategy
Add critique/challenge roles and bounded conflict reporting.

## 3.4 — Governance Strategy Selection UI
Expose governance mode and policy through Hive.Management.

---

# Phase 4 — CognitiveAgent : Agent

The base Agent and V1 pipeline continue working unchanged throughout this phase. CognitiveAgent is created explicitly; no runtime type promotion or demotion is introduced.

## 4.1 — Cognitive Kernel
Persistent cognitive identity binding, lifecycle, state versioning, recovery, and per-runtime concurrency ownership.

## 4.2 — Cognitive Strategy
Replaceable strategy contract capable of deterministic decisions and explicit no-model paths.

## 4.3 — Reasoning Requirement
Provider-neutral reasoning requirements kept separate from concrete Execution Target planning.

## 4.4 — Persistent Cognitive State
Beliefs, bounded workspace/attention, goals, intentions, plans, methods, self-model, impasses, and revision-safe transitions. Persistent state is independent of whether a runtime incarnation is currently active.

## 4.5 — Experience & Cognitive Event History
Bounded experience capture, provenance, actual outcomes, and replayable supported transitions. Actual observations/experiences remain distinguishable from simulated or predicted material.

## 4.6 — Death / Wake / Reincarnation Lifecycle
Define death as complete termination of the current runtime/incarnation, preserve Agent identity and cognitive state, support inactive periods with no live runtime, and explicitly reconstruct a new runtime from durable state when the Agent wakes.

## 4.7 — Postmortem & Dream Processing
Define bounded postmortem processing plus a Dream subsystem that can inspect history, generate hypothetical alternatives, run multiple simulations in parallel, compare predicted outcomes, and produce candidate cognitive-state updates without requiring the Agent runtime to remain alive.

## 4.8 — Questions
Define first-class Questions with structured context, specialty, provenance, answer type, evidence requirements, status, and confidence/uncertainty where applicable. Support specialty-specific questions so different Agents can investigate different aspects of the same user objective.

## 4.9 — Cognitive State Reconciliation
Integrate human edits, Dream results, Question answers, experience, beliefs, goals, plans, and other candidate updates through versioning, provenance, authorization, validation, and concurrency boundaries before the next wake/reincarnation.

---

# Phase 5 — Cognitive Resources

## 5.1 — Memory Resource Families
Working, episodic, semantic, procedural, and future extensible families with explicit scope/ownership.

## 5.2 — Knowledge / Wiki
Versioned, permissioned knowledge resources and managed Wiki source.

## 5.3 — Skills
Versioned reusable procedures, dependencies, constraints, provenance, and assignments.

## 5.4 — Learning Candidates & Governance
Experience → candidate → evidence/confidence → validation → approve/reject/promotion. No direct authoritative mutation from model output.

---

# Phase 6 — CognitiveHive : Hive

## 6.1 — Collective Cognitive State
Hive-level collective state is distinct from each member's own Agent/CognitiveAgent state.

## 6.2 — Collective Strategy
Coordinate planning/reasoning across members without moving member cognition into the Hive itself.

## 6.3 — Collective Questions & Specialty Routing
Route Questions by Agent specialty, avoid semantically duplicate work where evidence already exists, and allow each Agent to retain its own Questions and answers.

## 6.4 — Cross-Agent Evidence & Synthesis
Combine attributable answers, experiences, Dreams, observations, and other evidence into collective reasoning without erasing individual provenance.

## 6.5 — Collective Conflict & Consensus
Bounded coordination, conflict resolution, disagreement handling, and consensus mechanisms.

Base Hive coordination remains usable without CognitiveHive.

---

# Phase 7 — Generic Host Integration

Only pull this phase forward when a second real host application with meaningfully different integration requirements proves the need.

## 7.1 — Generic Host Context
Provider-neutral bounded host observations/context.

## 7.2 — Native Data-Source & Control Adapters
Generalize the exact patterns proven in V1 to DataTable, BindingSource, DataGridView, native collections, controls, and other host representations.

## 7.3 — Bounded Object Discovery
Cycle-safe, cancellation-aware, bounded, read-oriented discovery with no authority implication.

---

# Phase 8 — Multi-Tenancy, Scale, Configuration Portability & Extensibility

## 8.1 — Authentication Boundary
Add real authentication integration.

## 8.2 — Distributed Execution Decision Point
Re-evaluate Temporal/Dapr/distributed execution only from measured operational requirements.

## 8.3 — Configuration Import/Export
Versioned Hive configuration packages with compatibility/conflict handling.

## 8.4 — MCP / Tool Extensibility
Additional governed tool-extension boundary.

## 8.5 — Additional Host Surfaces
WPF/web/other hosts consume the same core and Management contracts.

---

# Phase 9 — Observability, Operations & Replay

## 9.1 — Full Metrics Taxonomy
Standardize platform metrics and operational dimensions.

## 9.2 — Dashboards & Operational Views
Management/operations visibility.

## 9.3 — CI/CD
Automated build, test, packaging, and verification pipelines.

## 9.4 — Event-Log Replay Regression
Replay durable event histories against stable contracts for regression detection.

## 9.5 — Long-Running Resilience
Long-duration concurrency, recovery, provider degradation, and resource-retention tests.

## 9.6 — Production Diagnostics & Support Tooling
Operational diagnostics, safe support exports, and controlled replay tooling.

---

## Ordering invariant

The order is intentional. The cognitive lifecycle, Dreams, and Questions remain CognitiveAgent-generation capabilities before collective cognition is added; CognitiveHive then extends them with cross-agent coordination without moving individual cognition into the Hive.


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
