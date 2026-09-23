# Hive — Roadmap

This is the ordered slice-level implementation plan. `docs/architecture.md` is the architectural source of truth; this file defines implementation order. Status belongs in `Hive_Current_Status.md`.

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

---

# Phase 0 — Foundations

## 0.1 — Solution & project scaffolding
Objective: create the complete initial solution structure:
- `Hive.Core`;
- `Hive.Agents`;
- `Hive.Persistence`;
- `Hive.Coordination`;
- `Hive.Tools`;
- `Hive.Providers.OpenAICompatible`;
- `Hive.Management`;
- `Hive.Host.WinForms`;
- `Hive.Host.WinForms.UI`;
- `Hive.Example.WinForms`;
- `Hive.Tests`.

Establish the dependency direction from the beginning. `Hive.Host.WinForms.UI` owns the Hive WinForms presentation implementation, `Hive.Host.WinForms` consumes the UI foundation, and `Hive.Example.WinForms` consumes public platform contracts plus the WinForms/UI layers. No core/platform project may depend on the Example host.

Verify: solution builds; forbidden references are absent; the Hive UI implementation remains behind `Hive.Host.WinForms.UI`; the Example host is isolated from test-framework internals.

## 0.2 — Common infrastructure
Objective: IDs, immutable value objects, typed errors/results, `IClock`, event envelope with event type and payload schema version, correlation/causation IDs, event upcasting compatibility boundary, and one JSON serialization stack.
Verify: normal/invalid/boundary unit tests, JSON round-trip, older-event payload upcast tests, and rejection of unsupported event schema versions.

## 0.3 — Identity, WorkItem & Resource foundation
Objective: Deployment/Tenant/Principal/User/Session/Workspace/Agent/Hive/Runtime/Execution/WorkItem identity, Resource envelope, ownership, scope, provenance, lifecycle/version metadata.
V1 work-unit rule: one submitted document is one WorkItem; a batch is multiple WorkItems.
Verify: scope matrix, missing-identity fail-closed cases, immutable identity snapshots, WorkItem lifecycle and provenance isolation.

## 0.4 — Persistence bootstrap
Objective: Hive-owned SQL Server database, LocalDB development setup, DbUp migrations, schema-version tracking, indexes.
Verify: clean install, repeat migration, failed migration, incompatible future schema.

## 0.5 — Test harness
Objective: xUnit scaffolding, fake provider infrastructure, fake clock, test-database strategy, deterministic event-test conventions.
Verify: baseline tests pass and automated tests make no real vendor/network calls.

## 0.6 — WinForms UI/UX Foundation
Objective: establish the shared WinForms visual foundation used by Hive.Host.WinForms and Hive.Example.WinForms. Hive owns the theme contract, semantic design tokens, and the small set of Hive-specific controls required by consumers. The current implementation uses native WinForms controls and custom System.Drawing rendering; application forms consume Hive-owned contracts.

The initial foundation includes:
- Light / Dark / System theme modes;
- Hive-owned palette, typography, spacing, and common visual-state tokens;
- HiveForm as the reusable application-window shell;
- HiveButton with Primary / Secondary / Navigation styles;
- HiveMessageBox with semantic message types and optional technical details;
- Hive-specific controls only where Hive needs behavior or styling beyond ordinary WinForms controls;
- native WinForms controls and custom `System.Drawing` rendering owned by `Hive.Host.WinForms.UI`;
- reusable data-page composition primitives: a header/action/content list layout and an optional pagination bar;
- `HiveCrudPage<TItem>` for generic Add/Edit/Delete/Refresh UI orchestration over consumer-supplied callbacks, including compact toolbar layout and an integrated `HivePaginationBar` footer;
- the CRUD presentation standard: clear title/description hierarchy, optional search, primary Add action separated from contextual Edit/Delete actions, predictable loading/empty/no-match states, keyboard-friendly list interaction, and compact record-count/status feedback;
- reusable editor-layout composition for repeated labeled-field and action-footer patterns; `HiveEditorLayout` supplies presentation only and does not own field semantics or validation, while standardizing field rhythm and action-footer alignment;
- domain pages keep their own schemas, columns, filters, validation, authorization, specialized editors, and persistence behavior.

Do not create a complete replacement control toolkit or wrap every WinForms control merely to rename it. Keep rendering implementation details inside `Hive.Host.WinForms.UI` so consuming forms depend only on Hive-owned UI contracts. A different renderer may be introduced later only through an explicit architectural decision.

Verify: a representative sample form renders in Light and Dark modes, shared styling is consistent, consuming forms use only Hive-owned UI contracts, and the UI implementation can evolve without changing consumer-facing Hive UI contracts.

## 0.7 — First-Class Example Host Shell
Objective: make `Hive.Example.WinForms` a permanent developer-facing application rather than a temporary demonstration.

The shell uses scalable navigation:
- Category;
- Subcategory;
- Example.

Use a left-side tree/list navigation surface and a right-side replaceable example `UserControl`. Do not use nested Category → Subcategory → Example TabPages as the primary navigation model.

Define a small discovery contract such as `IHiveExample` with category, subcategory, title, and a `CreateView(IServiceProvider services)` factory. Discover only designated example assemblies so adding an example requires implementing the contract without manual shell wiring.

Examples are grouped by feature area and grow with the platform. The shell itself uses only Hive-owned UI contracts.

Verify: adding one new example implementation makes it appear in navigation without additional shell wiring; selecting an example replaces the content view correctly; navigation remains usable with many examples; Light/Dark/System theme changes preserve navigation selection and scroll position; representative CRUD and dialog surfaces remain readable and consistent at supported compact and normal window sizes; no avoidable UI warnings or resource-lifetime regressions are introduced.

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
Verify: local fake-server tests for success, malformed response, timeout, cancellation, authentication failure, rate limit, transport failure, and structured-output failure; the developer may manually verify a real provider when appropriate.

## 1.4 — Capability-aware Execution Target Selection
Objective: required-capability filtering and explainable selection diagnostics.
Verify: supported match, unsupported exclusion, unknown exclusion for hard requirements, no-qualifying-target, fixed-target failure.

## 1.5 — Base Agent & AgentFactory
Objective: implement `Agent`, `AgentDefinition`, `RuntimeInstance`, `Execution`, and `AgentFactory.Create<TAgent>()` against base contracts. The selected generation is fixed when the Agent is created; there is no runtime promotion/demotion.

Generation creation is explicit and policy-authorized. The factory may create any supported generation when the caller's authorization/policy permits it; it never infers CognitiveAgent creation from task complexity.

Verify: multiple runtimes from one definition remain isolated; generation selection is explicit; unauthorized generation creation is rejected; the factory does not require cognitive types.

## 1.6 — Base Agent Work Protocols
Objective: add only the reusable base mechanisms required by the V1 boundary: Objective lifecycle, WorkItem binding/provenance, memory storage/retrieval infrastructure, Question/Answer transport, Patience / Understanding Gate, and delegation interfaces.

Simulation/Dream execution and Agent-owned Hive creation are architecturally supported mechanisms but are not pulled into this slice unless a V1 boundary actually requires them.

These mechanisms must not autonomously form/revise cognitive Goals or Beliefs, select Dreams, generate adaptive Questions, reinterpret experience, or learn from outcomes.

Verify: objective and WorkItem lifecycle, question waiting/timeout, minimum-understanding gates, delegation ownership/provenance, and isolation across multiple runtime instances.

## 1.7 — Event Log, Snapshots & Transactional Outbox
Objective: append-only event log, snapshot fold, and atomic event+snapshot+outbox persistence.
Verify: rollback leaves neither event nor outbox; replay of supported base events is deterministic.

## 1.8 — Outbox Poller
Objective: process committed unhandled outbox rows after transaction commit.
Verify: duplicate delivery safety and crash-before-processing recovery.

## 1.9 — First Real Agent Execution
Objective: connect a base Agent to MAF and the Hive provider boundary for one request, with correlation and durable lifecycle events.
Verify: fake-provider automated path plus manual real-provider developer verification when appropriate.

## 1.10 — Hive.Management Facade
Objective: CRUD facade for Providers, ProviderAccounts, ExecutionTargets, and AgentDefinitions.
Verify: service-level validation, authorization/scope cases, persistence integration.

## 1.11 — V1 Workspace & WorkItem Operations
Objective: implement the V1 operational Workspace over Hive.Management for image submission and governed business-app processing.

V1 scope:
- submit/attach an image to a WorkItem;
- view WorkItem status and activity;
- view relevant execution/provider status;
- receive WorkItem notifications;
- view PendingApproval;
- Approve / Reject the governed business-app write.

The V1 Workspace works with a single Agent and does not require Hive membership or Swarm state.

Verify: image submission creates the correct WorkItem, status/activity are visible, approval state is visible, Approve/Reject changes the authoritative WorkItem state correctly, stale approval is rejected, and the Workspace does not create hidden Hive/Swarm behavior.

## 1.12 — Global Hive Settings & Host Configuration
Objective: establish Hive Settings as the permanent global Hive package configuration center and make its authoritative configuration drive real host behavior. Phase 1.12 establishes the first concrete configuration domains while creating the reusable shell that later Hive capabilities extend.

The global configuration center owns the user-facing configuration surface for the Hive package. Current concrete domains are:
- Providers;
- ProviderAccounts / ExecutionTargets;
- AgentDefinitions and their configured execution relationship;
- Persistence.

Future Hive-owned configuration domains are added under this same Settings center when their authoritative contracts exist. Do not create parallel top-level settings roots for later tools, policy/permissions, runtime defaults, cognitive/resource configuration, host/integration configuration, or other package-owned settings.

Provider configuration:
- provider/account setup and provider connection test through the management/configuration boundary;
- no provider transport or persistence implementation in the WinForms shell.

Persistence configuration:
- typed Hive persistence configuration exposed through the Management boundary;
- V1 SQL Server configuration including server/instance, port, authentication metadata, database identity, and required connection-security options;
- SQL Server LocalDB represented as the same SQL Server persistence boundary for local development;
- Hive resource credentials use the database-backed Secret Store once Hive.Persistence is available;
- SQL-password bootstrap credentials use a separate user-scoped DPAPI-protected bootstrap boundary outside the target Hive database and are referenced rather than stored as plaintext;
- non-destructive connection test that only verifies connectivity and does not create the database or apply migrations;
- separate database/schema status and explicit initialization/migration lifecycle;
- saved persistence settings survive application restart without exposing the bootstrap password in ordinary configuration or diagnostics;
- only absence of saved configuration permits the typed `LocalDevelopment()` first-run default; invalid/unavailable saved configuration does not silently fall back;
- persisted `createDatabaseIfMissing` configuration does not make connection tests or ordinary startup composition perform database/schema mutation.

Agent configuration:
- AgentDefinition configuration may reference existing ExecutionTargets;
- the AgentDefinition does not duplicate Provider/ProviderAccount/ExecutionTarget endpoint, credential, model, or capability state;
- the resolved ExecutionTarget remains authoritative for concrete execution details.

Settings UI:
- uses the Hive.Host.WinForms.UI foundation, including HiveNavigationTree for navigation and HiveListPageLayout / HiveCrudPage<TItem> / HiveListView / HiveEditorLayout where applicable;
- no parallel Settings-specific navigation/list renderer or theme system.

Host consumption:
- the host application composition boundary owns the current persistence-backed service graph; `Hive.Example.WinForms` is a consumer of that boundary, not its architectural owner;
- the composition boundary depends on a stable bootstrap-credential contract; the concrete DPAPI-backed storage mechanism is introduced by the bootstrap sub-stage and remains replaceable behind that contract;
- the Example Host and future WinForms applications consume the same persisted configuration edited by Settings;
- Persistence changes recompose the persistence-backed service graph;
- resource configuration changes refresh authoritative Management state without unnecessary persistence reconstruction;
- configured Agents are used by normal host operations rather than by a Settings-only test.

The Settings shell owns navigation/composition only. Persistence pages must not construct `SqlConnection`, execute SQL directly, or duplicate Hive.Persistence migration/bootstrap rules.

Depends on: the existing 1.2 Hive resource Secret Store, the existing Hive.Persistence SQL Server boundary, the existing Management facade, and the Phase 0 Hive.Host.WinForms.UI foundation. Phase 1.12-B introduces the separate bootstrap-credential infrastructure required for SQL-password startup access.

Verify:
- global Settings is reachable as the normal package configuration center;
- Management logic remains outside the WinForms shell;
- persistence configuration can be saved, reloaded, and validated through the public Management contract;
- connection-test success/failure is reported without schema side effects;
- database/schema status is distinct from connection success;
- credentials are not persisted in plaintext or exposed in diagnostics;
- Settings uses the shared Hive UI navigation/list/editor foundation;
- a configured Agent can be selected and used by a normal host operation;
- developer manually verifies the complete configured-host flow.

## 1.13 — Image Input & WinForms Host Context
Objective: establish image as the first V1 input boundary and define the concrete WinForms host-context discovery contract.

Current WinForms boundary:
- `HiveWinFormsHostContext.Register(Form)` explicitly registers a host root;
- discovery returns immutable metadata snapshots rather than raw `Control` references;
- discovery covers Form, UserControl, custom/inherited controls, Panels, GroupBoxes, other containers, nested descendants, and relevant binding/data-source metadata;
- traversal is deterministic, bounded by configurable depth/node/text limits, cancellation-aware, and duplicate-reference safe;
- password control text is redacted;
- provenance records the registered resource access identity plus registration/capture identifiers;
- discovery grants no click/edit/invoke/mutation authority.

Image proof uses a checked-in deterministic fixture and the existing `WorkItemImageSubmission` contract; no duplicate image persistence/storage boundary is introduced.

Verify: focused host-context/image-fixture tests, bounded/cancellation/ownership coverage, and manual Example Host verification through:
`Host / WinForms Integration / Image Input & WinForms Host Context`.

## 1.14 — Dual Business-App Integration Contract
Type: architecture/contract implementation slice.
Objective: support both API/service and bounded UI integration. The same WorkItem or operation may use either path or both; the choice is made by actual operation capability and authorization, not a global API-vs-UI architecture gate.
Verify: fake API path, fake/bounded WinForms UI path, authorization boundary, provenance, and combined API+UI path where a real operation needs both.

## 1.15 — Vision Routing
Objective: rasterize/prepare non-text-extractable pages and route them to a Vision-capable execution target.
Verify: fixed scanned/image sample, unsupported-capability failure, bounded page/image handling.

## 1.16 — Structured Extraction & Validation
Objective: structured-output extraction to typed candidate data with required-field/type/domain validation.
Verify: valid sample, missing fields, invalid types, malformed model output, rejection path.

## 1.17 — Business-App Write Tool
Objective: propose a write, hold `PendingApproval`, and perform the write only after explicit approval.
Verify: pending blocks execution; rejection prevents side effect; approval reaches a fake client; duplicate approval cannot duplicate the write.

## 1.18 — MAF Sequential V1 Pipeline
Objective: wire ingest → extract → validate → write as one MAF Sequential workflow.
Verify: end-to-end fake-host path plus developer manual verification with one controlled real sample when available.

## 1.19 — Full-Pipeline Crash/Resume
Objective: prove event/outbox/recovery behavior across the complete V1 pipeline.
Verify: process termination at several checkpoints, restart, resume without duplicate terminal writes.

## 1.20 — Metrics, Budget Cap & OpenTelemetry
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

## 2.6 — Agent-Owned Hive Creation & Hive Lifecycle
Allow an Agent to explicitly create/sponsor a persistent Hive for a bounded need without changing the Agent's own type. Sponsorship is a relationship, not implicit lifecycle ownership; sponsor death/retirement/deletion does not automatically delete the Hive or its members.

## 2.7 — Specialty-Driven Population & Swarm Participation
Allow an authorized Hive to create or reuse Agents of any supported generation for missing specialties, including CognitiveAgents when the Hive's population policy explicitly permits that generation.

Define Swarm as the active subset of Hive members collaborating on a WorkItem, Question, or bounded problem. Swarm is derived/session state, not a persistent resource. A member Agent normally requests missing specialists through the parent Hive rather than recursively creating a child Hive.

All coordination uses MAF orchestration primitives where applicable; Hive does not become a second workflow engine.

## 2.8 — Workspace Coordination & Agentic Extensions
Extend Workspace after Hive membership and Swarm contracts exist.

Objective: add Agent/Hive organization and topology, active Swarm visibility, and Agentic mode where an Agent/Hive selects execution targets through the normal planner/policy boundary.

General LLM mode with explicit model selection may be added here as a general Workspace capability; it is not part of V1 document processing.

Verify: topology reflects authoritative Hive membership, Swarm views reflect derived active membership, Agentic mode displays the selected execution target, and Workspace does not create Hive/Swarm state merely by displaying it.


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

The base Agent and V1 pipeline continue working unchanged throughout this phase. The base Agent may already provide Objectives, Question transport, patience/understanding gates, memory infrastructure, simulations, delegation, and Hive creation as reusable mechanisms. CognitiveAgent is created explicitly and adds adaptive cognition over those mechanisms; no runtime type promotion or demotion is introduced.

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
Define bounded postmortem processing plus a Dream subsystem that can inspect history, generate hypothetical alternatives, run multiple simulations in parallel, compare predicted outcomes, and produce candidate cognitive-state updates without requiring the Agent runtime to remain alive. Dream processing is governed by applicable authorization, provider/model quota, token/cost budget, time budget, concurrency/parallelism limits, retrieval/work limits, and cancellation.

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

# Phase 7 — Additional Generic Host Integration

Only pull this phase forward when a second real host application with meaningfully different integration requirements proves the need to generalize patterns already proven by the V1 WinForms boundary.

## 7.1 — Generic Host Context
Provider-neutral bounded host observations/context.

## 7.2 — Cross-Host Data-Source & Control Adapters
Generalize V1's WinForms integration patterns to other host representations only when a second real host requires it.

## 7.3 — Cross-Host Bounded Object Discovery
Generalize the proven V1 discovery contract to other UI/object models. Discovery remains cycle-safe, cancellation-aware, bounded, read-oriented, and never grants action authority.


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
