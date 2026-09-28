# Hive Architecture — Execution, Provider, Resource, and Durability Boundaries



This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the detailed execution, provider, resource, intervention, and durable-event boundaries.



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
- cognitive lifecycle, outcome evaluation, Mistake/Success semantics, Risk/Fear/Confidence state, Dream, and Question semantics for the cognitive generations;
- learning-candidate and cognitive-state reconciliation semantics for the cognitive generations;
- host application integration;
- WinForms management facade and configuration surface, including first-class Provider and Persistence configuration;
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
| Execution model | Ephemeral execution + durable Agent/Hive state + transactional outbox | Execution objects and Agent incarnations may end; durable state survives; a WorkItem is the durable unit of user-visible work and a submission may produce one or multiple WorkItems |
| Persistence | SQL Server; LocalDB for development | Hive database is isolated from host business data |
| Vector storage | SQL Server `VECTOR` / `VECTOR_DISTANCE` behind `IVectorStore` | V1 bounded storage/search infrastructure; no separate vector database is required for V1 |
| Provider adapter | One shared OpenAI-compatible adapter | Compatible providers are configurations, not new adapter implementations |
| Provider | Vendor/service integration | Transport identity |
| ProviderAccount | Credential/account/project under Provider | Credential/account boundary |
| ExecutionTarget | Provider + ProviderAccount + endpoint + model/deployment | Concrete capability-bearing target |
| Capability state | `Supported` / `Unsupported` / `Unknown` | Unknown never silently becomes supported |
| Capability requirement | `Required` / `Preferred` / `Optional` / `Forbidden` | Independent of provider identity |
| Selection mode | `Auto` / `Preferred` / `Fixed` | Preference is separate from hard pinning |
| Cost policy | `FreeOnly` / `FreePreferred` / `NoRestriction` | Cost is separate from capability |
| Secrets | Encrypted at rest through `ISecretStore`; redacted elsewhere | No unnecessary external vault architecture |

`ProviderAccount` stores only an optional `SecretReference` for provider credential material. Provider connection tests resolve that reference through `Hive.Management` and `ISecretStore`; provider credentials are never stored in ProviderAccount fields, configuration files, diagnostics, or provider-test output.

The Phase 1.12 Settings boundary uses one typed persistence configuration contract containing SQL Server endpoint/port, database identity, authentication mode, non-secret login metadata, optional Secret Store credential reference, connection-security flags, database-initialization policy, and command timeout. `Hive.Management` exposes save/load and connection-test operations. The connection-test boundary must inspect server/database/schema state without creating the database or applying migrations. WinForms settings pages consume only these Management operations.

Current V1 provider configurations include compatible hosted/local targets such as Groq, OpenRouter, Cloudflare, Cerebras, NVIDIA, Google, and local OpenAI-compatible servers. The adapter contract remains vendor-neutral; adding another compatible provider should normally require configuration, not another transport implementation.

Deferred until a measured requirement exists: Temporal, Dapr, PostgreSQL/pgvector, Elasticsearch/OpenSearch, Akka.NET, Orleans, DiskANN, a custom Hive workflow engine, a separate external secrets-vault architecture, and the selection of a future embedded/local persistence backend. V1 does not require a separate vector database. Building a custom database engine is not assumed; a future embedded mode should prefer a mature embedded persistence technology behind the existing Hive persistence/resource contracts unless a measured requirement proves that insufficient.

---



## Phase 1.16 Provider / Model Discovery Boundary

Provider/model discovery is a provider-platform operation exposed through Hive.Management. Discovery is operational evidence; it is not configuration, authorization, or a second orchestration path.

The durable resource model remains:

```text
Provider
   ↓
ProviderAccount
   ↓
ExecutionTarget
```

ProviderAccount remains the credential/account boundary. ExecutionTarget remains the concrete capability-bearing execution resource. Discovery does not require those concepts to be collapsed.

The existing `IProviderCapabilityDiscovery` contract is target-aware because the completed Phase 1.16 implementation validates Provider, ProviderAccount, and endpoint identity together. Its cache is already keyed by Provider + ProviderAccount + endpoint rather than by target identity. The product-level provider onboarding/reconciliation boundary must preserve that endpoint/account scope. A normal Provider setup flow must not create a fake or placeholder persisted ExecutionTarget merely to obtain discovery data. Where the discovery contract is extended for onboarding, its natural input is the authorized Provider + ProviderAccount + discovery endpoint context, with the existing target-aware contract retained or adapted where the concrete target editor still needs it.

The first implementation is a shared OpenAI-compatible discovery implementation using the existing OpenAI-compatible transport to enumerate the provider model catalog. Compatible vendors remain configurations of that transport; adding another OpenAI-compatible vendor does not create another provider transport implementation.

Discovery remains conservative and ephemeral:

1. a successful fresh observation may be cached and consumed by Management/UI/reconciliation;
2. a failed, cancelled, stale, unsupported, malformed, rate-limited, or authentication-failing discovery never means zero models;
3. a failed refresh leaves the last successful observation intact;
4. discovered capability information never silently rewrites configured ExecutionTarget capability overrides;
5. stale discovery contributes no effective discovered capability;
6. provider/model availability and health remain operational metadata, not capability grants.

The existing Phase 1.16 cache/freshness/concurrency/security rules remain authoritative, including endpoint identity checks, forced-refresh semantics, credential-cache invalidation, bounded retention, cancellation, typed failures, and secret/raw-response redaction.

### Provider Configuration and Automatic Target Reconciliation

The normal Settings product experience is provider-centric rather than resource-graph-centric.

The built-in provider catalog is static application metadata describing supported provider choices, transport/authentication requirements, default discovery endpoint behavior, and any provider-specific onboarding requirements. A catalog entry is not itself a persisted Provider resource.

Normal onboarding is:

```text
Add Provider
    ↓
select built-in provider
    ↓
supply required credential material
    ↓
Hive.Management creates/enables the Provider
    ↓
creates the default ProviderAccount
    ↓
stores credential material through ISecretStore
    ↓
discovery
    ↓
target reconciliation
```

The credential is never persisted in the Provider record or returned to the Settings UI. For the normal built-in hosted-provider workflow, Add collects the API key and normal Edit replaces that API key; Provider catalog identity and transport configuration are not edited through the normal surface. Authentication requirements remain catalog-defined so a future built-in provider with a different simple credential shape can use the appropriate protected credential input rather than forcing an incorrect API-key model.

The durable ProviderAccount resource still exists even though normal users do not manage it directly. This preserves the ability to support multiple accounts/projects/credentials through Advanced Configuration without creating a second configuration model.

Successful provider configuration and external discovery are separate failure boundaries. Durable Provider/ProviderAccount/credential state may be committed before network discovery begins; network discovery must not run inside a database transaction. If discovery fails after configuration is saved, Hive preserves the configuration and reports the operational failure, allowing Refresh/retry. The same rule applies to credential replacement: invalidate affected discovery evidence, then perform the fresh discovery/reconciliation outside the credential transaction.

Successful discovery is consumed by a dedicated reconciliation boundary:

```text
Provider + ProviderAccount + endpoint
            ↓
   ProviderDiscoverySnapshot
            ↓
   ExecutionTarget Reconciler
            ↓
automatic ExecutionTargets
```

Automatic target reconciliation is durable, idempotent, and concurrency-safe. Each ExecutionTarget has an explicit durable `ExecutionTargetManagementMode` with at least `Automatic` and `Manual` values. `Automatic` means Hive owns the target's model/catalog synchronization; `Manual` means administrator configuration owns it and discovery/reconciliation must not overwrite it. Management mode is semantic state, not UI metadata, and must never be inferred from a display name, metadata convention, or edit history.

An automatic target represents the stable execution identity of one discovered model/deployment under one ProviderAccount and endpoint. Reconciliation uses a stable identity/reconciliation key based on the provider account, endpoint, and model/deployment identity so a rediscovered model reuses its existing automatic target where possible. Display names and diagnostics are not identity.

Example:

```text
discovered: A, B, C
targets:    A, B, C

later discovered: A, C, D
result:            A, B(retired), C, D
```

A model that disappears from a successful fresh enumeration causes its automatic target to transition to the existing retired lifecycle rather than being physically deleted. If the same model later returns under the same reconciliation identity, Hive may reactivate/reuse the existing automatic target after normal dependency and lifecycle validation. Automatic reconciliation must never erase durable identity merely because provider enumeration changed.

A successfully enumerated model that is temporarily reported unavailable or unhealthy is not treated as a missing model. Enumeration determines catalog membership; operational availability/health remains separate metadata. Only the explicit lifecycle/reconciliation rules may retire an automatic target.

Administrator-managed targets are outside automatic reconciliation ownership. Discovery may report evidence relevant to them, but reconciliation must not rewrite their endpoint, model/deployment, capability overrides, or lifecycle. Returning a target to automatic management is an explicit Management operation; after that operation, the target again becomes eligible for reconciliation. The implementation must preserve enough stable identity/reconciliation information to make that transition deterministic.

Automatic and manual targets may coexist when their concrete execution identities differ. A manual administrator target must not block discovery of a separate automatic target merely because its model name matches. Exact resource identity is determined by the complete target identity, not model display text alone.

Reconciliation is triggered by successful discovery events such as initial provider configuration, explicit Provider Settings Refresh, and credential changes. Periodic/background refresh is an operational scheduling concern and must not be silently invented by the Settings UI. Runtime auto-selection must not rely on an assumption that the catalog is permanently fresh; stale/unknown operational state must remain explicit until an authorized refresh/reconciliation path obtains current evidence.

All reconciliation writes pass through Hive.Management and the existing persistence/resource lifecycle/concurrency boundaries. WinForms does not create ProviderAccount/ExecutionTarget records directly, call provider transport, store secrets, or implement reconciliation logic.

### Advanced Configuration Boundary

Advanced Configuration is a single generalized administrative surface:

```text
Advanced Configuration
├── Providers
├── Accounts / Credentials
└── Execution Targets
```

It is not provider-specific. The Advanced entry point may optionally open with a selected provider/account filter for convenience, but the underlying pages and contracts remain generalized.

Advanced Configuration exists for cases that the normal Provider onboarding intentionally hides:

- multiple ProviderAccounts or credentials;
- custom or alternate endpoints;
- local and self-hosted OpenAI-compatible servers;
- manually configured models/deployments;
- explicit capability overrides;
- unusual provider/account/target relationships;
- administrative lifecycle management and troubleshooting.

The normal Settings page therefore represents the user-facing service configuration, while Advanced Configuration represents the underlying resource administration. They are two presentation levels over the same authoritative Management contracts, not two competing configuration systems.

### Capability and Selection Boundary

The effective capability model remains:

```text
configured ExecutionTarget capabilities
              +
current discovery evidence
              ↓
effective capability view
              ↓
authoritative ExecutionTargetSelector
```

Configured capability entries remain authoritative; discovery fills only missing evidence and never mutates durable overrides.

The existing `ExecutionTargetSelector` remains the single capability-selection policy boundary. It supports the existing internal Auto/Preferred/Fixed selection contract and deterministic tie-breaking. The normal Provider Settings UI does not expose target-selection policy.

Agent configuration is a separate Agent-owned concern. The existing AgentDefinition → ExecutionTarget relationship remains the durable foundation established earlier. A later Agent interaction/configuration slice owns the user-facing distinction between `Auto` and an exact selected execution target/model, including the rule that a fixed selection must not silently switch to another target when the pinned target becomes unusable.

Operational availability/health remains distinct from capability. Before an Agent Auto selection is executed, the execution-planning boundary must exclude targets that are explicitly ineligible under the current operational state; it must not reinterpret health or availability as a capability grant. The exact operational-eligibility policy belongs to execution planning, not Provider Settings.

No V1 workflow, MAF orchestration, business-operation write, Tool authorization, Review, cognition, or future-phase behavior is part of the provider configuration/discovery/reconciliation boundary.
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

Resource examples include Provider, ProviderAccount, ExecutionTarget, AgentDefinition, HiveDefinition, Workspace, WorkItem, Question, Memory, Knowledge, Wiki, Skill, LearningCandidate, CognitiveState, and Review resources. CognitiveAgent evidence additionally includes first-class Experience and OutcomeEvaluation semantics, with Mistake, Success, Partial, Unknown, Regret, Risk, Fear, and Confidence represented according to their owning cognitive contracts. A concept does not have to become a generic Resource merely to be first-class; where an independent lifecycle, persistence, scheduling, or replacement boundary exists, a dedicated resource/component/event stream may be used.

Assignments are references/policies, not copies of the assigned resource.

Unknown future resource types remain representable through the generic inventory model.

---



### Internal persistence implementation separation

The public provider persistence contract remains grouped by the Provider → ProviderAccount → ExecutionTarget resource family through `IProviderResourceStore`. This grouping is a stable application-facing persistence contract and is not itself the concrete implementation boundary.

The SQL implementation must not concentrate all three resource implementations in one concrete class. `SqlProviderResourceStore` is a composition/facade over resource-specific internal stores:

```
IProviderResourceStore
        ↓
SqlProviderResourceStore
   ┌────┴────────┬──────────────────┐
   ↓             ↓                  ↓
Provider      ProviderAccount   ExecutionTarget
store         store             store
```

Each internal store owns the SQL statements, resource-specific write logic, resource-specific validation that belongs to persistence, lifecycle transitions, and optimistic-concurrency mechanics for its resource. A small shared `SqlProviderResourceReader` owns cross-resource reads and row materialization needed to validate relationships without duplicating SQL or coupling the resource stores directly to one another. Shared connection creation, command construction, access-parameter construction, common serialization, and structured SQL/error translation remain shared infrastructure when those mechanics are genuinely identical.

This is an internal implementation separation. It must preserve the existing public `IProviderResourceStore` contract, resource ownership/scope enforcement, transactional semantics, cancellation behavior, error classification, deterministic ordering, and dependency direction. The composition class must not regain resource-specific SQL or domain logic merely to make the split appear superficial. Cross-resource relationship reads must remain explicit through the shared reader rather than recreating a monolithic three-resource store.

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

LLM mode is a V1 Workspace interaction path where the user chooses the execution target. Agent mode selects an Agent, which uses this planner on behalf of that Agent; after Phase 2 adds persistent Hive/Swarm coordination, Hive-level Agentic behavior extends the same planner boundary. Any explicitly pinned Agent target is the exact durable `ExecutionTarget` identity, not a model-name string.
```

A required capability must be explicitly supported. Unknown capability evidence does not qualify for a hard requirement.

Operational state remains separate from configured capability:

- quota;
- rate limits;
- health;
- availability;
- capacity;
- cost.

Phase 1.16 adds provider/model discovery and refresh semantics for these metadata dimensions where the provider can report them. Discovery evidence is distinct from explicitly configured capability overrides; unsupported discovery does not fabricate a capability.

Planner output must be explainable without exposing credentials.

Running executions use immutable effective configuration snapshots.

Terminal execution state cannot be overwritten by a late provider result.

### Cognitive evidence and adaptation boundary

For CognitiveAgent generations, execution produces evidence that is later interpreted by the cognitive layer.

```
Execution / external observation
          ↓
Experience
          ↓
OutcomeEvaluation
          ├── Success
          ├── Mistake
          ├── Partial
          └── Unknown
          ↓
Cognitive Strategy
          ├── risk/confidence revision
          ├── Question
          ├── Dream
          ├── Hive/specialist escalation
          └── learning evidence / proposal
                            ↓
                    governed Learning Candidate
```

The execution boundary owns what the execution actually returned and its technical lifecycle. The cognitive OutcomeEvaluation boundary decides whether that evidence means the intended objective was achieved, what likely contributed to the result, and what adaptation should be considered. OutcomeEvaluation, Mistake, Success, Risk, Fear, Confidence, Dream, and Learning remain first-class cognitive semantics even when they share persistence or processing infrastructure.

Outcome evaluation must remain attributable to the objective/plan/method/decision and preserve expected-versus-observed evidence. It should distinguish outcome correctness from method/strategy quality and causal attribution where the evidence permits. Technical failures must not be silently converted into Mistakes, and technical successes must not be silently converted into durable Success lessons.

Risk/Fear/Confidence may affect strategy selection and escalation, but the Execution Planner and authorization boundaries remain authoritative for capability, target, policy, scope, budget, and permission decisions.

Dreams and counterfactuals are evidence with a different epistemic status from actual experience. Their predicted outcomes may support a Learning Candidate but must never be replayed as observed events. The Dream request itself must already be authorized through the applicable policy/management boundary; Dream processing does not acquire new authority because the Agent runtime is inactive.

Learning Candidates are persisted through their owning cognitive-resource/governance boundary; the execution store does not become a cognitive-learning engine. A future deterministic shortcut may be persisted as a governed Skill, Method, strategy rule, routing rule, or equivalent cognitive resource, but the promotion boundary must retain provenance/applicability and support later invalidation, revision, or retirement.

---



## 8. Human Intervention

### V1 WorkItem semantics

For V1, a **WorkItem is the durable unit of user-visible work** and represents one logical business operation when a business operation is required. A single input submission may produce one or multiple independent WorkItems. A related submission or batch is an operational grouping of WorkItems, not a replacement for their individual lifecycle, authorization, or correctness boundaries.

A WorkItem may contain a parent business record and child-row collection when the host treats those changes as one logical operation. A WorkItem may require multiple executions or steps. A runtime incarnation is not inherently bound one-to-one to a WorkItem; a runtime may process multiple WorkItems according to its execution policy. Execution remains the concrete execution/lifecycle unit.

An active Swarm may be represented as the set of Hive members participating in a WorkItem or related Question. The Swarm is derived/session state, not a persistent resource.

### Approval versus Review

Approval and post-write Review are different lifecycle concepts.

**Approval** answers:

```
Should Hive perform the proposed consequential operation?
```

**Review** answers:

```
Did the resulting host/application state contain the intended data correctly?
```

Approval therefore occurs before the governed business-app write when policy requires it. Review occurs after a write when review policy requires verification.

Approval is one intervention action, not the entire architecture.

The broader intervention contract may eventually support:

```
Inspect / Approve / Reject / Pause / Resume / Cancel /
Retire / Shutdown / Redirect / Defer / RequestInformation
```

For V1, **Approve / Reject** on the proposed business-app write is the required pre-write intervention. A separate first-class **Review** contract governs post-write correctness.

Intervention never bypasses authorization, capability, budget, or host validation.

### Business-operation receipt

A consequential host operation that crosses the host boundary produces a durable business-operation receipt/attempt record. For non-transactional host calls, the logical operation identity and initial attempt state are persisted before submission so an interruption cannot erase the only evidence that a mutation may have been in flight.

The receipt records, as available:

- WorkItem and logical operation identity;
- host/application and adapter identity;
- operation type;
- parent record identity;
- affected child record identities;
- host correlation/transaction identifier;
- completion/result state;
- host revision/concurrency evidence.

The receipt is attribution and recovery state, not a copy of the host application's database.

A generated host ID must be captured when the host can provide it. A hidden UI primary-key field is a valid host implementation mechanism, but visibility is never the source of identity semantics.

An unknown write outcome after interruption must not automatically trigger a duplicate write. Recovery first loads the durable attempt/receipt by logical operation identity, uses any host-side idempotency or correlation evidence, and rereads authoritative host state as needed to establish whether the operation already took effect. A retry is safe only after that reconciliation boundary permits it.

### First-class V1 Review

Review is a provenance-bearing, WorkItem-linked durable object. It may be backed by a dedicated generic resource contract and persistence stream while retaining the host business record as the source of truth.

Review supports:

```
Human
Automated
Hybrid
```

The minimum V1 requirement is human review when review policy requires it. Automated verification may precede human review and may resolve a low-risk operation without human intervention when policy explicitly permits that behavior.

The normal verification flow is:

```
intended candidate/proposed data
        +
BusinessOperationReceipt
        ↓
authorized host read
        ↓
bounded comparison
        ↓
Review outcome
```

Review is host-operation correctness evidence, not a synonym for CognitiveAgent Success/Mistake. A Review may later contribute evidence to cognitive OutcomeEvaluation through an explicit provenance-bearing reconciliation step, including when a human review corrects the observed result.

Minimum outcome states are:

```
PendingReview
VerifiedCorrect
VerifiedIncorrect
```

The contract may also represent unresolved operational states such as:

```
VerificationUnavailable
Inconclusive
```

A review records discrepancies rather than silently rewriting the original candidate. Hive should persist only the minimum bounded evidence required to explain and audit the review; it must not become an uncontrolled mirror of host business data.

## 9. Events, Snapshots, and Transactional Outbox

Hive uses append-oriented event history with durable snapshots as recovery aids.

Every durable event carries an explicit event type and **payload schema version**. Event readers/upcasters must be able to translate supported older payload versions to the current contract without rewriting historical events. Database schema versioning and event-payload versioning are separate concerns.

Phase 1.7 establishes the durable persistence primitive inside `Hive.Persistence`:

- an event stream is identified by an existing `ResourceReference`;
- each stream has an explicit positive sequence/version;
- events remain immutable and append-only;
- snapshots store the latest reconstructed state for a stream and its snapshot payload schema version;
- an outbox row is created from the same event that caused the durable state change;
- the event, optional snapshot replacement, and corresponding outbox row commit in one SQL transaction;
- an optimistic expected-version check prevents two writers from silently appending the same stream version;
- unique event and stream-version constraints protect duplicate writes at the database boundary.

The durable event store remains generic. It does not own Agent execution, workflow scheduling, polling, provider transport, or management policy.

The raw event persistence port and SQL implementation are infrastructure-internal. `IEventPersistenceStore` and `SqlEventPersistenceStore` are not public application contracts, so callers cannot bypass Hive.Management authorization by operating arbitrary event streams directly. Trusted Hive composition uses the opaque `HiveEventPersistenceComposition` created by `HiveEventPersistence.CreateSql(...)`; that handle exposes only the bounded public operations needed for trusted composition, such as creation of an `EventOutboxPoller`.

Snapshot reconstruction is a separate deterministic contract over `EventEnvelope` values. A registered reducer handles a known event type and current payload schema; older supported payloads are normalized through the existing Core upcaster registry before reduction. Historical events are never rewritten during upcasting or folding.

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
- an in-flight delivery keeps its outbox lease renewed until completion or cancellation;
- loss of lease renewal cancels the delivery token and leaves the outbox item recoverable;
- replay is deterministic where the contract requires it;
- terminal execution state cannot be overwritten by late provider completion.

The outbox is not a general distributed message broker.

---