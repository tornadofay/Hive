# Hive — Architecture & Roadmap (Living Document)

Last updated: 2026-09-19 (rev 5 — one shared OpenAI-compatible adapter, Execution Target concept, proven WinForms config shape, AGENTS.md-derived rules)

Treat this file as the single source of truth. Update it before any structural code change. Status lives only in `Hive_Current_Status.md`; the current work slice lives only in `Hive_Active_Work.md` (Rule 21).

## 0. Purpose & Scope

- V1 use case: automating data entry from documents/images into the user's own business app, via a fixed multi-agent pipeline.
- Clean rewrite; HAgent is a design reference only, but its validated, real patterns are adopted directly where they are genuinely good: the single shared OpenAI-compatible adapter, the WinForms configuration shape, and a curated set of its engineering rules (Section 7.1).
- V1 interface is WinForms; all logic UI-agnostic; Hive is a pure library first.
- Full `Tenant → Users → Agents → Hives → Resources` hierarchy modeled from day one.

## 1. The MAF Dependency Boundary

**Hard rule: use MAF whenever it already owns the behavior. Hive adds only the semantics MAF does not own.**

MAF owns: agent pipeline, tool invocation, workflow orchestration (Sequential/Concurrent/Handoff/GroupChat/Magentic), checkpoints, generic HITL, basic model routing, the actual model-service call.

Hive owns: the provider control plane (Section 2), the document ingestion pipeline (Section 3), the management surface (Section 4), resource ownership (Section 5), agent identity (Section 6), learning governance, Hive membership/roles/authorization and — from Phase 5 — governance patterns, persistent cognition. **The LLM never becomes the authority.**

Migration note: evaluated against MAF's current capabilities only; a future MAF equivalent is a deliberate migration decision, never automatic.

## 2. Core Tech Stack & Provider Platform

| Concern | Choice | Why |
|---|---|---|
| Language / runtime | C# / .NET 10 only | — |
| Agent programming model | MAF | Section 1 |
| Execution model | Ephemeral, in-process, transactional outbox | No orchestrator needed for single-process V1 |
| Persistence | SQL Server (LocalDB for dev), Hive's own dedicated database, fully separate from the business app's database | Hive persistence never grants implicit access to the host application's business database |
| Vector storage | SQL Server `VECTOR`/`VECTOR_DISTANCE` behind `IVectorStore` | DiskANN remains deferred |
| Provider adapter implementation | **One shared `OpenAICompatibleProvider` class** | One compatible wire-format adapter can serve all V1-compatible providers and local servers |
| Provider / ProviderAccount / Execution Target | `Provider` = vendor integration; `ProviderAccount` = credential under a provider; `Execution Target` = Provider + Account + Model | Capability lives at the concrete target level |
| Capability state | `Supported` / `Unsupported` / `Unknown`, with evidence where practical | Never silently collapse capability to a boolean |
| Model/provider selection | Capability matching against the Execution Target is a mandatory hard filter | Prevents unsupported execution |
| Secrets | Provider API keys entered via config UI, encrypted at rest in Hive's own database, redacted from diagnostics/logs/traces | No separate vault subsystem unless a real future requirement demands one |
| Local dev | SQL Server LocalDB | — |

**Explicitly deferred/rejected:** Temporal/Dapr, PostgreSQL/pgvector, Elasticsearch, Akka.NET, Orleans, DiskANN, a Hive-built orchestration engine, a bespoke secrets-vault subsystem before it's needed.

## 3. Document & Vision Ingestion Pipeline

```text
Input file (PDF/Word/Excel/image) → format-specific parsing → route to capability
(text → Chat; scanned/image → Vision) → Structured Output extraction → validation
→ business-app write (governed Tool, human-approved, via the business app's own
  exposed interface — never its raw database)
```

## 4. Management Surface

`Hive.Management` is a thin facade over `Hive.Core` / `Hive.Persistence` resource APIs, not a parallel configuration store.

`HiveSettingsForm` is a composition shell: a left navigation panel plus a content panel, registering each configuration area by name to page factory. Feature logic does not belong in the shell.

`HiveConfigurationContext` is one plain object carrying shared dependencies needed by configuration pages.

`ISecretStore` is a dedicated interface for credential encryption/decryption/redaction.

Each configuration area is its own page under `Hive.Host.WinForms/UI/Configuration/<Area>/`. Future WPF/web hosts call the same management facade and configuration-context shape.

## 5. Generic Resource Model

`Resource { Identity, Owner, Scope, Version, Provenance, Lifecycle, Permissions, Metadata }` is the shared shape for `Memory`, `Knowledge`, `Skill`, `LearningCandidate`, `Conversation`, `AgentState`, `CognitiveState`, `Provider`, `ProviderAccount`, `AgentDefinition`, and `HiveDefinition`.

Canonical scopes: `Global`, `Tenant`, `User`, `Workspace`, `Agent`, `Runtime`, `Execution`. Missing identity is valid only where a subsystem explicitly permits single-user/anonymous operation; identity-required boundaries fail closed by default.

## 6. Agent Identity — Three Levels

`Agent Definition → Runtime Instance → Executions`.

"Death" is four levels: execution / runtime instance / agent lifecycle / cognitive identity. Whatever is event-sourced survives.

### Config-execution isolation

An active execution runs against an **immutable snapshot** of its configuration taken at start. A configuration edit made while an execution is running must never alter that execution mid-flight.

## 7. Non-Negotiable Rules

1. Every agent owns its full cognitive stack.
2. State is append-only events, never mutated rows.
3. Death ≠ deleted. Postmortem produces candidate updates only.
4. Belief updates require evidence + confidence, human-gated in the MVP. V1 learning = memory/knowledge updates only.
5. Every persisted entity carries a `TenantId` / canonical scope.
6. LLM calls go through an abstraction interface. Capability matching against the Execution Target is mandatory.
7. Every incarnation has a hard token/call budget. Rate limits, quota, and observed operational state are three different things.
8. Tools are permissioned per agent. MAF owns invocation mechanics; Hive owns authorization.
9. **The LLM never becomes the authority.** The model proposes; Hive decides.
10. Hive↔Agent communication is message/DTO-shaped, not shared mutable references.
11. Coordination mechanics run on MAF orchestration primitives. Hive never reimplements them.
12. Every host UI is a thin shell calling only `Hive.Management`.
13. Testing follows from Rule 12: most coverage is unit/contract tests on the libraries; hosts need only thin UI-automation smoke tests.

### 7.1 Rules Adopted From HAgent's Real Codebase and AGENTS.md

14. No empty catch blocks.
15. One JSON serializer.
16. Config surfaces must actually be read by the code implementing the behavior, test-covered.
17. No duplicated implementations of the same computation.
18. Structured error classification preferred over string-matching.
19. Timeout settings explicitly wired/validated at startup.
20. Repeated ID lookups use an actual index.
21. Status lives in `Hive_Current_Status.md`; the current work slice lives in `Hive_Active_Work.md` — nowhere else.
22. Phases are plain integers.
23. Test coverage claims only in the status file, backed by real tests.
24. Running executions use immutable configuration snapshots.
25. Capability is three-state: `Supported` / `Unsupported` / `Unknown`.
26. Observability must never leak secrets or sensitive payloads; use correlation IDs with configurable redaction by default.
27. Terminal execution outcomes are protected against late provider responses.
28. Independent runtime instances never share mutable state; shared infrastructure is reused only through concurrency-safe contracts.
29. Not every cognitive decision requires an LLM call; use deterministic decisions when existing state, policy, or memory already determine the answer.
30. Network-provider automated tests use fakes/local test infrastructure, never a real vendor.
31. Complete-per-slice, not complete-for-everything: fully implement the active slice without building ahead into future phases.

## 8. Roadmap

### Phase 0 — Foundations

- Solution layout: `Hive.Core`, `Hive.Agents`, `Hive.Persistence`, `Hive.Coordination`, `Hive.Tools`, `Hive.Providers.OpenAICompatible`, `Hive.Management`, `Hive.Host.WinForms`, `Hive.Tests`
- Common infrastructure; Hive's own database, separate from any business-app database

### Phase 1 — Multi-Agent Pipeline MVP — current phase

- `Hive.Providers.OpenAICompatible`: one adapter implementation, configured against the concrete providers recorded in `Hive_Current_Status.md` as `Provider` / `ProviderAccount` / `Execution Target` records
- Several Agent Definitions in a fixed pipeline (ingest → extract → validate → write) via MAF Sequential orchestration
- Document & Vision Ingestion Pipeline, end-to-end for at least one document type
- `EventLog`, `Snapshots`, `Outbox`, Resource-shaped tables
- `Hive.Management` + `HiveSettingsForm` / `HiveConfigurationContext`, minimum Overview, Providers, Agents
- `Hive.Tests` covering Core/Agents/Persistence/Management from the start
- Capability-aware selection; config-execution snapshot isolation; terminal-state protection
- Transactional outbox; business-app write Tool, human-approved
- Manual crash/resume test across the whole pipeline
- Minimal metrics + budget per incarnation; OpenTelemetry from the start

### Phase 2 — Persistence Hardening

### Phase 3 — Death / Postmortem / Reincarnation

### Phase 4 — Hive Membership & Coordination

### Phase 5 — Hive Governance Patterns (Manager-led / Democratic-Voting / Adversarial-Critique)

### Phase 6 — Cognitive Safety

### Phase 7 — Multi-Tenancy & Scale

### Phase 8 — Tooling & Extensibility

### Phase 9 — Observability & Ops

## 9. Concept Reference

Foundation, Provider Platform, Execution Selection, Resource & Persistence, Normal Agent, Agent Runtime, Hive (governance patterns from Phase 5), Cognitive Agent, Cognitive Hive (optional, later, not default), Composition.

## 10. Open Questions

- What API/protocol does the business app expose for the write step?
- Which document type should Phase 1 handle first?
- Which voting/conflict-resolution rule for Phase 5?
- Which UI-automation tool for WinForms smoke tests?
- Which auth provider for Phase 7?
