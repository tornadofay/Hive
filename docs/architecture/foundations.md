# Hive Architecture — Foundations



This document is part of the authoritative architecture defined by `docs/architecture.md`. The root file is the architecture index and contains global invariants; this document contains the detailed foundation contracts.



### 0.1 Identity and resource foundation

The Phase 0.3 identity/resource contract establishes stable typed identities without creating domain-specific persistence or authorization services prematurely.

The common resource identity set is:

- `DeploymentId`
- `TenantId`
- `PrincipalId`
- `UserId`
- `SessionId`
- `WorkspaceId`
- `AgentId`
- `HiveId`
- `RuntimeId`
- `ExecutionId`
- `WorkItemId`

Each identity is immutable, strongly typed, and non-empty. Identities are references, not mutable state objects.

`ReviewId` is a planned Phase 1.18 identity for the first-class Review resource and is intentionally not part of the current Phase 0 identity implementation until that slice owns the contract.

### Lifecycle semantics

Hive-owned resources use the lifecycle states Active, Suspended, and Retired. Retirement is a durable lifecycle transition that preserves the resource identity and historical record; it is not a physical delete.

Retired resources may be explicitly reactivated to Active through the owning Management boundary. Reactivation is versioned like every other lifecycle transition and preserves the resource identity, ownership, scope, provenance, and metadata. A retired resource may not transition directly to Suspended.

Reactivation of a resource with dependencies must validate those dependencies again at the Management/persistence boundary. A ProviderAccount requires an active Provider; an ExecutionTarget requires active Provider and ProviderAccount parents. Reactivation is therefore not equivalent to bypassing normal create/update validation. Durable uniqueness rules apply to the reactivated active record, so a key conflict with another active resource is surfaced as a conflict rather than silently replaced.

The UI should keep retired records discoverable for repair/history workflows, while active-only selectors used for new configuration continue to exclude retired resources. Lifecycle actions are explicit and visually distinguishable from ordinary CRUD operations.

Every Hive-owned persistent resource is represented through an immutable `ResourceEnvelope<TIdentity>` containing:

- resource identity;
- explicit owner (`PrincipalId`);
- explicit canonical scope (`Global`, `Tenant`, `User`, `Workspace`, `Agent`, `Runtime`, or `Execution`);
- positive resource version;
- provenance (creator principal, creation time, correlation/causation identifiers, and optional source resource reference);
- lifecycle metadata;
- immutable metadata values.

Scope matching is a structural boundary, not an implicit grant. The access context must always contain a `PrincipalId`; missing required identity components fail closed. The scope matrix is:

| Scope | Required context |
|---|---|
| Global | Deployment + Principal |
| Tenant | Deployment + Tenant + Principal |
| User | Deployment + Tenant + User + Principal |
| Workspace | Deployment + Tenant + Workspace + Principal |
| Agent | Deployment + Tenant + Agent + Principal |
| Runtime | Deployment + Tenant + Agent + Runtime + Principal |
| Execution | Deployment + Tenant + Agent + Runtime + Execution + Principal |

Scope matching does not itself grant authorization. Later management/security slices add resource-specific permissions and policy; they consume this explicit identity/scope boundary instead of replacing it.

`WorkItem` is the durable unit of user-visible work. Its identity is independent from Runtime and Execution identities. A WorkItem transition returns a new immutable state with the same WorkItem identity and a higher resource version; provenance and scope are preserved. When a business operation is required, one logical business operation is represented by one WorkItem, and that operation may require multiple executions or steps. A single input submission may produce one or multiple independent WorkItems. Related WorkItems may be grouped operationally as a submission or batch without replacing their independent identity, lifecycle, provenance, authorization, or any applicable operation receipt or Review state.

---



### 0.4 Persistence bootstrap

Phase 0.4 establishes the Hive-owned SQL Server persistence boundary without putting database dependencies into Hive.Core or the host application.

#### Ownership and dependency boundary

- Hive.Persistence owns all SQL Server connectivity, database bootstrap, migration execution, schema-version checks, and persistence-specific exceptions/results.
- Hive.Core remains dependency-light and has no SQL client, DbUp, or database connection-string dependency.
- Hive.Persistence may reference Hive.Core contracts, but Core never references Persistence.
- Hive's database is a separate database owned by Hive. It is never used as a gateway to the host application's business database.
- Persistence configuration is a first-class Hive platform configuration domain, not incidental host wiring.
- Hosts may provide initial/bootstrap persistence configuration, but the long-term authoritative configuration surface is Hive's own typed persistence-configuration contract and management/settings boundary.
- Credentials are not written to the repository, migration scripts, logs, or Hive database metadata. Hive resource credentials belong to the database-backed Secret Store boundary once Hive.Persistence is available. The SQL bootstrap credential is a separate user-scoped DPAPI-protected bootstrap-secret boundary outside the target Hive database and is referenced by persisted configuration rather than stored as plaintext.
- `HiveDatabaseOptions` keeps any credential-bearing SQL connection string as Persistence-internal state; public callers receive only non-secret configuration/metadata contracts. Raw credential-bearing connection strings must not cross the public Hive.Persistence API boundary.
- `HiveDatabaseOptions` enables database creation by default. Passing `createDatabaseIfMissing: false` is an explicit opt-out when the host requires pre-provisioned databases.

#### Database technology

- SQL Server is the only V1 persistence engine.
- SQL Server LocalDB is the supported local-development deployment of the same SQL Server boundary; it is not a separate persistence provider. LocalDB is not the future end-user embedded deployment profile; that remains a separate later portability/deployment capability.
- Microsoft.Data.SqlClient is used for SQL Server connectivity.
- DbUp SQL Server support is used for ordered schema migrations rather than hand-written migration orchestration.
- DbUp migrations are embedded SQL resources in Hive.Persistence, numbered in execution order, and executed transactionally per migration script.

#### First-class persistence configuration
Hive's persistence configuration is a product/platform configuration domain with the same separation of concerns as Provider configuration.

The authoritative configuration contract must represent, at minimum:
- selected persistence backend;
- SQL Server server/instance endpoint and port;
- authentication mode and non-secret login metadata;
- Hive database identity/name policy;
- SQL connection security options required by the supported deployment;
- Hive resource secret identity for resource credentials once Hive.Persistence is available;
- a separate bootstrap credential reference for SQL-password startup access, with protected material stored outside the Hive database;
- database creation/migration policy where exposed by the platform.

V1 has one persistence engine: SQL Server. LocalDB is a SQL Server deployment form for local development, not a second provider.

The configuration surface is intentionally separated from the low-level connection implementation:
- `Hive.Management` owns the management/configuration contract exposed to hosts and Settings UI;
- `Hive.Persistence` owns connection construction, database bootstrap, migration, schema inspection, and persistence-specific failures/results;
- WinForms configuration pages consume the Management contract and must not construct `SqlConnection` or embed SQL Server persistence rules;
- the persistence configuration page provides a non-destructive connection test and reports database/schema status separately from connection success;
- connection testing must not implicitly create a database or apply migrations;
- database initialization and schema migration remain explicit lifecycle operations in Hive.Persistence.

Running executions must use an immutable effective persistence configuration snapshot where a runtime operation depends on persistence settings, so later configuration edits cannot silently change an already-running operation.

The persistence configuration model must remain the single authoritative configuration model. Future configuration import/export must serialize that same contract rather than introduce a second database-configuration format.

The implementation currently pins dbup-sqlserver 7.2.0 and Microsoft.Data.SqlClient 7.1.0. The first is the current stable DbUp SQL Server package and the second is the current stable Microsoft SQL client at the time this slice is implemented. citeturn544673view0turn598125search0

#### Schema version and migration journal

Hive maintains two separate pieces of migration metadata:

1. DbUp's migration journal records which migration scripts completed successfully.
2. A Hive-owned singleton schema-version row records the current logical Hive database schema version.

The schema-version row is updated inside the same migration transaction as the schema change it describes. A failed migration therefore cannot advance the Hive schema version. A clean database has no Hive schema-version row until the first migration completes successfully.

The code has one supported CurrentSchemaVersion. Before running DbUp, Hive reads the stored logical schema version when present. A stored version greater than the code's supported version is an incompatible future schema and migration/execution is rejected before normal application use. Older supported versions are passed to DbUp for forward migration.

The DbUp journal and schema-version metadata use explicit primary/unique indexes for deterministic lookup. Domain-table indexes are added with the domain persistence slice that introduces each table rather than being guessed during this bootstrap phase.

#### Migration failure and compatibility semantics

Migration execution must be deterministic:

- clean database → ordered migrations → current schema;
- current database → no-op migration run;
- failed migration → failing script is not journaled and the logical schema version remains at the previous successful version;
- stored future schema version → typed incompatible-schema failure before normal migration proceeds;
- malformed or unavailable migration configuration → typed configuration/persistence failure rather than partial-success reporting.

A migration runner exposes the outcome as structured Hive.Persistence state instead of requiring callers to parse DbUp log strings.

#### Initial schema boundary

The bootstrap schema contains only persistence infrastructure required at this phase: Hive schema metadata and the DbUp journal. Agent, Hive, WorkItem, event-log, snapshot, outbox, provider, and other domain tables are introduced by the slices that own their persistence semantics.

This prevents Phase 0.4 from prematurely freezing later aggregate schemas while still establishing a real Hive-owned database, migration history, compatibility protection, and indexed metadata foundation.

---



### 0.5 Test harness

Phase 0.5 establishes reusable verification infrastructure without creating production abstractions that belong to later provider, Agent, or persistence slices.

#### Test boundaries

- `Hive.Tests` remains the authoritative automated test suite.
- Test doubles live in `Hive.Tests` unless a later production contract explicitly requires a reusable public fake package.
- No fake provider contract is added to `Hive.Core` or `Hive.Providers.OpenAICompatible` before the production provider boundary exists.
- Unit tests must not require SQL Server, network access, provider credentials, MAF services, or the WinForms host.
- Persistence integration tests are explicit boundary tests and may use the configured developer SQL Server/LocalDB database strategy.

#### Fake clock

`FakeClock` implements the existing `Hive.Core.IClock` contract. It starts at an explicitly supplied UTC instant and advances only through test-controlled operations. Tests therefore avoid sleeping or depending on wall-clock time for deterministic lifecycle/event assertions.

#### Test-only fake provider

`FakeProvider` is a deterministic test double owned by `Hive.Tests`. It records requests, returns configured responses, can produce configured failures, and honors cancellation without network access. Its request/response types are test-only and are not production provider contracts.

#### Persistence test strategy

Persistence integration tests use an explicit connection string stored in `Hive.Tests/HivePersistenceTestConfiguration.cs`, with the Hive test database names derived by each test. The tests create the database automatically when missing and may reuse a database only when the test explicitly resets the relevant schema. No environment variables or hidden machine-specific prerequisites are required.

#### Event test conventions

Event tests use fixed `DateTimeOffset` values, deterministic typed IDs, and explicit payload JSON. Serialization round-trips compare contract fields rather than incidental JSON property ordering. Upcast tests construct the exact older payload version and verify the normalized current contract.

Test infrastructure is verification support only. It does not become a second runtime, provider, persistence, or orchestration architecture.



### 0.6 WinForms UI/UX Foundation

Phase 0.6 establishes the shared WinForms visual foundation used by `Hive.Host.WinForms` and `Hive.Example.WinForms`. The rendering dependency is an implementation detail of `Hive.Host.WinForms.UI`.

#### UI implementation boundary

- `Hive.Host.WinForms.UI` owns Hive's WinForms presentation implementation.
- The current controls use native WinForms behavior and custom System.Drawing rendering; no third-party rendering dependency is currently used.
- Consuming forms and platform services reference Hive-owned UI contracts and do not depend on a renderer implementation detail.
- A future renderer may be introduced only through this boundary after an explicit architectural decision; it must not leak into consuming projects.

#### Theme contract

The UI foundation exposes a Hive-owned theme vocabulary:

- `HiveThemeMode`: `Light`, `Dark`, `System`;
- semantic palette roles for application background, surface, elevated surface, text, muted text, border, accent, accent-hover, accent foreground, input, disabled input, disabled text, and selection;
- typography roles for body and heading text;
- spacing tokens for the common 4/8/12/16/24 pixel scale;
- common visual-state tokens for normal, hover, pressed, focused, disabled, and selected states.

Theme resolution is deterministic. `System` resolves from the Windows application-theme preference when available and falls back to Light when the OS setting cannot be read.

The theme manager is stateful but UI-only. Changing the mode raises one theme-change notification. Consumers reapply the effective theme to their attached control trees in response to that notification. The manager does not own application settings, persistence, Agent/Hive state, or host business data.

#### Reusable data-oriented UI composition

Hive reuses presentation and CRUD orchestration mechanics for data-oriented pages without assigning domain meaning to them. The reusable foundation may provide:
- a three-region list-page layout (header, action/filter region, content);
- a structured CRUD toolbar with a clear primary action, contextual edit/delete actions, refresh, and optional client-side search;
- a styled native `ListView`-based tabular/list surface where its native behavior is sufficient, with predictable selection and keyboard interaction;
- explicit loading, empty, and no-match states plus a compact record-count/status footer;
- a compact paging/navigation footer using `HivePaginationBar`, owning page state and navigation events while remaining independent of data retrieval;
- a generic `HiveCrudPage<TItem>` that owns Add/Edit/Delete/Refresh UI orchestration, selection, list population, search state, client-side page slicing over its loaded snapshot, busy-state, empty-state presentation, keyboard interaction, and operation failure notification while receiving load/edit/delete callbacks from the consuming feature;
- reusable editor-layout composition for the repeated label/description + field + action-footer pattern, with consistent field spacing and a dedicated action footer;
- optional master/detail composition where a bounded list and selected-item details are useful.

The reusable page follows a compact application UX hierarchy: page title/description first, a single-row search/action toolbar second, the primary data surface immediately below it, and a compact status/pagination footer last. The action toolbar must not reserve unnecessary vertical space. destructive actions use semantic danger treatment and require confirmation. The reusable editor keeps domain validation and authorization outside the layout while providing a consistent field rhythm, readable labels/descriptions, and right-aligned primary/secondary actions.

`HiveCrudPage<TItem>` is a UI/application-boundary primitive, not an ORM or persistence abstraction. The consumer supplies columns/projections, filters, validation, authorization, persistence, and the domain-specific editor through callbacks or composition. The control does not know Provider, Agent, Resource, or any other domain schema, and it never creates or mutates domain state on its own. This lets Provider, Agent, Tool, Policy, and future configuration pages share the same CRUD interaction contract while retaining different columns and specialized edit forms.

`DataGridView` remains available when its richer native tabular behavior is specifically required. Hive does not introduce a generic ORM, repository, or domain CRUD model.
WinForms DPI behavior is delegated to the .NET 10 / WinForms platform rather than duplicated in Hive. Hive does not maintain a custom DPI helper or manual control-tree scaling layer. Normal forms and controls use WinForms' built-in scaling behavior; custom-painted Hive controls keep their own design geometry unless a concrete, measured DPI defect requires a focused exception.


#### Internal `HiveCrudPage<TItem>` implementation separation

`HiveCrudPage<TItem>` remains the stable consumer-facing generic CRUD control. Its public responsibility is reusable CRUD interaction, not ownership of a single monolithic implementation.

The concrete implementation should separate the mechanical concerns that have independent change pressure:

```
HiveCrudPage<TItem>
   ├── operation lifecycle / cancellation / busy state
   ├── item filtering / selection / paging / projection
   └── layout / theme / responsive presentation
```

These components are internal implementation details and may use callbacks supplied by the page. They must not become a second public UI framework, ORM, domain CRUD model, or replacement for the existing Hive UI contracts.

The refactored page must preserve the existing consumer-facing API and behavior, including search/filter semantics, stable selection where applicable, paging, Add/Edit/Delete/Activate/Refresh callbacks, cancellation and stale-operation protection, error reporting, theme changes, compact/normal layout behavior, disposal, and the existing `HiveCrudPage<TItem>` ownership model.

#### Hive-owned controls and window shell

The foundation introduces only consumer-facing Hive contracts:

- `HiveForm` provides the shared rounded, borderless application-window shell, custom header, window movement, and theme-aware body surface.
- `HiveButton` provides a Hive-owned button surface with Primary, Secondary, Navigation, selected, and semantic danger states while keeping its rendering implementation internal to `Hive.Host.WinForms.UI`.
- `HiveMessageBox` provides a Hive-owned semantic dialog with Information, Success, Warning, Error, and Question variants, standard `DialogResult` semantics, optional technical details, and copy support. It uses a compact dialog-specific shell rather than the full `HiveForm` application header: rounded surface, thin semantic accent bar, semantic circular icon, clear caption/message hierarchy, optional details panel, and right-aligned action buttons.
- `HiveForm` provides the reusable borderless rounded application-window shell and body surface.
- The custom application header supports title, subtitle, close, optional minimize/help actions, and window movement with a compact visual hierarchy similar to the established HAgent WinForms visual language.
- `HiveMessageBox` may take visual direction from the useful HAgent message-dialog characteristics, but its implementation remains smaller, Hive-owned, theme-driven, and free of HAgent dependencies.
- Semantic message colors and navigation colors are theme tokens rather than form-specific constants.

The visual direction intentionally carries forward the useful HAgent characteristics—rounded windows, a distinctive header, strong semantic accents, compact navigation, and explanatory field labels—without copying HAgent's monolithic UI implementation. Hive keeps the implementation smaller, theme-driven, disposable, and independent of HAgent types.

Ordinary WinForms controls remain first-class. The foundation styles common native controls through the theme manager where practical; it does not create Hive-prefixed wrappers merely to rename every framework control.

#### Representative verification surface

A representative example form in `Hive.Example.WinForms` exercises:

- Light, Dark, and System mode selection;
- a HiveButton;
- a HiveMessageBox;
- representative native WinForms controls such as labels, text input, check boxes, and panels;
- disabled/focused/selected visual states where the control supports them.

The example form is the manual UI verification surface for this phase. No UI automation framework is introduced. Its navigation and content areas must use standard WinForms layout containers rather than overlapping absolute child placement so the example remains readable at its supported window sizes.

#### Replaceability contract

The public consumer surface consists only of Hive-owned theme contracts and the small Hive-specific controls introduced by this phase. Consuming forms must remain independent of the UI rendering implementation, so future rendering changes remain isolated to `Hive.Host.WinForms.UI`.

#### Scope boundary

Phase 0.6 establishes visual infrastructure only. It does not introduce Hive membership, Swarm, Agent/Hive runtime behavior, cognitive generations, Dreams, Questions, or Workspace feature behavior.
---