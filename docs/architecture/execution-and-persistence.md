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

The Management boundary also exposes an endpoint-scoped read-only discovery operation for administrative Model Information. It accepts the authorized Provider + ProviderAccount + endpoint context, uses the same discovery cache/freshness and Secret Store path as target-scoped discovery, and may use a non-persisted in-memory discovery probe only at the provider-adapter invocation boundary. The probe is never written as an ExecutionTarget and is never exposed as durable configuration. The public result remains the same ProviderDiscoverySnapshot contract, so Advanced Model Information does not introduce a parallel model catalog or persistence model.

The normalized model profile carried by ProviderDiscoverySnapshot is intentionally observational. Known Hive capability identities remain distinct from provider-specific evidence; configured ExecutionTarget capability entries remain authoritative when present. Model-scoped limits are not quotas or rate limits, and missing pricing is not evidence of a free model. Provider-specific extension evidence is bounded, sanitized, non-secret, and informational only.

The existing `IProviderCapabilityDiscovery` contract is target-aware because the completed Phase 1.16 implementation validates Provider, ProviderAccount, and endpoint identity together. Its cache is already keyed by Provider + ProviderAccount + endpoint rather than by target identity. The product-level provider onboarding/reconciliation boundary must preserve that endpoint/account scope. A normal Provider setup flow must not create a fake or placeholder persisted ExecutionTarget merely to obtain discovery data. Where the discovery contract is extended for onboarding, its natural input is the authorized Provider + ProviderAccount + discovery endpoint context, with the existing target-aware contract retained or adapted where the concrete target editor still needs it.

The first implementation is a shared OpenAI-compatible discovery implementation using the existing OpenAI-compatible transport to enumerate the provider model catalog. Compatible vendors remain configurations of that transport; adding another OpenAI-compatible vendor does not create another provider transport implementation.

#### Complete model metadata profile

Every successfully enumerated model is a valid discovery result even when the provider does not report normalized capability evidence. Model discovery is therefore a **complete useful provider-reported metadata ingestion boundary within Hive's bounded and security-controlled contract**, not a bounded capability-list probe.

The normalized model metadata profile covers all applicable fields from the following semantic areas that the provider actually reports:

1. **Identity and descriptive metadata**
   - model/deployment identifier;
   - ownership/provider model attribution where reported;
   - family/type/category, version, creation metadata, and other descriptive model information where reported.

2. **Input modalities**
   - all provider-reported input modalities, including text, image, audio, video, and other provider-defined modalities.

3. **Output modalities**
   - all provider-reported output modalities, including text, image, video, embeddings, and other provider-defined outputs.

4. **Capabilities**
   - tool calling;
   - structured output;
   - reasoning;
   - thinking;
   - provider-reported effort/level options and defaults where applicable;
   - other provider-reported machine-readable capabilities.

   Reasoning and thinking are separate semantic concepts. Thinking configuration may include levels/options/defaults when the provider exposes them.

5. **Model-scoped limits**
   - context window;
   - maximum input tokens;
   - maximum output tokens;
   - additional model-specific limits and constraints where reported.

   Provider/account quotas and rate limits remain separate operational metadata and are not copied into the model limit set unless the provider explicitly identifies a limit as model-scoped.

6. **Pricing and economics**
   - input and output pricing;
   - separately priced reasoning/thinking tokens where reported;
   - cached input, image/audio, request, or other provider-defined billing units where reported;
   - explicit free/zero-cost indication;
   - normalized source unit quantity and currency qualification;
   - bounded pricing conditions/variants where provider evidence requires them.

   Pricing normalization occurs at provider discovery ingestion. A comparable token rate must have an explicitly established source quantity and qualifying USD currency. Supported source bases include per-token, per-1K-token, and per-1M-token pricing; an unresolved source quantity or non-qualifying currency may remain observational metadata but is non-comparable to the canonical USD token filter. Hive must not infer a token quantity from the raw numeric value.

   The canonical token comparison uses normalized USD input/output token rates per 1M tokens and selects a deterministic applicable pricing variant. It must not silently choose an arbitrary variant when applicability is unknown.

   Free pricing evidence requires explicit provider evidence or a complete provider-reported input/output token-price pair that is both exactly zero. The zero pair is free evidence even when missing currency or quantity leaves the rates non-comparable to the canonical USD filter; comparability and free-evidence qualification are separate decisions. A zero-priced unrelated billing dimension, such as image or request charges, does not establish that the model is free. Free pricing evidence does not guarantee zero user/account cost under every provider plan, routing arrangement, quota, or policy. Missing pricing is **not** interpreted as free. Pricing is discovery evidence, not a cost-policy decision.

7. **Operational metadata**
   - availability;
   - health;
   - discovery observation timestamp and freshness;
   - other provider-reported operational state that belongs to the model rather than the provider/account.

Hive normalizes common semantics into the provider-neutral profile, but the discovery adapter must not discard useful machine-readable provider metadata merely because Hive does not yet have a dedicated first-class field for it. Additional provider-specific metadata/evidence is retained in a structured, bounded, extensible, non-secret form for future use. Extension data is discovery evidence only; it cannot bypass Hive's normalized capability, authorization, selection, policy, or budget boundaries, and consumers must not treat an extension they do not understand as an authoritative contract. Raw provider responses remain subject to the existing response-size, validation, cancellation, and secret/redaction rules.

Absence has explicit semantics:

- no reported modality ≠ no modality;
- no reported capability ≠ Unsupported;
- no reported limit ≠ unlimited;
- no reported pricing ≠ free;
- no reported operational state ≠ healthy/available.

Configured `ExecutionTarget` capability overrides remain authoritative for effective target capability resolution. Discovery may provide the missing evidence used by selection, display, reconciliation, and later features, but discovery never mutates those configured overrides. The complete model profile remains discovery evidence; automatic target reconciliation copies only the target state explicitly owned by the ExecutionTarget contract (including stable execution identity and applicable discovered capability state) rather than treating the model profile as durable target configuration.

No separate durable `Model` resource is introduced by this boundary. The discovery snapshot remains the reusable source for the complete observed model profile. A discovered profile is correlated to a durable ExecutionTarget only through the deterministic ProviderAccount + endpoint + model/deployment identity boundary used by reconciliation; model name alone is never sufficient identity. Pricing, modalities, descriptive metadata, model limits, and other rich profile fields do not become duplicated durable target configuration merely because an automatic target exists. A durable cross-restart model catalog, if later required, is a separate architectural decision and must not be inferred from the existence of discovery.

Discovery remains conservative and ephemeral:

1. a successful fresh observation may be cached and consumed by Management/UI/reconciliation and other authorized provider-platform consumers;
2. a failed, cancelled, stale, unsupported, malformed, rate-limited, or authentication-failing discovery never means zero models;
3. a failed refresh leaves the last successful observation intact;
4. discovered model metadata never silently rewrites configured ExecutionTarget capability overrides;
5. stale discovery contributes no effective discovered capability or other stale model evidence to current selection;
6. provider/model availability and health remain operational metadata, not capability grants.

The existing Phase 1.16 cache/freshness/concurrency/security rules remain authoritative, including endpoint identity checks, forced-refresh semantics, credential-cache invalidation, bounded retention, cancellation, typed failures, and secret/raw-response redaction.

### Provider Configuration and Automatic Target Reconciliation

The normal Settings product experience is provider-centric rather than resource-graph-centric.

The built-in provider catalog is static application metadata describing supported provider choices, transport/authentication requirements, default discovery endpoint behavior, and any provider-specific onboarding requirements. Its authentication metadata distinguishes **No credential**, **Optional credential**, and **Required credential**. A catalog entry is not itself a persisted Provider resource.

Normal onboarding is:

```text
Add Provider
    ↓
select built-in provider
    ↓
supply credential material according to its catalog authentication mode
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

The credential is never persisted in the Provider record or returned to the Settings UI. The normal Add Provider dialog remains intentionally minimal: it selects a built-in provider and collects only its credential material; it does not expose endpoint, account, model, deployment, or target administration. For providers using the normal simple credential workflow, Add collects the API key and normal Edit replaces that API key; Provider catalog identity and transport configuration are not edited through the normal surface. Providers that require account-specific or non-universal endpoint configuration use Advanced Provider Configuration for the additional setup. Authentication requirements remain catalog-defined so a future built-in provider with a different simple credential shape can use the appropriate protected credential input rather than forcing an incorrect API-key model.

The durable ProviderAccount resource still exists even though normal users do not manage it directly. This preserves the ability to support multiple accounts/projects/credentials through Advanced Provider Configuration without creating a second configuration model.

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

### Advanced Provider Configuration Boundary

Advanced Provider Configuration is a single generalized administrative surface:

```text
Advanced Provider Configuration
├── Providers
├── Accounts / Credentials
└── Execution Targets
```

It is not provider-specific. The Advanced entry point may optionally open with a selected provider/account filter for convenience, but the underlying pages and contracts remain generalized.

Advanced Provider Configuration exists for cases that the normal Provider onboarding intentionally hides:

- multiple ProviderAccounts or credentials;
- custom or alternate endpoints;
- local and self-hosted OpenAI-compatible servers;
- manually configured models/deployments;
- explicit capability overrides;
- unusual provider/account/target relationships;
- administrative lifecycle management and troubleshooting.

The normal Settings page therefore represents the user-facing service configuration, while Advanced Provider Configuration represents the underlying resource administration. They are two presentation levels over the same authoritative Management contracts, not two competing configuration systems.

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

The existing `ExecutionTargetSelector` remains the single capability-selection policy boundary. It supports the existing internal Auto/Preferred/Fixed selection contract and deterministic tie-breaking. The Agent-facing `Auto` / `Favorites` distinction is a target-source choice applied before this selector; `Favorites` is not a new internal `ExecutionTargetSelector` mode. The normal Provider Settings UI does not expose target-selection policy.

Favorite ExecutionTarget preferences are a separate candidate-pool concern. A favorite set is stored as durable owner/scope preference state containing only ExecutionTarget identities. When a consumer has a non-empty favorite set, it filters its supplied target candidates to those identities before invoking ExecutionTargetSelector; an empty set leaves the candidate set unchanged. Favorite state never alters target configuration, capability evidence, lifecycle, authorization, automatic/manual management mode, or selector ranking, and the filter does not fall back to non-favorite targets when the filtered set contains no qualifying target.

Agent configuration is a separate Agent-owned concern. The existing AgentDefinition → ExecutionTarget relationship remains the durable foundation established earlier. The V1 Agent interaction/configuration slice uses the favorite preference conditionally: when no favorites exist, Agent `Auto` supplies the normal eligible ExecutionTarget set to the existing capability-aware planning boundary; when one or more favorites exist, `Auto` first filters candidates to those favorites. Agent `Favorites` is the explicit favorite-only target source and lists only saved favorites, even when there is one, persisting the exact selected ExecutionTarget identity. A non-empty favorite pool never expands to non-favorites when no favorite qualifies; an empty Favorites source has no selectable candidate. A selected favorite that later becomes unusable follows the exact-target failure boundary rather than silently switching. The UI may display provider/model/deployment details for that target, but model name alone is never a durable selection key because the same model identifier may exist under multiple providers, accounts, endpoints, or deployments.

Operational availability/health remains distinct from capability. Before an Agent Auto selection is executed, the execution-planning boundary must exclude targets that are explicitly ineligible under the current operational state; it must not reinterpret health or availability as a capability grant. The exact operational-eligibility policy belongs to execution planning, not Provider Settings.

No V1 workflow, MAF orchestration, business-operation write, Tool authorization, Review, cognition, or future-phase behavior is part of the provider configuration/discovery/reconciliation boundary.
## Phase 1.17 Structured Extraction & Validation Boundary

Target-schema source and mapping context:
- Phase 1.17 consumes a target semantic-field schema from the existing host/business semantic boundary. It does not define the later business-operation capability or invent a replacement host schema.
- A mapping context is one source table-like region (normally a worksheet/table region with its headers) plus one target semantic-field schema. A workbook may therefore produce multiple mapping contexts.
- Multiple files or worksheets may reuse a mapping only after Hive deterministically establishes compatible source structure and identical target schema. Otherwise they are mapped independently.

Durability:
- Accepted spreadsheet mappings, mapping-review state, extracted/reviewed candidates, and per-item processing outcomes are retained in durable Hive work/batch state so restart/recovery does not erase user corrections or require repeated LLM mapping.
- This state is Hive-owned processing state, not a copy of host business records and not the host application's database.

Image execution:
- Each image has an independent extraction attempt against the applicable target semantic schema and concrete ExecutionTarget.
- A batch may orchestrate many image attempts, but images do not need to be combined into one provider request. Independent attempts preserve per-image failure isolation, execution attribution, and source provenance.

Human correction:
- User mapping/candidate edits are explicit deterministic state changes. A correction does not trigger another LLM mapping/extraction call unless the user explicitly requests a new interpretation.
- The candidate authorization checkpoint applies to an explicit accepted subset; failed, rejected, or uncertain items remain outside the accepted set until resolved.

Phase 1.17 consumes the completed Phase 1.15 PreparedInput boundary and produces a source-neutral structured candidate. It is an interpretation/validation boundary, not a host business-operation boundary.

Input selection and batching:
- Single File and Folder are explicit user selection modes.
- A single file becomes a one-item InputSubmission; a folder becomes one bounded batch of discovered input items.
- Folder batches may contain multiple Excel files, multiple images, and unsupported file types together.
- Folder enumeration is non-recursive by default. An explicit Include Subfolders option may enable bounded recursive enumeration with depth, item count, file size, total size, and cancellation/resource limits.
- Unsupported items are recorded as per-item failures and do not make safe supported items fail as a group.

Target semantic schema:
- Extraction and mapping operate against a source-neutral target field contract supplied by the owning host/business boundary.
- Target fields have stable semantic identity; database column names, control names, and display labels are not durable field identity.
- The contract may describe display name, expected type, requiredness, parent/child structure, data-source identity, optional database-field reference, and bounded lookup/reference semantics required to construct a candidate.
- Data-source/database-field metadata is descriptive evidence for mapping and extraction; stable semantic field identity remains authoritative.
- Host database schema and private host types remain behind the existing host semantic contract.

Spreadsheet interpretation:
- Reuse the existing bounded .xlsx preparation implementation for workbook/worksheet/row mechanics rather than introducing a second spreadsheet-reading stack without a concrete requirement.
- Build a bounded mapping context from workbook structure, header rows, and a representative sample of row values.
- The LLM proposes source-column to target-semantic-field mappings once per applicable workbook/mapping context, not once per row.
- Hive validates the mapping deterministically before it is applied.
- The mapping is a reviewable artifact that a human can inspect and edit before downstream use.
- Once accepted, the mapping is applied deterministically to all applicable rows/files sharing that mapping context.
- Mapping identity/provenance records the source context and target semantic-field identities; a column display name or database field name alone is never the durable mapping key.

Vision interpretation:
- Images continue to use the existing capability-aware routing boundary to identify an eligible vision-capable ExecutionTarget.
- Each image is interpreted against the target semantic-field contract and produces its own extraction result/candidate or typed failure.
- A batch may process many images under one processing plan while preserving independent per-image status, source identity, execution identity, and provenance.
- One failed image never requires successful images in the same batch to be discarded.
- An image is not required to have its own human authorization merely because it requires an individual model call; batch-level processing authorization may cover the selected processing plan when policy permits.

StructuredCandidate:
- Model output is parsed into a typed, source-neutral candidate contract.
- Candidate structure supports parent data and child collections where the later target operation requires them.
- Required-field and type validation are deterministic Hive concerns.
- Domain validation remains owned by the applicable host/business semantic contract; Hive must not duplicate the host's business rules merely to validate extraction.
- Malformed output is rejected as a typed extraction/serialization failure.
- Candidate fields/items can carry validation state and provenance instead of silently dropping unusable values.
- Relevant execution, mapping, source, and evidence/confidence metadata may be retained; confidence is evidence, never authorization.

Review and governance boundary:
Review and governance boundary:
- Phase 1.17 defines two distinct human checkpoints: authorization to process the selected input batch, and authorization to proceed with the reviewed/corrected candidate and mapping results.
- The processing checkpoint may cover an entire selected batch rather than requiring one authorization per image solely because each image causes an individual model call.
- The second checkpoint occurs after extraction/mapping results are available. The user can inspect failures, open original sources, edit mappings/candidate values, exclude failed or uncertain items, and then authorize the accepted candidate set to proceed. This is approval of the interpreted data for downstream use, not approval of the eventual host mutation.
- The later business-operation proposal has its own authorization boundary before consequential host mutation.
- The generalized Approve / Reject intervention state machine belongs to Phase 1.22 and must not be recreated inside the extraction subsystem.

Result and provenance:
- A batch is an operational grouping only; every input item retains its own source identity, result/failure, provenance, and downstream correlation.
- A stable mapping can be applied repeatedly within its defined source context without requiring repeated LLM interpretation.
- Successful candidates and rejected/failed candidates remain attributable to their exact source item.
- Phase 1.17 does not mutate host state, create business-operation receipts, or perform the later governed business write.
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

A narrower scope must not be silently collapsed to a broader scope when required identity is missing. In particular, User scope requires the applicable TenantId and UserId according to the canonical ResourceScope contract; a preference operation missing required scope identity must fail closed rather than being treated as Global.

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