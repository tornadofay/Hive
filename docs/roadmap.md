# Hive — Roadmap

This is the ordered slice-level roadmap and navigation index. Each slice links to its detailed plan under `docs/plan/`. `docs/architecture.md` is the architectural source of truth; this file defines implementation order. Status belongs in `Hive_Current_Status.md`.

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

Detailed implementation plan: [0.1 — Solution & project scaffolding](plan/Phase0/0.1.md)

Objective: create the complete initial Hive solution structure and establish the dependency direction between the core, platform, WinForms, Example Host, and test projects.

Scope and non-goals: foundation scaffolding only; the Example Host remains a consumer and never becomes a platform dependency, while Hive.Host.WinForms.UI remains the owner of Hive presentation implementation.

Verify: solution builds; forbidden references are absent; Hive UI implementation stays behind Hive.Host.WinForms.UI; the Example Host is isolated from test-framework internals.

## 0.2 — Common infrastructure

Detailed implementation plan: [0.2 — Common infrastructure](plan/Phase0/0.2.md)

Objective: establish shared IDs/value objects, typed errors/results, IClock, versioned event envelopes, correlation/causation identity, event upcasting, and one JSON serialization stack.

Scope and non-goals: common infrastructure only; domain-specific persistence, orchestration, and later-generation cognition are not pulled into this foundation slice.

Verify: normal, invalid, and boundary behavior; JSON round trips; older-event payload upcasting; unsupported event-schema versions are rejected.

## 0.3 — Identity, WorkItem & Resource foundation

Detailed implementation plan: [0.3 — Identity, WorkItem & Resource foundation](plan/Phase0/0.3.md)

Objective: establish stable typed identity, ResourceEnvelope, ownership, scope, provenance, lifecycle, and version metadata for Hive resources and WorkItems.

Scope and non-goals: identity/resource contracts remain neutral and durable; a WorkItem is the durable user-visible work unit, while submission/batch grouping never replaces WorkItem identity, lifecycle, provenance, authorization, or later Review/receipt state.

Verify: scope matrix; missing-identity fail-closed behavior; immutable identity snapshots; WorkItem lifecycle and provenance isolation.

## 0.4 — Persistence bootstrap

Detailed implementation plan: [0.4 — Persistence bootstrap](plan/Phase0/0.4.md)

Objective: establish the Hive-owned SQL Server persistence foundation, LocalDB development setup, DbUp migrations, schema-version tracking, and required indexes.

Scope and non-goals: persistence bootstrap only; later domain persistence does not become part of this slice.

Verify: clean installation; repeat migration; failed migration handling; incompatible future-schema handling.

## 0.5 — Test harness

Detailed implementation plan: [0.5 — Test harness](plan/Phase0/0.5.md)

Objective: establish the authoritative xUnit test harness, fake provider infrastructure, fake clock, test-database strategy, and deterministic event-test conventions.

Scope and non-goals: test infrastructure only; automated tests must not depend on real vendor accounts or external network services.

Verify: baseline tests pass and automated tests remain isolated from real vendor/network calls.

## 0.6 — WinForms UI/UX Foundation

Detailed implementation plan: [0.6 — WinForms UI/UX Foundation](plan/Phase0/0.6.md)

Objective: establish the shared Hive WinForms visual foundation, including Light/Dark/System themes, semantic design tokens, HiveForm, HiveButton, HiveMessageBox, reusable list/editor composition, CRUD/pagination primitives, and the small set of justified Hive-specific controls.

Scope and non-goals: presentation foundation only; do not build a replacement control toolkit or wrap ordinary WinForms controls merely to rename them. Rendering stays inside Hive.Host.WinForms.UI, while domain pages retain their own schemas, validation, authorization, and persistence behavior.

Verify: representative forms render correctly in Light and Dark modes; shared styling is consistent; consuming forms use Hive-owned UI contracts; the implementation can evolve without changing consumer-facing UI contracts.

## 0.7 — First-Class Example Host Shell

Detailed implementation plan: [0.7 — First-Class Example Host Shell](plan/Phase0/0.7.md)

Objective: make Hive.Example.WinForms a permanent developer-facing application with scalable Category → Subcategory → Example discovery and replaceable example views.

Scope and non-goals: the Example Host is a consumer/test surface, not a platform dependency. Navigation uses a left-side tree/list and a replaceable right-side UserControl rather than nested feature TabPages; examples are discovered through the designated example contract/assembly boundary.

Verify: adding an example requires no shell wiring; selecting examples replaces the content view correctly; navigation scales; theme changes preserve navigation state; representative CRUD/dialog surfaces remain usable at supported sizes; no avoidable UI/resource-lifetime regressions are introduced.

# Phase 1 — Base Agent, Provider Platform, Management UI, and Data-Entry Pipeline (V1)

## 1.1 — Provider / ProviderAccount / ExecutionTarget

Detailed implementation plan: [1.1 — Provider / ProviderAccount / ExecutionTarget](plan/Phase1/1.1.md)

Objective: establish the durable Provider → ProviderAccount → ExecutionTarget resource model and three-state capability representation that later provider selection and discovery use.

Scope and non-goals: define the core provider resources, ownership/scope, lifecycle and persistence boundaries without introducing discovery or a second provider model.

Verify: CRUD and persistence behavior; ownership/scope enforcement; duplicate identities; malformed state; Supported/Unsupported/Unknown capability handling.

## 1.2 — Secret Store

Detailed implementation plan: [1.2 — Secret Store](plan/Phase1/1.2.md)

Objective: provide DPAPI-backed secret storage with secure replacement, deletion, and redaction.

Scope and non-goals: secrets remain behind the existing Hive secret-store boundary; plaintext persistence and secret exposure through diagnostics or public contracts are not allowed.

Verify: no plaintext persistence; redacted diagnostics; replacement invalidates the previous credential; secure deletion behavior.

## 1.3 — OpenAI-compatible Provider Adapter

Detailed implementation plan: [1.3 — OpenAI-compatible Provider Adapter](plan/Phase1/1.3.md)

Objective: provide one OpenAI-compatible provider adapter parameterized by endpoint and credentials for compatible hosted and local targets.

Scope and non-goals: one shared transport implementation only; provider-specific persistence or multiple parallel transport architectures are out of scope.

Verify: fake-server success, malformed responses, timeout, cancellation, authentication failure, rate limiting, transport failure, and structured-output failure.

## 1.4 — Capability-aware Execution Target Selection

Detailed implementation plan: [1.4 — Capability-aware Execution Target Selection](plan/Phase1/1.4.md)

Objective: select ExecutionTargets using explicit capability requirements and explain why a target qualifies or is rejected.

Scope and non-goals: capability-aware selection only; it does not invent capabilities or bypass a fixed target's authoritative configuration.

Verify: supported matches; unsupported and hard-Unknown exclusion; no qualifying target; fixed-target failure; explainable diagnostics.

## 1.5 — Base Agent & AgentFactory

Detailed implementation plan: [1.5 — Base Agent & AgentFactory](plan/Phase1/1.5.md)

Objective: implement the Base Agent resource/runtime model and explicit `AgentFactory` creation path.

Scope and non-goals: Agent generation is fixed at creation; no runtime promotion/demotion and no implicit CognitiveAgent creation from task complexity.

Verify: isolated runtimes; explicit generation selection; unauthorized generation creation is rejected; factory remains independent of cognitive types.

## 1.6 — Base Agent Work Protocols

Detailed implementation plan: [1.6 — Base Agent Work Protocols](plan/Phase1/1.6.md)

Objective: add reusable Base-Agent work mechanisms required by V1, including Objectives, WorkItems, Questions/Answers, patience/understanding gates, memory infrastructure, and delegation.

Scope and non-goals: these mechanisms remain reusable base infrastructure and do not autonomously perform cognitive learning, Dream selection, adaptive questioning, or belief/goal revision.

Verify: lifecycle/provenance, question waiting and timeout, understanding gates, delegation ownership, and multi-runtime isolation.

## 1.7 — Event Log, Snapshots & Transactional Outbox

Detailed implementation plan: [1.7 — Event Log, Snapshots & Transactional Outbox](plan/Phase1/1.7.md)

Objective: establish append-only event history, snapshot folding, and atomic event/snapshot/outbox persistence.

Scope and non-goals: state-changing persistence remains append-oriented and transactional; replay must not become a second state-management model.

Verify: atomic rollback, deterministic supported-event replay, and consistency between event, snapshot, and outbox state.

## 1.8 — Outbox Poller

Detailed implementation plan: [1.8 — Outbox Poller](plan/Phase1/1.8.md)

Objective: process committed unhandled outbox work after transaction commit with safe recovery behavior.

Scope and non-goals: the poller consumes durable outbox records; it does not make delivery itself the source of truth for business state.

Verify: duplicate-delivery safety, crash-before-processing recovery, and correct handling of committed outbox records.

## 1.9 — First Real Agent Execution

Detailed implementation plan: [1.9 — First Real Agent Execution](plan/Phase1/1.9.md)

Objective: connect one Base Agent request path to MAF and the Hive provider boundary with correlation and durable lifecycle events.

Scope and non-goals: one real execution path only; later multi-Agent assignment and full V1 pipeline composition remain later slices.

Verify: deterministic fake-provider execution, durable lifecycle/correlation behavior, and appropriate manual real-provider verification.

## 1.10 — Hive.Management Facade

Detailed implementation plan: [1.10 — Hive.Management Facade](plan/Phase1/1.10.md)

Objective: expose the authoritative Management facade for Providers, ProviderAccounts, ExecutionTargets, and AgentDefinitions.

Scope and non-goals: Management owns application operations and validation; WinForms remains a consumer and does not bypass the facade.

Verify: service-level validation, authorization/scope behavior, and persistence integration.

## 1.11 — Initial Workspace & WorkItem Operations

Detailed implementation plan: [1.11 — Initial Workspace & WorkItem Operations](plan/Phase1/1.11.md)

Objective: establish the initial Workspace over Hive.Management for image-backed WorkItems and governed approval interaction.

Scope and non-goals: this first Workspace slice uses a single Agent and does not create hidden Hive/Swarm state; direct LLM/Agent interaction and multiple independent Agents come later.

Verify: image submission, WorkItem status/activity, approval state, Approve/Reject, stale approval rejection, and no implicit Hive/Swarm behavior.

## 1.12 — Global Hive Settings & Host Configuration

Detailed implementation plan: [1.12 — Global Hive Settings & Host Configuration](plan/Phase1/1.12.md)

Objective: establish Hive Settings as the permanent global package configuration center and make its authoritative settings drive real host behavior.

Scope and non-goals: Settings is the configuration shell over Management; persistence/provider logic stays outside the UI, and later configuration domains extend this same center rather than creating parallel roots.

Verify: configuration save/reload/validation, non-destructive connection testing, database/schema distinction, credential secrecy, shared UI foundation use, and configured-host consumption.

## 1.13 — Image Input & WinForms Host Context

Detailed implementation plan: [1.13 — Image Input & WinForms Host Context](plan/Phase1/1.13.md)

Objective: establish image as the first V1 input boundary and define bounded WinForms host-context discovery.

Scope and non-goals: discovery is read-oriented and returns immutable metadata rather than raw control authority; registration, traversal, bounds, cancellation, redaction, and provenance remain explicit.

Verify: deterministic host-context discovery and image fixture handling, bounds/cancellation/ownership behavior, secret redaction, and Example Host verification.

## 1.14 — Dual Business-App Integration Contract

Detailed implementation plan: [1.14 — Dual Business-App Integration Contract](plan/Phase1/1.14.md)

Objective: establish neutral Core host-integration contracts and reusable WinForms implementations so real business apps can adopt Hive with minimal integration code.

Scope and non-goals: Hive owns neutral semantics and reusable bounded adapters; host business state, private types, business rules, and authorization remain host-owned and are never exposed through generic Hive contracts.

Verify: automatic/default control metadata, explicit overrides, parent/child surfaces, stable row identity, lookup semantics, bounded interaction, authorization boundaries, and the dual-host contract examples.

## 1.15 — Input Preparation & Routing

Detailed implementation plan: [1.15 — Input Preparation & Routing](plan/Phase1/1.15.md)

Objective: prepare supported V1 inputs and route each source through the capability needed to produce the common prepared-input boundary.

Scope and non-goals: image and spreadsheet paths converge on one prepared-input boundary; later structured extraction owns the next transformation and host business state is not mutated here.

Verify: image routing, workbook/worksheet/row handling, multiple WorkItems per submission, bounded processing, cancellation, failure isolation, and unsupported inputs.

## 1.16 — Provider / Model Capability Discovery & Operational Metadata

Detailed implementation plan: [1.16 — Provider / Model Capability Discovery & Operational Metadata](plan/Phase1/1.16.md)

Objective: establish provider/model discovery with provider-neutral capability evidence, availability, health, and operational metadata.

Scope and non-goals: discovery is observational; it does not silently fabricate capabilities, replace configured overrides, or create a separate model resource architecture.

Verify: discovery, model enumeration where supported, capability normalization, failure/cancellation, stale refresh, override behavior, and secret redaction.

## 1.16 UI — Provider Configuration, Discovery & Target Reconciliation

Detailed implementation plan: [1.16 UI — Provider Configuration, Discovery & Target Reconciliation](plan/Phase1/1.16-UI.md)

Objective: make provider/model discovery usable through the end-product Provider Settings experience while retaining Advanced administration of the underlying resource graph.

Scope and non-goals: normal setup hides unnecessary ProviderAccount/ExecutionTarget complexity; Advanced remains the path for multiple accounts, custom endpoints, manual targets, overrides, and troubleshooting.

Verify: Add Provider, Refresh/reconciliation, automatic target creation/retirement, failure preservation, administrator-managed target protection, Advanced navigation, and Example Host workflow.

## 1.16 Follow-Up — Complete Provider Model Metadata Discovery

Detailed implementation plan: [1.16 Follow-Up — Complete Provider Model Metadata Discovery](plan/Phase1/1.16-Follow-Up.md)

Objective: extend Phase 1.16 discovery to preserve the complete useful provider-reported model profile and expose it through Advanced Provider Configuration **Model Information**.

Scope and non-goals: the Provider → ProviderAccount → ExecutionTarget graph remains unchanged; no durable Model resource, second provider transport architecture, Agent target-selection redesign, all-model probing, or background discovery is introduced.

Verify: rich metadata normalization, absence semantics, modalities/capabilities, reasoning/options, limits/pricing, operational and bounded provider-specific evidence, failure/cancellation/security, reconciliation/override authority, structured UI, credential semantics, and Example Host verification.

## 1.17 — Structured Extraction & Validation

Detailed implementation plan: [1.17 — Structured Extraction & Validation](plan/Phase1/1.17.md)

Objective: turn the prepared-input boundary into a source-neutral typed StructuredCandidate boundary for later business-operation proposals, with bounded extraction, mapping, validation, provenance, and human-reviewable results.

Scope and non-goals: no host business mutation, no business-operation proposal/receipt, no parallel authorization subsystem, no per-row LLM mapping after an accepted mapping, and no direct host SQL access.

Verify: file/folder batching, semantic target-field contracts, one-time spreadsheet mapping, image extraction, candidate parsing/validation, parent/child structure, provenance, reviewable failures, and no host mutation.

## 1.18 — Durable Base-Agent Work State

Detailed implementation plan: [1.18 — Durable Base-Agent Work State](plan/Phase1/1.18.md)

Objective: make Base-Agent work mechanisms durable across runtime lifetimes.

Scope and non-goals: persist Objective, WorkItem, Question/Answer, memory/work-state, and delegation state as needed without introducing CognitiveAgent beliefs/goals/learning/Dream semantics.

Verify: restart persistence, ownership/scope isolation, stale/concurrent handling, durable transitions, cancellation/recovery, and no cross-runtime leakage.

## 1.19 — V1 Vector Retrieval Infrastructure

Detailed implementation plan: [1.19 — V1 Vector Retrieval Infrastructure](plan/Phase1/1.19.md)

Objective: establish bounded, replaceable vector storage and similarity retrieval for later Hive capabilities.

Scope and non-goals: V1 uses the `IVectorStore` boundary and SQL Server vector support; this is retrieval infrastructure, not the Phase 5 semantic-memory/learning system.

Verify: insertion/search, deterministic ordering/ties, ownership/scope, invalid vectors, result bounds, cancellation, persistence/reload, and isolation.

## 1.19A — Execution Target Preferences & Favorite Target Pool

Detailed implementation plan: [1.19 Follow-Up — Execution Target Preferences & Favorite Target Pool](plan/Phase1/1.19-Follow-Up.md)

Objective: establish a durable user/scope-aware favorite ExecutionTarget pool that acts only as an optional candidate filter for later target selection, with a simple Provider Settings Favorites preference CRUD surface. Adding a favorite uses a compact Provider → Account → Execution Target picker so Provider / Account filters reduce the target choices instead of forcing the user through a long global target list. Agent and other target-selection behavior remains unchanged in this slice and is owned by the relevant later phase.

Scope and non-goals: favorite target IDs are durable preferences, not a new ExecutionTarget resource type or selection mode. When the favorite pool is empty, existing target candidate behavior is unchanged; when it is non-empty, consumers may filter candidates to the favorite IDs and then invoke the existing capability-aware selection policy. Favorites do not alter target capability, lifecycle, automatic/manual ownership, or ranking semantics.

Verify: persistence and scope isolation; invalid/inaccessible target rejection; favorite filter empty/non-empty behavior; preservation across target retirement; Provider Settings Favorites CRUD UI; Add Favorite Provider / Account / Target filtering; focused Example Host behavior.

## 1.20 — V1 Workspace Foundation

Detailed implementation plan: [1.20 — V1 Workspace Foundation](plan/Phase1/1.20.md)

Objective: establish the real human-facing Workspace as the common interaction and operational surface over Hive.Management.

Scope and non-goals: Workspace adds direct LLM interaction, conversation/history, runtime and WorkItem visibility without creating Hive membership merely because Agents are visible.

Verify: normal Workspace entry, direct LLM conversation, explicit target selection, execution visibility, history/reload, WorkItem visibility, and authorization remaining outside the UI.

## 1.21 — V1 Agent Interaction & Application/Form Agents

Detailed implementation plan: [1.21 — V1 Agent Interaction & Application/Form Agents](plan/Phase1/1.21.md)

Objective: extend Workspace with explicit Agent interaction and application/form-scoped specialist Agents.

Scope and non-goals: Agent selection may be Auto or an exact target from the user's Favorite ExecutionTarget pool; Auto runs authoritative capability-aware planning only over favorites, explicit selection presents only favorites and remains pinned, and neither mode silently falls back to non-favorite targets when the favorite pool has no qualifying target.

Verify: Agent conversation/selection using the Favorite ExecutionTarget pool, Auto over favorites only, explicit favorite-only target selection, empty/no-qualifying-favorite failure without fallback, pinned-target failure behavior, application and form association, create/reuse/activation, mode switching, runtime isolation, bounded host association, and no implicit Hive/Swarm state.

## 1.22 — Governed Tools, Policy, Permissions & Human Intervention

Detailed implementation plan: [1.22 — Governed Tools, Policy, Permissions & Human Intervention](plan/Phase1/1.22.md)

Objective: establish the general governance boundary for Tools, policy, permissions, authorization, provenance/audit, and human intervention.

Scope and non-goals: authorization is deterministic and code-enforced; model output never grants permission, and business/application state remains host-owned.

Verify: unauthorized/authorized Tool execution, stale authorization/intervention rejection, Approve/Reject behavior, audit/provenance preservation, and fail-closed enforcement.

## 1.23 — Business-App Proposal & Governed Write

Detailed implementation plan: [1.23 — Business-App Proposal & Governed Write](plan/Phase1/1.23.md)

Objective: turn validated structured data into an authorized consequential host business operation through the approved host operation boundary.

Scope and non-goals: proposal, approval, operation identity, and host execution are distinct; the Tool/model does not become the owner of business semantics or authorization.

Verify: approval blocking/rejection, successful fake host execution, duplicate/stale approval protection, generated identity capture, idempotency/retry behavior, and unknown/partial dispositions.

## 1.24 — Business Operation Receipt & Reconciliation

Detailed implementation plan: [1.24 — Business Operation Receipt & Reconciliation](plan/Phase1/1.24.md)

Objective: durably record what happened after a consequential host operation and reconcile interrupted or unknown outcomes safely.

Scope and non-goals: the receipt and durable operation-attempt state remain the recovery anchor; reconciliation may reread authoritative host state rather than duplicating mutation.

Verify: parent/child receipt identity, interrupted/unknown reconciliation, idempotent retry, non-idempotent host protection, authorization, and provenance.

## 1.25 — Post-Write Review

Detailed implementation plan: [1.25 — Post-Write Review](plan/Phase1/1.25.md)

Objective: verify resulting host state against the intended candidate/proposal through a WorkItem-linked Review boundary.

Scope and non-goals: Review is separate from approval and does not rewrite the original candidate; it uses the operation receipt and authorized host-state rereads.

Verify: exact record location, correct/incorrect outcomes, discrepancy recording, candidate preservation, and authorization/provenance enforcement.

## 1.26 — MAF Sequential V1 Pipeline

Detailed implementation plan: [1.26 — MAF Sequential V1 Pipeline](plan/Phase1/1.26.md)

Objective: compose the complete single-Agent V1 business workflow through MAF Sequential orchestration.

Scope and non-goals: this is the full single-Agent pipeline before application-level concurrent Agent assignment; it reuses the established preparation, extraction, governance, operation, receipt, and review boundaries.

Verify: deterministic fake-host end-to-end success and rejected/failed branches.

## 1.27 — Multi-Agent Work Assignment & Concurrent Execution

Detailed implementation plan: [1.27 — Multi-Agent Work Assignment & Concurrent Execution](plan/Phase1/1.27.md)

Objective: allow multiple independent Agents to run the established V1 workflow concurrently within one host application.

Scope and non-goals: concurrency does not require persistent Hive membership or Swarm state; WorkItems, cancellation, failures, targets, and runtime state remain independently owned.

Verify: simultaneous Agents, different forms/contexts, independent WorkItems, failure/cancellation/selection isolation, no shared-runtime leakage, and no implicit Hive/Swarm creation.

## 1.28 — Full-Pipeline Crash/Resume

Detailed implementation plan: [1.28 — Full-Pipeline Crash/Resume](plan/Phase1/1.28.md)

Objective: prove crash/restart recovery across the complete V1 workflow and its durable operational state.

Scope and non-goals: resume or reconciliation must use durable state and authoritative host evidence rather than replaying terminal business mutations.

Verify: process termination at multiple checkpoints, safe restart, no duplicate terminal mutation, terminal WorkItem protection, failure isolation, receipt/Review persistence, and independent recovery.

## 1.29 — Resource Inventory & Runtime Diagnostics

Detailed implementation plan: [1.29 — Resource Inventory & Runtime Diagnostics](plan/Phase1/1.29.md)

Objective: provide the V1 operational surface for authoritative resource inventory and runtime/execution diagnostics.

Scope and non-goals: inventory is inspection-oriented, bounded, safe, and correlation-aware; it never exposes secrets or mutates state unless an explicitly authorized action exists.

Verify: authoritative inventory, ownership/scope, runtime inspection, useful typed diagnostics, secret redaction, and durable-state reload.

## 1.30 — Metrics, Budget Cap & OpenTelemetry

Detailed implementation plan: [1.30 — Metrics, Budget Cap & OpenTelemetry](plan/Phase1/1.30.md)

Objective: establish V1 resource-control and observability boundaries for metrics, budgets, quotas, cancellation, and OpenTelemetry.

Scope and non-goals: telemetry and budgets observe/control execution without leaking secrets or replacing authoritative lifecycle and authorization state.

Verify: hard budget stopping, provider/model metrics, correlation, token/cost tracking, cancellation, OpenTelemetry traces/metrics, quota handling, and secret redaction.

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

## 2.8 — Hive Coordination Workspace & Swarm Extensions
Objective: extend the V1 Workspace with persistent Hive organization and collective coordination capabilities.

Scope:
- Hive organization/topology;
- Hive membership presentation;
- persistent Hive/Agent relationships;
- active Swarm visibility;
- Hive-level coordination;
- collective work context;
- Workspace representation of persistent Hive membership;
- Agentic behavior that specifically depends on Hive membership or Swarm state.

Basic LLM mode, basic Agent mode, ordinary multi-Agent application work, and single-application Agent assignment remain V1 capabilities.

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
Replaceable strategy contract capable of deterministic decisions and explicit no-model paths, including adaptive interpretation of evaluated outcomes, contextual Risk/Fear/Confidence, reconsideration, and selection among direct execution, Questions, Hive assistance, Dreams, decomposition, and previously governed strategy/resource adaptations. This slice defines the extension point for learned deterministic shortcuts but does not implement Learning Candidate promotion from later Phase 5 work.

Verify: strategy decisions can consume cognitive evidence and Risk/Fear/Confidence without bypassing authorization, capability, scope, budget, or execution planning; any promoted adaptation is consumed only through its owning governed contract.

## 4.3 — Reasoning Requirement
Provider-neutral reasoning requirements kept separate from concrete Execution Target planning.

## 4.4 — Persistent Cognitive State
Beliefs, bounded workspace/attention, goals, intentions, plans, methods, self-model, contextual Risk/Fear/Confidence state, impasses, and revision-safe transitions. Persistent state is independent of whether a runtime incarnation is currently active.

Risk, Fear, and Confidence are evidence-backed cognitive state rather than authorization or policy state. They may alter strategy and escalation behavior but never override authoritative enforcement.

Verify: versioned cognitive-state transitions preserve context/provenance for Risk/Fear/Confidence and do not permit cognitive state to bypass deterministic safety, authorization, capability, scope, or budget checks.

## 4.5 — Experience, Outcome Evaluation & Cognitive Event History
Bounded experience capture, provenance, expected-versus-observed results, outcome evaluation, attribution/credit context, and replayable supported transitions. OutcomeEvaluation is a first-class cognitive contract/process that establishes a provenance-bearing outcome classification from the observed evidence and applicable success criteria. Success, Mistake, Partial, and Unknown are first-class cognitive outcome concepts associated with that evaluation; specialized processing may consume them without making them aliases for execution states. Outcome correctness remains distinct from method/strategy quality and causal attribution. Actual observations/experiences remain distinguishable from simulated, predicted, counterfactual, human-corrected, and external evidence.

Define mutually exclusive outcome semantics for one evaluation:
- Success = all applicable success criteria were actually satisfied;
- Partial = some but not all applicable criteria were satisfied and the result is incomplete rather than wholly incorrect;
- Mistake = the result is known to be wrong relative to the intended objective or success criteria and is not better classified as Partial;
- Unknown = available evidence cannot establish the substantive result.

Technical execution failure is not automatically a Mistake. Technical execution success is not automatically a cognitive Success. Attribution of the failure or success remains a separate evidence problem and may involve the Agent, tools, specialists, the environment, or other factors.

Verify: outcome evaluation preserves evidence, attribution, and the distinction between outcome correctness and method/strategy quality; actual/simulated evidence remain distinguishable; technical failure/success cannot be silently mapped to cognitive learning labels; partial and unresolved outcomes remain representable.

## 4.6 — Death / Wake / Reincarnation Lifecycle
Define death as complete termination of the current runtime/incarnation, preserve Agent identity and cognitive state, support inactive periods with no live runtime, and explicitly reconstruct a new runtime from durable state when the Agent wakes.

## 4.7 — Postmortem & Dream Processing
Define bounded postmortem processing plus a first-class Dream subsystem that can inspect history, generate hypothetical alternatives, run multiple simulations in parallel, compare predicted outcomes, and produce proposed cognitive updates without requiring the Agent runtime to remain alive. Dream purposes have explicit semantics and provenance; purpose-specific processors may share the core Dream contract or be separately replaceable when scheduling, lifecycle, or resource boundaries justify that split. Proposed changes are not authoritative state transitions; reconciliation and the owning resource/governance boundary decide whether they are accepted.

Dream purposes include:
- Recovery — explore alternatives after a Mistake or unresolved outcome;
- Optimization — search for cheaper, faster, safer, simpler, or more deterministic ways to reproduce a Success;
- Nightmare / Stress-Test — actively search for plausible conditions under which an apparently successful method, plan, assumption, or strategy would fail;
- Reconsideration — revisit prior decisions in light of later evidence;
- Preparation — rehearse plausible future scenarios.

Dream processing is governed by applicable authorization, provider/model quota, token/cost budget, time budget, concurrency/parallelism limits, retrieval/work limits, and cancellation.

Dream evidence remains simulated/predicted evidence and cannot become actual experience. Counterfactual conclusions such as Regret must remain distinguishable from information actually available at the time of the original decision.

After a Mistake, Cognitive Strategy may retry with a revised method directly or may first use Questions, Hive assistance, or a Recovery Dream when the expected benefit justifies the additional work. After a Success, it may use Optimization and Nightmare/Stress-Test Dreams before adopting a broader lesson.

Verify: evaluated Mistake → Recovery proposal or bounded revised retry; evaluated Success → Optimization proposal; evaluated Success → Nightmare/Stress-Test proposal; Dream results remain simulated; Dream processing works while the Agent runtime is inactive; an inactive-runtime Dream requires an already authorized request or durable policy trigger; budgets/cancellation/concurrency are enforced.

## 4.8 — Questions
Define first-class Questions with structured context, specialty, provenance, answer type, evidence requirements, status, and confidence/uncertainty where applicable. Support specialty-specific questions so different Agents can investigate different aspects of the same user objective.

Questions may be selected or prioritized when outcome attribution is uncertain, risk remains high, evidence conflicts, or a missing fact materially changes the choice among competing strategies.

Verify: unresolved Mistake/Success attribution can result in an evidence-seeking Question; redundant Questions remain avoidable when sufficient evidence already exists.

## 4.9 — Cognitive State Reconciliation
Integrate human edits, actual experience, evaluated outcomes, Mistake/Success interpretations, Dream results, Question answers, beliefs, goals, plans, Risk/Fear/Confidence state, and other candidate updates through versioning, provenance, authorization, validation, and concurrency boundaries before the next wake/reincarnation.

Conflicting evidence must remain attributable. Reconciliation may retain multiple hypotheses, uncertainty, or an unresolved Question instead of inventing a single authoritative explanation.

Verify: concurrent human/Dream updates do not lose evidence; actual experience cannot be overwritten by simulated evidence; stale candidate updates are rejected or reconciled explicitly.

---

# Phase 5 — Cognitive Resources

## 5.1 — Memory Resource Families
Working, episodic, semantic, procedural, and future extensible families with explicit scope/ownership.

## 5.2 — Knowledge / Wiki
Versioned, permissioned knowledge resources and managed Wiki source.

## 5.3 — Skills
Versioned reusable procedures, dependencies, constraints, provenance, and assignments.

## 5.4 — Learning Candidates & Governance
Transform evaluated cognitive evidence into governed Learning Candidates through a first-class learning/governance boundary. The implementation may use a dedicated learning component or shared cognitive-resource infrastructure, but promotion remains explicit and governed.

Evidence sources include:
- evaluated Success outcomes;
- evaluated Mistake outcomes;
- Partial or mixed outcomes;
- repeated outcome patterns;
- human corrections;
- Question answers;
- Recovery Dreams;
- Optimization Dreams;
- Nightmare/Stress-Test Dreams.

Each candidate preserves:
- evidence type and actual/simulated origin;
- provenance and attribution/credit context;
- support/confidence;
- applicability conditions;
- the proposed target of adaptation (Skill, Method, strategy/routing rule, safeguard, memory/knowledge update, or other owned cognitive resource);
- conditions for invalidation, revision, or retirement;
- validation status.

Promotion may change an appropriate Skill, method, applicability rule, memory/knowledge representation, or Cognitive Strategy routing according to explicit ownership rules.

A candidate may learn that a deterministic procedure is preferable to another model call for a known class of situations, but promotion must remain governed. The promoted shortcut must identify its applicability boundary and remain revocable/revisable when later evidence invalidates or narrows it. No direct authoritative mutation from model output or Dream output.

Verify: positive, negative, partial, mixed, human-corrected, and simulated evidence; conflicting candidates; applicability boundaries; insufficient support; promotion/rejection concurrency; explicit adaptation target; later invalidation/revision; and strategy consumption of an already-governed adaptation.

---

# Phase 6 — CognitiveHive : Hive

## 6.1 — Collective Cognitive State
Hive-level collective state is distinct from each member's own Agent/CognitiveAgent state.

## 6.2 — Collective Strategy
Coordinate planning/reasoning across members without moving member cognition into the Hive itself.

## 6.3 — Collective Questions & Specialty Routing
Route Questions by Agent specialty, avoid semantically duplicate work where evidence already exists, and allow each Agent to retain its own Questions and answers.

## 6.4 — Cross-Agent Evidence & Synthesis
Combine attributable answers, experiences, evaluated outcomes, Mistakes, Successes, Dreams, observations, and other evidence into collective reasoning without erasing individual provenance or actual-versus-simulated evidence status.

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

## 8.6 — Lightweight / Embedded Hive Deployment Profile
Objective: provide an optional local/embedded persistence deployment for users who should not need to install or operate a separate SQL Server instance, while preserving the same Hive resource model, Management contracts, event/snapshot/outbox semantics, and authorization boundaries.

Scope:
- select and document a mature embedded persistence technology rather than creating a database engine without a measured requirement;
- reuse the existing Hive persistence/resource contracts instead of maintaining a second logical schema/model;
- define which capabilities the embedded backend supports, including vector storage/search;
- keep SQL Server as the server-oriented V1 persistence implementation;
- make backend selection explicit and configuration-driven;
- preserve migration/version/concurrency/security semantics across supported backends;
- provide a clear upgrade/export path from local/embedded deployment to the server-oriented persistence profile when required.

This slice is a deployment/storage portability capability, not permission to fork Hive's domain model or introduce a separate vector database.

Verify: clean local install, restart/persistence durability, migrations/upgrades, concurrency, crash/recovery, secret handling, supported vector-search behavior where available, explicit unsupported-capability reporting, and configuration migration between supported deployment profiles where that contract is provided.

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
