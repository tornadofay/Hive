# Phase 1 — Base Agent, Provider Platform, Management UI, and Data-Entry Pipeline (V1)

This document contains the detailed ordered plan for this phase. It does not authorize implementation; authorization remains in `docs/Hive_Active_Work.md`.

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
Objective: establish the initial Provider/model discovery boundary with provider-neutral capability evidence and operational metadata.

Scope:
- provider/account connection and metadata discovery through the existing provider/Management boundary;
- model/target enumeration when a provider supports it;
- normalization of provider-reported model capabilities into Hive capability keys;
- discovered `Supported / Unsupported / Unknown` capability state;
- refresh and stale-discovery handling;
- provider/model availability and health metadata;
- discovery failures remain typed and do not silently fabricate capabilities;
- explicit configured capability overrides remain distinguishable from discovered capability information;
- capability-aware ExecutionTarget selection continues to use the existing authoritative capability boundary.

This slice establishes the initial discovery contract. A later bounded 1.16 follow-up expands that contract to the complete useful provider-reported model metadata profile defined by the architecture.

Verify:
- supported provider discovery;
- model enumeration where available;
- capability normalization;
- unsupported/unavailable capability handling;
- discovery timeout/cancellation/failure;
- stale metadata refresh;
- explicit capability override behavior;
- no provider credentials or secrets appear in metadata/diagnostics.

## 1.16 UI — Provider Configuration, Discovery & Target Reconciliation
Objective: make the completed Phase 1.16 provider/model discovery capability directly usable through a simple end-product Provider Settings experience, while preserving the underlying Provider → ProviderAccount → ExecutionTarget resource model for advanced administration.

Normal Provider Settings:
- present only the providers the user has configured;
- use the existing shared Hive CRUD presentation rather than provider-specific cards or a per-row action column;
- use normal CRUD Edit to replace the configured provider's protected API key/credential; provider identity/transport changes belong only to Advanced;
- use the existing Provider lifecycle behavior for Retire, preserving durable provider identity/history and preventing use/reactivation of dependent resources while the provider is inactive;
- provide toolbar actions `Add Provider`, `Refresh`, and `Advanced`;
- `Add Provider` opens a compact provider-configuration dialog using the built-in provider catalog and the provider's required credential input;
- normal setup does not require users to create ProviderAccounts or ExecutionTargets manually;
- credentials remain secret-backed and are never displayed or returned as plaintext;
- `Refresh` requests fresh discovery for configured provider/account/endpoint contexts and reconciles the automatically managed ExecutionTargets before refreshing the provider list/summary;
- provider status/model-count and other safe operational summary are presentation data, not a second configuration model.

Built-in provider catalog:
- is static application metadata, not persisted runtime configuration;
- identifies supported provider choices, transport/authentication requirements, and default discovery endpoint behavior;
- must not imply that every provider can be configured with an API key when its actual authentication requirements differ.

Management-owned onboarding:
- creates/enables the durable Provider;
- creates the default ProviderAccount for normal setup;
- stores credential material through the existing Secret Store boundary;
- performs discovery/reconciliation outside the credential/configuration transaction;
- preserves configured Provider/ProviderAccount state when discovery fails.

Automatic target reconciliation:
- consumes successful fresh ProviderDiscoverySnapshot evidence at ProviderAccount + endpoint scope;
- must not create a fake/placeholder persisted ExecutionTarget solely to perform discovery;
- materializes durable ExecutionTargets for discovered models/deployments;
- gives automatic targets an explicit durable `ExecutionTargetManagementMode` (`Automatic` / `Manual`) distinct from administrator-managed targets;
- uses a stable reconciliation identity based on ProviderAccount, endpoint, and model/deployment identity so rediscovered models reuse their automatic target where possible;
- retires missing automatic targets without physically deleting their durable identity;
- may reactivate/reuse an automatic target when the same model/deployment returns and normal lifecycle/dependency validation succeeds;
- never interprets failed/stale/unsupported discovery as an empty model catalog;
- never overwrites administrator-managed target endpoint/model/deployment/capability configuration.

Advanced Provider Configuration:
- is one generalized administrative entry point beside `Add Provider` and `Refresh`, not a provider-specific action;
- is independent of row selection or provider selection;
- opens the existing generalized Providers / Accounts / Credentials / Execution Targets administration pages;
- may carry the current provider as an initial filter for convenience, but its contracts remain provider-neutral;
- is the supported path for multiple accounts, custom/alternate endpoints, local/self-hosted OpenAI-compatible servers, manually configured models/deployments, explicit capability overrides, and administrative lifecycle/troubleshooting.

UI and ownership constraints:
- WinForms remains a thin presentation layer over Hive.Management;
- no direct provider transport, Secret Store access, SQL, migration, or reconciliation logic lives in the UI;
- the existing Phase 1.16 discovery freshness, cancellation, failure, concurrency, endpoint-identity, credential-invalidation, and secret-redaction rules remain authoritative;
- discovered capability/operational metadata remains observational and does not silently rewrite durable configured capability overrides.

Verify:
- configured built-in providers can be added through the normal Provider page with the required credential input;
- Add Provider creates the expected Provider/ProviderAccount relationship and initiates the discovery/reconciliation boundary without exposing secrets;
- Refresh performs fresh discovery/reconciliation and updates the provider-facing operational summary;
- newly discovered models materialize as automatic targets;
- removed models cause only the corresponding automatic targets to retire/reconcile and do not erase durable identity;
- failed, stale, unsupported, cancelled, and authentication-failing discovery preserves existing targets and reports the operational condition;
- administrator-managed/advanced targets are not overwritten by automatic reconciliation;
- Advanced opens the generalized resource-management surface and supports local/self-hosted/custom configuration paths;
- the complete Provider Settings workflow is manually verified in the Example Host;
- focused automated coverage exercises onboarding, refresh/reconciliation, lifecycle, concurrency/idempotency, failure/cancellation, and credential secrecy boundaries.

## 1.16 Follow-Up — Complete Provider Model Metadata Discovery
Detailed implementation plan: [Phase 1.16 Follow-Up — Model Information](plan/Phase1.16_FollowUp_Model_Information.md)

Objective: extend the established Phase 1.16 discovery boundary to preserve the complete useful provider-reported model profile within Hive's existing security, validation, response-size, cancellation, and bounded-processing constraints, and expose that information through the Advanced Provider Configuration **Model Information** page.

Scope and non-goals are defined in the detailed plan. The durable Provider → ProviderAccount → ExecutionTarget graph remains unchanged; no durable Model resource, second provider transport architecture, Agent target-selection redesign, automatic all-model probing, or periodic/background discovery is introduced.

Verify: rich model metadata normalization and absence semantics; modalities, capabilities, reasoning/thinking and options, limits, pricing/free evidence, operational metadata, bounded provider-specific evidence, failure/cancellation/security behavior, stable reconciliation and override authority, structured capability UI, credential semantics, Advanced Provider Configuration tree/Overview/Model Information navigation, and matching Example Host verification.

## 1.17 — Structured Extraction & Validation
Detailed implementation plan: [Phase 1.17 Structured Extraction & Validation](plan/Phase1.17_Structured_Extraction_Validation.md)

Objective: turn the completed Phase 1.15 prepared-input boundary into a source-neutral, typed StructuredCandidate boundary for later business-operation proposals, with bounded batch processing, one-time semantic mapping where required, human-reviewable extraction/mapping results, validation, and provenance. Phase 1.17 never mutates the host business application.

Phase 1.17 implementation slices, in order:
1. **Input Selection & Batch Construction**
   - support explicit Single File and Folder selection;
   - normalize both selection modes into the existing InputSubmission/InputItem model;
   - allow mixed supported file types and multiple files in one folder batch;
   - folder selection is non-recursive by default; an explicit Include Subfolders choice may opt into bounded recursive enumeration;
   - identify unsupported files without preventing safe supported items from continuing.
2. **Target Schema & Semantic Field Contract**
   - expose the target operation's source-neutral semantic fields needed for extraction and mapping;
   - use stable semantic field identity, not database-field names, control names, or display labels as the durable mapping key;
   - consume the target semantic-field schema from the existing host/business semantic boundary rather than inventing a database schema;
   - include human-readable field name, expected type, requiredness, parent/child structure, data-source identity, optional database-field reference, and bounded lookup/reference semantics where required;
   - data-source/database-field metadata is descriptive mapping evidence only; stable semantic field identity remains authoritative;
   - keep host database schema and private host types behind the existing host semantic boundary.
3. **Spreadsheet Profiling & One-Time Mapping**
   - use the existing bounded .xlsx preparation boundary for workbook/worksheet/row mechanics;
   - inspect workbook structure, headers, and a bounded representative sample of row values to build a mapping context;
   - use the LLM once to propose source-column → target-semantic-field mappings for a workbook/mapping context;
   - validate the proposed mapping deterministically through Hive before it is applied;
   - make the mapping human-reviewable and editable;
   - after the mapping is accepted, apply it deterministically to all applicable rows without an LLM call per row;
   - preserve mapping provenance/evidence and the mapping's source context.
4. **Vision Extraction**
   - route each image to an eligible vision-capable ExecutionTarget using the existing authoritative routing/selection boundary;
   - extract each image against the target semantic-field contract;
   - process multiple images as one bounded batch while retaining independent per-image status/result/provenance;
   - isolate a failed image from successful images and preserve the original source identity for later review.
5. **Candidate Normalization, Validation & Provenance**
   - parse model output into a typed, source-neutral StructuredCandidate;
   - support parent/child candidate structure;
   - perform deterministic required-field/type validation and apply host/domain validation through the appropriate owning contract;
   - represent field/item validation failures explicitly rather than silently dropping invalid data;
   - reject malformed model output safely;
   - preserve source linkage, extraction/mapping provenance, execution identity, and relevant evidence/confidence metadata without treating confidence as authorization.
6. **Reviewable Results & Handoff**
   - provide a batch-level result view with per-file/per-item outcomes;
   - show failed extraction/mapping/validation items with safe error information and source file identity;
   - allow the user to open the original image/file for inspection where the host/UI surface permits;
   - allow correction of mappings and candidate values before downstream business-operation work;
   - produce a stable, reviewable candidate result ready for the later governance/business-operation pipeline.
   - generalized Approve / Reject authorization remains owned by the Phase 1.22 human-intervention boundary; Phase 1.17 must not invent a second authorization system.

Batch semantics:
- a single file is a one-item submission;
- a folder is an input-selection scope that becomes one bounded batch of input items;
- multiple Excel files may coexist with images and other supported inputs in one batch;
- each input retains its own item identity and result/failure even when grouped in one batch;
- batch grouping must never erase per-item provenance, validation state, or recovery/audit identity.

Human-review checkpoints:
Human-review checkpoints:
- **Processing authorization checkpoint:** after Single File or Folder selection, the user may authorize the selected processing batch as one unit. This authorization covers the planned extraction work and its associated model/provider processing; it does not need to be repeated for every image in the batch when policy permits batch authorization.
- **Candidate/mapping authorization checkpoint:** after processing completes, Hive presents the resulting spreadsheet mappings and image-extracted candidates for human inspection and correction. The user may edit incorrect mappings/values and then authorize the accepted candidate set to proceed to the next stage. This checkpoint authorizes use of the reviewed candidate data; it is not yet the consequential host business write.
- **Business-operation authorization:** the later Phase 1.23 business-operation proposal is authorized separately before consequential host mutation.
- Phase 1.17 defines these checkpoints and the reviewable artifacts, but the generalized authoritative Approve/Reject intervention state machine belongs to Phase 1.22. Phase 1.17 must not implement a parallel authorization subsystem.

Non-goals:
- no host business mutation;
- no business-operation proposal or receipt;
- no generic Approve/Reject implementation beyond already-existing boundaries;
- no per-row LLM mapping calls after a mapping is established;
- no model-name-only durable mapping identity;
- no direct SQL/database access to host business data;
- no requirement for one human approval per image.

Verify:
- single-file and folder selection/batch construction, including mixed supported file types;
- bounded folder enumeration and cancellation/failure isolation;
- semantic target-field schema and stable field identity;
- one-time spreadsheet mapping proposal, deterministic mapping validation, human-editable mapping, and deterministic reuse across rows/files in the applicable mapping context;
- image extraction with independent per-image success/failure;
- valid candidate parsing;
- missing required fields and invalid types;
- malformed model output;
- parent/child candidate structure;
- provenance/source linkage preservation;
- reviewable failed-item reporting and source inspection path;
- no host mutation.

## 1.18 — Durable Base-Agent Work State
Objective: make the already-defined Base-Agent work mechanisms durable across runtime lifetimes.

Scope:
- durable Objective state;
- durable WorkItem binding/provenance where applicable;
- durable Question/Answer state;
- durable base-Agent memory/work-state records;
- durable delegation state where required by active work;
- runtime/lifecycle recovery for those work mechanisms;
- preservation of ownership, scope, provenance, version, and concurrency rules;
- deterministic durable retrieval of Base-Agent work records;
- actual versus simulated evidence remains distinguishable.

Explicit non-goal: no CognitiveAgent beliefs/goals/learning/Dream semantics and no vector-database implementation in this slice.

Verify:
- persistence across runtime restart;
- ownership and scope isolation;
- stale/concurrent state handling;
- durable Question transitions;
- durable Objective transitions;
- durable memory/work-state retrieval;
- delegation-state durability where applicable;
- cancellation and recovery;
- no cross-runtime state leakage.

## 1.19 — V1 Vector Retrieval Infrastructure
Objective: establish the bounded, replaceable vector-storage and similarity-retrieval foundation used by later Hive capabilities without turning V1 into a semantic-memory product.

Scope:
- replaceable `IVectorStore` contract;
- SQL Server `VECTOR` implementation for the V1 persistence profile;
- bounded vector insertion;
- bounded similarity search;
- deterministic result ordering/tie handling;
- ownership/scope/provenance boundaries for stored vectors;
- cancellation and bounded result limits;
- integration with the existing Base-Agent work-state persistence boundary only where required for durable references;
- explicit actual versus simulated evidence metadata where vectorized records represent evidence.

Explicit non-goal: no CognitiveAgent beliefs/goals/learning/Dream semantics and no Phase 5 semantic Memory/Knowledge/Learning system.

Verify:
- vector insertion/search;
- similarity ordering and deterministic boundaries;
- ownership and scope isolation;
- malformed/invalid vector handling;
- bounded result behavior;
- cancellation;
- persistence/reload;
- no cross-resource or cross-runtime leakage.

## 1.20 — V1 Workspace Foundation
Objective: establish the real human-facing Workspace as the common interaction and operational surface over Hive.Management.

Scope:
- Workspace becomes a first-class V1 interaction surface, not only a WorkItem monitor;
- common Workspace shell and conversation/history model;
- direct LLM mode with explicit ExecutionTarget selection; the UI may display provider/model/deployment details for human readability.
- conversation/chat submission and display;
- active execution/runtime context;
- WorkItem/job visibility;
- Workspace restart/reload state handling;
- mode/state presentation boundaries;
- normal Management and authorization boundaries remain authoritative;
- Workspace does not create Hive membership merely because Agents are visible;
- Workspace remains a presentation/control surface, not an authority.

Verify:
- Workspace opens as the normal user-facing interaction surface;
- direct LLM conversation;
- explicit ExecutionTarget selection; the UI may display provider/model/deployment details for human readability;
- active execution/runtime visibility;
- conversation/history behavior;
- WorkItem/job visibility;
- Workspace restart/reload;
- authorization remains outside the UI.

## 1.21 — V1 Agent Interaction & Application/Form Agents
Objective: extend the Workspace foundation with Agent-directed interaction and application-scoped specialist Agent operation.

Scope:
- Agent mode with explicit Agent selection;
- Agent execution configuration with `Auto` or an exact selected `ExecutionTarget`;
- `Auto` uses the existing authoritative capability-aware execution-target selection/planning boundary;
- an exact selected `ExecutionTarget` is pinned and must fail clearly when that target becomes unusable rather than silently switching;
- the Agent configuration UI may show provider/model/deployment details as the human-readable label, but the persisted selection is the exact durable `ExecutionTarget` identity; model name alone is never a selection key;
- user commands/objectives submitted to an Agent;
- application-wide Agent support;
- application-scoped role/context such as a Manager Agent;
- form-associated specialized Agents;
- Agent association with bounded host context;
- create/reuse/activate/deactivate Agent instances through the normal Management boundary;
- switching between direct LLM and Agent modes;
- display current Agent/WorkItem/job context;
- ordinary Agent resources with different definitions/context; no new Manager/Invoice framework types;
- no automatic Hive membership or Swarm creation.

V1 Agent model:

```text
Application
    │
    ├── Manager Agent
    ├── Invoice Agent
    ├── Customer Agent
    └── other specialized Agents
```

Verify:
- Agent conversation;
- Agent selection;
- application-wide Agent;
- specialized form-associated Agent;
- create/reuse/activate/deactivate behavior;
- switching modes;
- independent Agent/runtime state;
- bounded host association;
- authorization remains outside the UI;
- Workspace does not create Hive/Swarm state implicitly.

## 1.22 — Governed Tools, Policy, Permissions & Human Intervention
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

## 1.23 — Business-App Proposal & Governed Write
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
- unknown outcome remains unresolved for Phase 1.24 reconciliation.

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

## 1.24 — Business Operation Receipt & Reconciliation
Objective: durably establish what happened after a consequential host operation and recover safely from interrupted or unknown outcomes.

Scope:
- durable `BusinessOperationReceipt` built from the Phase 1.23 operation-attempt state;
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

## 1.25 — Post-Write Review
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

## 1.26 — MAF Sequential V1 Pipeline
Objective: compose the complete single-Agent V1 business workflow through MAF Sequential orchestration before introducing application-level concurrent Agent assignment.

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

Verify: complete deterministic fake-host end-to-end path with one Agent, covering successful and rejected/failed branches.

## 1.27 — Multi-Agent Work Assignment & Concurrent Execution
Objective: allow multiple independent Agents to perform the established V1 workflow concurrently inside one host application without requiring persistent Hive membership or Swarm state.

Scope:
- assign WorkItems/jobs to specific Agent instances;
- application-wide Manager Agent may coordinate assignment through authorized mechanisms;
- specialized Agents can own and execute their own WorkItems;
- multiple Agent runtimes may execute concurrently using the established V1 MAF workflow;
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
- multiple Agents operating simultaneously through the established V1 workflow;
- different forms/contexts;
- independent WorkItems;
- failure isolation;
- cancellation isolation;
- provider/target selection isolation;
- no accidental shared runtime state;
- no implicit Hive/Swarm creation.

## 1.28 — Full-Pipeline Crash/Resume
Objective: prove recovery across the complete V1 workflow, including WorkItem, runtime/execution, durable Base-Agent work state, operation-attempt, receipt, reconciliation, multiple-Agent, and Review recovery.

Verify:
- process termination at several checkpoints;
- restart;
- resume or reconcile without duplicate terminal host mutation;
- already-terminal WorkItems are not processed again;
- failure of one WorkItem or Agent does not incorrectly fail unrelated work;
- durable Base-Agent work state remains consistent;
- completed WorkItems retain receipts;
- Review state survives restart;
- recoverable Agents/WorkItems can resume or reconcile independently.

## 1.29 — Resource Inventory & Runtime Diagnostics
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

## 1.30 — Metrics, Budget Cap & OpenTelemetry
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

Verify:
- budget stop;
- telemetry correlation;
- cancellation;
- secret redaction.
