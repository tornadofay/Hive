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
V1 work-unit rule: a WorkItem is the durable unit of user-visible work and represents one logical business operation when a business operation is required. A single input submission may produce one or multiple independent WorkItems. A submission/batch is an operational grouping, not a replacement for WorkItem identity, lifecycle, provenance, authorization, or any applicable operation receipt or Review state.
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

**V1 completion target:** Hive is usable as an embedded platform inside a real host application such as HForms. Completed V1 provides provider/model discovery and operational metadata, a user-facing Workspace with direct LLM and Agent interaction, durable Base-Agent work state, governed Tools and authorization, application-scoped Agents including multiple concurrent specialist Agents, the complete supported-input-to-host-write pipeline, operation receipt/reconciliation, post-write Review, operational inventory/diagnostics, MAF composition, crash/recovery, budgets, and telemetry. V1 multi-Agent operation does not require persistent Hive membership or Swarm state; multiple Agents may work concurrently inside one application while remaining independent Agent resources. Phase 2 introduces the persistent Hive collective, membership, and Swarm model.

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

## 1.11 — Initial Workspace & WorkItem Operations
Objective: establish the initial Workspace foundation over Hive.Management for image-backed WorkItems and governed approval interaction. This is the first V1 Workspace slice; later V1 slices expand it into direct LLM/Agent interaction and multiple independent Agent operation.

Initial scope:
- submit/attach an image to a WorkItem;
- view WorkItem status and activity;
- view relevant execution/provider status;
- receive WorkItem notifications;
- view PendingApproval;
- Approve / Reject the governed business-app write.

This initial Workspace foundation uses a single Agent and does not require Hive membership or Swarm state. Later V1 slices may add multiple independent Agents and direct LLM/Agent interaction without changing the persistent Hive/Swarm boundary.

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

Objective: establish Hive-owned neutral host-integration contracts and a reusable WinForms implementation layer so a host application can adopt Hive with minimal integration code while keeping all host-private business semantics authoritative in the host.

Design model:
- Hive.Core owns the neutral host integration contracts and guarantees.
- Hive provides reusable WinForms base forms/controls and bounded default implementations for common control/data-surface semantics.
- A typical host can opt in by deriving application forms from `HiveForm` and using Hive-owned base controls such as `HiveTextBox`, `HiveComboBox`, `HiveDataGridView`, and other justified common controls.
- Common integration metadata and capabilities should work automatically through deterministic conventions/defaults; explicit host metadata and semantic hooks override those defaults when the application meaning cannot be safely inferred.
- Host-specific business semantics remain host-owned: authoritative parent/child relationships, application-specific field meaning, lookup resolution, validation/save semantics, and business/application actions.
- Existing native/custom WinForms controls that cannot or should not derive from Hive controls remain supported through the bounded adapter/semantic-provider path.
- Hive must not turn the base-control layer into a complete replacement WinForms toolkit or expose host-private control libraries, SQL, credentials, arbitrary reflection, or unrestricted invocation through the neutral boundary.

Scope:
- host-neutral Core-defined contracts for host registration/context, semantic controls, data surfaces, fields, stable row identity, lookups, bounded UI interaction, and business-operation capabilities needed for API/UI composition;
- reusable bounded WinForms base forms/controls that implement the neutral contracts without leaking presentation or host-private business semantics into Hive.Core;
- deterministic default metadata/capability behavior for common WinForms controls, including safe conventions for control/field/surface identity where inference is reliable;
- explicit override points for host-specific field/surface names, generated/computed semantics, primary-key row identity, parent/child relationships, lookup dependencies, and other business-specific meaning;
- bounded WinForms discovery/adaptation over native/custom controls where base-control inheritance is not practical or desired;
- explicit parent/child data-surface relationships where the host provides them, including combined parent/child save semantics when supported by the host;
- stable row identity using the current V1 primary-key ID contract; row index is positional only;
- generated-field and computed-field semantics without requiring a particular implementation mechanism;
- bounded lookup operations, including dependencies on current-record values and/or external host/application context;
- separation of UI interaction capabilities from business-operation semantics;
- API, UI, and API+UI composition contracts behind one authorized logical operation, without implementing the consequential business write in this slice;
- Management-owned authorization/orchestration through Core-defined host ports, plus provenance, cancellation, lifecycle/disposal, stale-state, and supported concurrency evidence.

Known V1 host semantics represented by the contracts:
- New, Edit, Save, Delete, Reload, Move, Search, Report, Print, and Preview may be exposed as bounded host capabilities when the host provides them;
- generated and computed fields are semantic states; their internal implementation remains host-owned;
- child editing is supported and parent/child changes may form one combined business operation;
- ordinary host save behavior is supported without requiring an optimistic-concurrency token;
- DataGridView row-index handling remains positional and must not replace stable primary-key identity.

Phase 1.14 revision work:
- define the reusable Hive WinForms base-control/form hierarchy and ownership boundary;
- define automatic/default semantic metadata behavior and deterministic convention rules;
- define explicit host override points so application-specific semantics remain visible and authoritative;
- implement justified common base controls without creating a complete control toolkit;
- connect base-control metadata/capabilities to the existing neutral contracts and Management authorization path;
- preserve the existing adapter/semantic-provider compatibility path for controls that cannot use Hive base types;
- extend the deterministic reference host/example to prove the low-code host integration path and the explicit-override path;
- extend focused contract/WinForms tests for defaults, overrides, lifecycle, disposal, stable IDs, generated/computed fields, child surfaces, lookups, authorization, and unsupported/malformed cases.

Verify:
- a minimal host Form can derive from the Hive base form and gain bounded integration behavior without manually constructing neutral descriptors for every standard control;
- standard Hive base controls automatically expose safe default field/surface/capability metadata;
- explicit host overrides replace defaults deterministically without bypassing Hive authorization;
- parent/child surfaces, stable primary-key identity, generated/computed fields, and bounded dependent lookup semantics remain correct;
- non-inheriting native/custom controls continue to work through the bounded adapter/semantic-provider path;
- authorization is enforced before consequential capability execution;
- API-only, UI-only, and API+UI composition remains represented under one logical correlation;
- registration/disposal/cancellation/lifecycle and provenance remain correct;
- no host business write is introduced in this slice.

### Phase 1.14 revision implementation boundary

The reopened revision is implemented around the concrete Hive-owned WinForms base layer:

- `HiveForm` plus bounded native-derived controls `HiveTextBox`, `HiveComboBox`, `HiveCheckBox`, `HiveDateTimePicker`, `HiveNumericUpDown`, and `HiveDataGridView`;
- deterministic control, field, surface, and automatic capability identities;
- explicit field/surface metadata for generated/computed fields, primary-key identity, lookups, and parent/child relationships;
- compatibility with the existing non-inheriting adapter/semantic-provider path;
- Management authorization remains the gate for interactions and application semantics remain host-owned.

Verification and slice completion remain controlled by `docs/Hive_Active_Work.md`; this roadmap entry does not constitute verification evidence.

## 1.15 — Input Preparation & Routing
Objective: prepare supported V1 input sources and route each source through the capability required to produce the common prepared-input boundary.

Initial V1 input paths:

```
Image
  → Vision-capable execution target
  → prepared input

Spreadsheet
  → workbook / worksheet / row parsing and mapping
  → prepared input
```

Input-specific processing must converge on the common prepared-input boundary rather than creating separate downstream business-operation pipelines. Phase 1.17 consumes prepared input to produce the common structured-candidate boundary.

Verify: image routing, spreadsheet workbook/worksheet/row handling, multiple WorkItems from one submission, bounded file/workbook/row processing, cancellation, input failure isolation, and unsupported-input handling.

## 1.16 — Provider / Model Capability Discovery & Operational Metadata
Objective: complete the Provider platform with automatic discovery of provider/model metadata and normalized capability state.

Scope:
- provider/account connection and metadata discovery through the existing provider/Management boundary;
- model/target enumeration when a provider supports it;
- normalization of provider-specific model capabilities into Hive capability keys;
- discovered `Supported / Unsupported / Unknown` capability state;
- refresh and stale-discovery handling;
- provider/model availability and health metadata;
- discovery failures remain typed and do not silently fabricate capabilities;
- explicit configured capability overrides remain distinguishable from discovered capability information;
- capability-aware ExecutionTarget selection continues to use the existing authoritative capability boundary.

Verify:
- supported provider discovery;
- model enumeration where available;
- capability normalization;
- unsupported/unavailable capability handling;
- discovery timeout/cancellation/failure;
- stale metadata refresh;
- explicit capability override behavior;
- no provider credentials or secrets appear in metadata/diagnostics.

## 1.17 — Structured Extraction & Validation
Objective: produce typed candidate business data from supported input capabilities and apply required-field, type, and domain validation.

Scope:
- image/vision extraction;
- spreadsheet-derived structured candidate input;
- source-neutral candidate contract;
- parent/child candidate structure when required by the target operation;
- required/type/domain validation;
- malformed extraction result handling;
- candidate provenance and source linkage;
- no host mutation.

Verify:
- valid candidate;
- missing fields;
- invalid types;
- malformed model output;
- parent/child candidate structure;
- invalid/rejected candidate path;
- provenance preservation.

## 1.18 — Durable Base-Agent Work State & Vector Retrieval
Objective: make the already-defined Base-Agent work mechanisms durable across runtime lifetimes while establishing the bounded V1 vector-storage/retrieval foundation.

Scope:
- durable Objective state;
- durable WorkItem binding/provenance where applicable;
- durable Question/Answer state;
- durable base-Agent memory/work-state records;
- durable delegation state where required by active work;
- runtime/lifecycle recovery for those work mechanisms;
- preservation of ownership, scope, provenance, version, and concurrency rules;
- replaceable `IVectorStore` contract;
- SQL Server `VECTOR` implementation for the V1 persistence profile;
- bounded vector insertion and similarity search;
- vector retrieval remains infrastructure, not a Phase 5 semantic-memory system;
- actual versus simulated evidence remains distinguishable.

Explicit non-goal: no CognitiveAgent beliefs/goals/learning/Dream semantics.

Verify:
- persistence across runtime restart;
- ownership and scope isolation;
- stale/concurrent state handling;
- durable Question transitions;
- durable Objective transitions;
- durable memory retrieval;
- vector insertion/search;
- cancellation and bounded retrieval;
- no cross-runtime state leakage.

## 1.19 — V1 Workspace & Agent Interaction
Objective: provide the real human-facing Workspace where users interact directly with LLMs or with Hive Agents.

Scope:
- Workspace becomes a first-class V1 interaction surface, not only a WorkItem monitor;
- LLM mode with explicit model/ExecutionTarget selection;
- Agent mode with explicit Agent selection;
- conversation/chat surface;
- active Agent/runtime/execution context;
- user commands/objectives submitted to an Agent;
- application-wide Agent support;
- application-scoped role/context such as a Manager Agent;
- form-associated specialized Agents;
- Agent association with bounded host context;
- create/reuse/activate/deactivate Agent instances through the normal Management boundary;
- switching between LLM and Agent modes;
- display current WorkItems/jobs produced by the interaction;
- Workspace never creates Hive membership merely because multiple Agents are visible;
- Workspace remains a presentation/control surface over Management and does not become an authority.

V1 Agent model:

```text
Application
    │
    ├── Manager Agent
    ├── Invoice Agent
    ├── Customer Agent
    └── other specialized Agents
```

These remain ordinary Agent resources with different definitions/context. `ManagerAgent`, `InvoiceAgent`, etc. are not required as new framework types.

Verify:
- direct LLM conversation;
- explicit model selection;
- Agent conversation;
- Agent selection;
- application-wide Agent;
- specialized form-associated Agent;
- switching modes;
- independent Agent/runtime state;
- Workspace restart/reload;
- authorization remains outside the UI.

## 1.20 — Governed Tools, Policy, Permissions & Human Intervention
Objective: establish the general governance boundary used whenever an Agent proposes or performs consequential work.

Scope:
- first-class Tool identity/capability contract;
- Tool registration/discovery through Hive-owned contracts;
- policy/permission evaluation;
- authorization decisions;
- provenance and audit evidence;
- generic human-intervention contract;
- V1 `Approve / Reject` implemented through the intervention boundary;
- intervention states and stale-state handling;
- deterministic fail-closed authorization;
- business/application actions remain host-owned;
- model output never grants authorization;
- UI capability never substitutes for Hive authorization.

Verify:
- unauthorized Tool execution rejected;
- authorized Tool execution succeeds;
- stale authorization/intervention rejected;
- Approve/Reject behaves through the generalized intervention boundary;
- provenance/audit evidence preserved;
- model cannot grant itself permission.

## 1.21 — Business-App Proposal & Governed Write
Objective: turn validated structured data into an authorized business operation and perform the consequential host write through the approved host operation boundary.

Scope:
- structured BusinessOperationProposal for parent data and, where required, child collections;
- authorization before the consequential operation;
- `PendingApproval` with Approve / Reject semantics when policy requires approval;
- host business operation execution through API, UI, or API+UI implementation;
- each WorkItem has its own logical business-operation identity;
- durable initial operation-attempt state before non-transactionally coupled host submission;
- operation correlation/idempotency identity reused for retries when supported;
- generated host IDs captured from successful creation operations;
- success, known no-side-effect failure, partial, and rejected-before-mutation evidence;
- unknown outcome remains unresolved for Phase 1.22 reconciliation.

Approval answers whether Hive may perform the proposed operation. It is distinct from post-write correctness Review.

Verify:
- pending approval blocks the consequential write when required;
- rejection prevents the side effect;
- approved operation reaches a fake business client or bounded fake host adapter;
- duplicate/stale approval cannot duplicate the write;
- generated host IDs are captured;
- operation identity remains stable for a retry-capable host boundary;
- authorization and provenance remain enforced through the consequential operation;
- unknown/partial host dispositions are preserved for the receipt/reconciliation boundary.

## 1.22 — Business Operation Receipt & Reconciliation
Objective: durably establish what happened after a consequential host operation and recover safely from interrupted or unknown outcomes.

Scope:
- durable `BusinessOperationReceipt` built from the Phase 1.21 operation-attempt state;
- durable attempt remains the recovery anchor after submission but before host response;
- host correlation/idempotency evidence;
- unknown and partial outcome reconciliation;
- authoritative host-state reread when required;
- parent/child identities established by the host remain attributable;
- each WorkItem retains its own operation receipt and reconciliation state.

Verify:
- parent + child identity receipt is durable;
- interrupted/unknown outcome is reconciled from durable state and authoritative host state without duplicate mutation;
- idempotent retry reuses the logical operation identity;
- non-idempotent host requires reconciliation before a second mutation;
- authorization and provenance remain enforced.

## 1.23 — Post-Write Review
Objective: verify the resulting host state against the intended candidate/proposed data through a first-class WorkItem-linked Review boundary.

Scope:
- first-class WorkItem-linked Review object;
- review queue/list;
- bounded authorized action to open/navigate to the associated host record/editor;
- Human, Automated, or Hybrid review modes;
- authorized host-state reread and bounded comparison;
- `PendingReview`, `VerifiedCorrect`, `VerifiedIncorrect`, and unresolved verification states;
- discrepancy recording without rewriting the original candidate;
- minimum bounded review evidence;
- Review uses the operation identity/receipt to locate affected host records.

Approval answers whether Hive may perform the proposed operation. Review answers whether the resulting host state is correct. They are separate lifecycle boundaries.

Verify:
- review locates the exact written host records through the receipt;
- correct result reaches `VerifiedCorrect`;
- incorrect result reaches `VerifiedIncorrect` with discrepancies;
- review does not mutate the original candidate;
- authorization and provenance remain enforced.

## 1.24 — Multi-Agent Work Assignment & Concurrent Execution
Objective: allow multiple independent Agents to perform work concurrently inside one host application without requiring persistent Hive membership or Swarm state.

Scope:
- assign WorkItems/jobs to specific Agent instances;
- application-wide Manager Agent may coordinate assignment through authorized mechanisms;
- specialized Agents can own and execute their own WorkItems;
- multiple Agent runtimes may execute concurrently;
- independent WorkItem queues/assignment state;
- independent cancellation;
- independent failure/retry;
- bounded host-form/context association;
- multiple different forms may have different specialized Agents active simultaneously;
- execution/provider/resource policy remains authoritative;
- Agent identities and WorkItem identities remain separate;
- concurrency and isolation are explicit;
- no persistent Hive membership;
- no Swarm resource.

Example:

```text
Manager Agent
      │
      ├── Invoice Agent A → Invoice Form A → WorkItem 1
      ├── Invoice Agent B → Invoice Form B → WorkItem 2
      └── Customer Agent  → Customer Form  → WorkItem 3

all may run concurrently
```

Verify:
- multiple Agents operating simultaneously;
- different forms/contexts;
- independent WorkItems;
- failure isolation;
- cancellation isolation;
- provider/target selection isolation;
- no accidental shared runtime state;
- no implicit Hive/Swarm creation.

## 1.25 — MAF Sequential V1 Pipeline
Objective: compose the complete V1 business workflow through MAF Sequential orchestration.

```text
Submission
    ↓
WorkItem creation
    ↓
Input preparation / routing
    ↓
Structured extraction / validation
    ↓
Tool / policy / authorization
    ↓
Business proposal
    ↓
Approval / intervention when required
    ↓
Governed host write
    ↓
Receipt / reconciliation
    ↓
Post-write Review
```

MAF owns orchestration where applicable; Hive retains ownership of identity, authorization, host semantics, receipts, reconciliation, Review, and resource governance.

Verify: complete deterministic fake-host end-to-end path.

## 1.26 — Full-Pipeline Crash/Resume
Objective: prove recovery across the complete V1 workflow, including WorkItem, runtime/execution, durable Base-Agent work state, operation-attempt, receipt, reconciliation, multiple-Agent, and Review recovery.
Verify: process termination at several checkpoints, restart, resume or reconcile without duplicate terminal host mutation; already-terminal WorkItems are not processed again; failure of one WorkItem or Agent does not incorrectly fail unrelated work; durable Base-Agent work state remains consistent; completed WorkItems retain receipts; Review state survives restart; and recoverable Agents/WorkItems can resume or reconcile independently.

## 1.27 — Resource Inventory & Runtime Diagnostics
Objective: provide the V1 operational surface for understanding what Hive owns and what it is doing.

Scope:
- authoritative resource inventory;
- Providers, ProviderAccounts, ExecutionTargets, Agents, RuntimeInstances, Executions, WorkItems, jobs/assignments, operation/receipt state, and Review state;
- provider/model availability and health;
- runtime/execution diagnostics;
- lifecycle/state inspection;
- bounded failure diagnostics;
- safe correlation/provenance information;
- no secret disclosure;
- inspection does not mutate state unless an explicitly authorized action exists.

Verify:
- inventory reflects authoritative Management state;
- resource ownership/scope is respected;
- runtime state is inspectable;
- failed operations expose useful typed diagnostics;
- secrets remain redacted;
- inventory survives/reloads from durable state.

## 1.28 — Metrics, Budget Cap & OpenTelemetry
Objective: establish V1 resource control and observability.

Scope:
- request/success/failure/timeout metrics;
- provider/model operational metrics;
- WorkItem/Agent/runtime correlation;
- token/cost tracking;
- hard per-runtime budget;
- applicable provider/model quotas;
- cancellation;
- OpenTelemetry traces/metrics;
- no secrets in telemetry.

Verify: budget stop, telemetry correlation, cancellation, and secret redaction.

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
