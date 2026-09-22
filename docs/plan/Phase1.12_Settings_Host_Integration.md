# Phase 1.12 — Settings, Configuration, and Real Host Consumption Plan

## Purpose

Phase 1.12 currently has a first Settings surface, Provider/Persistence configuration, Secret Store integration, and connection tests. The remaining work is to make the configuration system behave like a real Hive application configuration system, following the proven HAgent model.

The target is not merely a better Settings screen.

The target is:

- Settings edits the same authoritative Hive state that the application uses.
- Provider, ProviderAccount, ExecutionTarget, and AgentDefinition resources are real persisted configuration.
- The host application is composed from the saved persistence configuration rather than hard-coded `LocalDevelopment()`.
- The Example Host behaves like a normal Hive consumer.
- The Example Host exposes Settings as a normal application capability.
- Normal configured examples consume the configured Agent/Provider/ExecutionTarget state.
- The configured runtime behavior itself proves that Settings works.
- The Hive-owned WinForms UI foundation is reused instead of creating a second Settings-specific navigation/list toolkit.

This remains **Phase 1.12** work. It does not authorize Phase 1.13 or later.

---

## 0. Global Package Configuration Principle

Hive Settings is the **global Hive package configuration center**. It is not a Provider Settings screen, Persistence Settings screen, or a collection of unrelated administrative dialogs.

The Settings surface is a permanent first-class application boundary through which the configuration of the entire Hive package is managed as the corresponding capabilities become available.

Conceptually:

```text
Hive Configuration
├── Overview
├── Providers
├── Agents
├── Persistence
├── Tools
├── Policy / permissions
├── Workspace / application behavior
├── Runtime / execution defaults
├── cognition/resource configuration
├── integration/host configuration
├── diagnostics/configuration inspection
└── other Hive-owned configuration domains
```

The exact pages are introduced with the phases that own their configuration contracts. A future page must be added to this global configuration center; it must not create a second top-level configuration mechanism.

### Configuration-center rules

1. There is one canonical Hive Settings/Configuration entry point for the package.
2. Configuration domains are owned by their respective platform contracts and services, but their user-facing configuration surface belongs under the global Settings center.
3. Settings navigation is extensible and must use the reusable `Hive.Host.WinForms.UI` navigation foundation.
4. A configuration page may be absent while the underlying capability does not yet have an authoritative configuration contract; the page is not a reason to invent configuration state early.
5. A capability-specific dialog may still exist when it is an execution-time action rather than durable package configuration, but durable package configuration belongs in the global Settings center.
6. The Example Host must expose the same global Settings experience a normal Hive WinForms application would consume.
7. The global Settings center itself is not an Example scenario. The Example scenario demonstrates configured behavior using the resulting state.
8. Adding future package configuration domains must extend this same center instead of creating parallel `FooSettingsForm` roots.

### Current Phase 1.12 domains

Phase 1.12 establishes the first concrete global configuration domains that already have authoritative contracts:

- Providers;
- ProviderAccounts and ExecutionTargets as Provider configuration subdomains;
- AgentDefinitions and their configured execution relationship;
- Persistence;
- the Settings shell/navigation infrastructure needed to host future Hive configuration domains.

Later phases add their own domains under this same configuration center without changing the architectural ownership rule.

## 1. Target Architecture

The intended application flow is:

```
Saved Hive configuration
        ↓
Application composition root
        ↓
Configured Hive.Persistence services
        ↓
Hive.Management facade
        ↓
Host application features
        ↓
Actual configured Hive resources / execution
```

Settings is a management client over that same boundary:

```
Hive Settings UI
        ↓
IHiveManagementFacade
        ↓
Hive.Persistence / provider boundary
        ↓
authoritative persisted state
```

The Example Host follows the same model:

```
Hive.Example.WinForms
        ↓
public Hive contracts
        ↓
IHiveManagementFacade / configured execution services
        ↓
real persisted Providers / Accounts / Targets / Agents
```

There must not be a second Example-only configuration model.

---

## 2. HAgent Pattern Being Reused

HAgent establishes the desired behavioral pattern:

1. One real configuration surface manages Providers and Agents.
2. Configuration uses the authoritative application store.
3. The Example application has a normal Configuration entry.
4. Closing configuration causes the host to reload its current configured resources.
5. Normal Example operations resolve the configured Agent and its execution configuration before doing work.
6. Storage configuration is real application configuration and can cause the host to recompose its runtime storage.
7. A configuration-reading example can exist, but it is not the primary proof.
8. Secrets remain outside ordinary configuration data and are never printed.
9. UI is responsible for composition/presentation; storage, security, provider transport, and runtime composition remain outside the UI.

Hive should reproduce these architectural behaviors using its stronger typed Management/resource boundaries.

---

## 3. Hive Resource Model

Hive already has explicit configuration resources:

```
Provider
    ↓
ProviderAccount
    ↓
ExecutionTarget

AgentDefinition
    ↓
configured execution relationship
```

Settings must preserve those relationships.

Provider settings must not collapse:

- Provider identity;
- ProviderAccount identity;
- credential reference;
- ExecutionTarget identity;
- endpoint;
- model/deployment;
- capability state

into one UI-only object.

Agent settings must operate on persisted `AgentDefinition` resources and their configured execution relationship. They must not manufacture hidden provider configuration outside Management.

Resource ownership, scope, version, lifecycle, concurrency, and authorization remain Management concerns.

---

## 3A. Current Agent Configuration Gap\n\nThe current AgentDefinition contract contains identity, display name, and generation, but no persisted execution-selection relationship. The current first-real-execution boundary instead receives an ExecutionTarget directly in AgentExecutionRequest.\n\nThat is insufficient for the target Settings behavior because an Agent selected by the host must carry durable configuration that identifies what it should use.\n\n### Required Phase 1.12 correction\n\nExtend the Agent configuration boundary so an AgentDefinition can persist its configured execution-target selection without duplicating Provider/ProviderAccount/ExecutionTarget data.\n\nThe configuration must reference an existing ExecutionTargetId (or an equivalent explicit selection contract) rather than storing:\n\n- provider endpoint;\n- provider key;\n- account credentials;\n- model/deployment;\n- capability state\n\ninside the AgentDefinition.\n\nThe existing ExecutionTarget remains authoritative for those details.\n\nThe configured-agent path therefore becomes:\n\n```text\nAgentDefinition\n    └── configured ExecutionTarget reference\n             ↓\n      Management loads target\n             ↓\n      existing target contains Provider/Account/Endpoint/Model\n             ↓\n      execution boundary receives the resolved target\n```\n\n### Selection boundary\n\nDo not build a second provider/model selection algorithm inside Settings.\n\nUse the existing execution-target identity/selection contracts as the foundation. For the first configured-host flow, an explicit configured target reference is sufficient. More advanced automatic/preferred selection remains owned by the existing execution-target selection architecture and must not be reimplemented in WinForms.\n\nIf the implementation needs a broader persisted selection policy than one target reference, that decision must remain in the Agent/Coordination contract and be documented before coding; it must not be hidden in UI state or metadata JSON.\n\n### Persistence implications\n\nAdding a durable AgentDefinition → ExecutionTarget relationship is a persistence contract change.\n\nThe implementation must therefore include:\n\n- an ordered migration;\n- an appropriate foreign-key/index strategy where compatible with the existing lifecycle model;\n- load/save/update behavior in SqlAgentDefinitionResourceStore;\n- Management validation that the referenced target belongs to the allowed access context;\n- stale/missing/retired target handling;\n- concurrency behavior;\n- focused tests.\n\nThis is still Phase 1.12 because it is required to make the new Settings configuration actually drive a configured Agent.\n\n## 4. WinForms UI Foundation Requirement

The Settings surface **must use the reusable controls already designed and owned by `Hive.Host.WinForms.UI`**.

Required controls/patterns:

### Navigation

Use:

```
HiveNavigationTree
```

Settings must not implement its own owner-drawn `ListBox`, custom tree renderer, or parallel navigation mechanism.

The Settings hierarchy should be represented as:

```
Settings
├── Providers
├── Agents
└── Persistence
```

Additional sub-levels may be represented through the established tree/navigation model when they provide useful architectural grouping.

The existing `HiveNavigationTree` theme behavior must be preserved, including selection state and scroll position during theme changes.

### List / CRUD

Use:

```
HiveListPageLayout
HiveCrudPage<TItem>
HiveListView
```

for list-oriented Settings surfaces.

The existing Hive CRUD presentation standard remains authoritative:

- header;
- action bar;
- list/content area;
- search where useful;
- predictable empty/no-match states;
- keyboard-friendly selection;
- Add/Edit/Delete/Refresh behavior;
- compact record/status feedback;
- responsive action layout.

Do not create a second Settings-specific ListView renderer or another generic CRUD abstraction.

### Editors

Use:

```
HiveEditorLayout
HiveButton
HiveMessageBox
```

where the existing UI contracts fit.

Domain-specific validation and field semantics remain in the Settings page/editor; the reusable controls remain presentation infrastructure.

### Theme

Use `IHiveThemeManager` and the same Light/Dark/System theme behavior as the rest of Hive.

Settings must not own an independent color palette, typography system, selected-state implementation, or theme-change mechanism.

---

## 5. 1.12-A — Host Configuration and Runtime Composition

### Goal

Make saved `HivePersistenceConfiguration` actually control how the host constructs Hive.

### Current problem

The Example Host currently constructs:

```csharp
HiveDatabaseOptions.LocalDevelopment()
```

inside its service composition.

That means the new persistence Settings can save a different configuration while the running application continues to use the hard-coded default.

That must be removed from the normal runtime composition path.

### Required work

Create one application-composition path that:

1. loads the saved persistence configuration;
2. falls back to typed `LocalDevelopment()` only when no saved configuration exists;
3. resolves the persistence bootstrap credential before Hive DB access;
4. creates `HiveDatabaseOptions.FromConfiguration(...)`;
5. constructs the configured Persistence stores;
6. constructs `IHiveManagementFacade` from those stores and provider boundaries;
7. exposes the configured Management/runtime services to the host;
8. disposes/replaces the old persistence-backed services when configuration changes.

`LocalDevelopment()` remains the first-run default, not a permanent runtime wiring shortcut.

### Failure boundaries

The composition layer must distinguish:

- invalid configuration;
- missing bootstrap credential;
- database/server unavailable;
- database missing;
- schema not initialized;
- schema requires migration;
- future/unsupported schema;
- invalid provider configuration;
- missing selected Agent;
- unavailable ExecutionTarget;
- missing provider credential;
- provider connection failure.

UI may translate these into user-friendly messages, but must not merge them into a single generic implementation failure.

---

## 6. 1.12-B — Bootstrap Credential Boundary

### Problem

Hive's current Secret Store is SQL-backed.

A SQL password is required to establish the database connection that is needed to access that Secret Store.

Therefore:

```
SQL password
   → needed to open Hive DB
   → needed to open Hive Secret Store
   → cannot retrieve SQL password from that DB first
```

This is a circular bootstrap dependency.

### Required solution

Introduce a separate bootstrap-secret boundary for the persistence connection credential.

The bootstrap credential must:

- live outside the target Hive database;
- use Windows DPAPI protection;
- be user-scoped;
- never be written as plaintext into `hive-settings.json`;
- be available before Hive.Persistence is constructed;
- expose only the minimum API required by the application composition root;
- never appear in diagnostics, Example output, or normal configuration serialization.

This should follow the architectural idea already used by HAgent's file-backed protected secret storage.

### Separation of responsibilities

There are therefore two secret classes:

**Bootstrap secret**

Used to open Hive persistence.

**Hive resource secret**

Used after Hive persistence is open, for ProviderAccount credentials and other Hive-owned secrets.

The existing `SqlDpapiSecretStore` remains authoritative for Hive-owned durable resources. The bootstrap secret must not be stuffed into `HiveSecrets` before the database exists.

---

## 7. 1.12-C — Real Settings Management Surface

### Target navigation

```
Settings
├── Providers
│   ├── Provider list
│   ├── Provider editor
│   ├── ProviderAccount management
│   └── ExecutionTarget management
├── Agents
│   └── AgentDefinition list/editor
└── Persistence
    └── SQL Server / LocalDB settings
```

An Overview page is optional only if it adds real application value; it must not exist merely to display counts that are not otherwise useful.

### Provider surface

The Provider area must allow a normal host administrator/user to:

- view Providers;
- create/edit/retire Providers according to Management lifecycle rules;
- view ProviderAccounts for a Provider;
- create/edit/retire ProviderAccounts;
- associate credential references;
- view ExecutionTargets for an Account;
- create/edit/retire ExecutionTargets;
- execute the supported connection test through Management;
- see connection/test state without revealing credentials.

The page should present the actual resource relationships rather than flattening all three resource types into unrelated lists.

### Agent surface

The Agent area must:

- list AgentDefinitions;
- create/edit/retire AgentDefinitions;
- show configured execution selection clearly;
- allow selection of valid existing execution resources;
- preserve Management authorization, version, lifecycle, and concurrency behavior;
- expose effective configuration that is meaningful to a host consumer.

An Agent must not silently create provider/account/target records merely because an editor needs a selection.

### Persistence surface

The Persistence area must continue to support:

- SQL Server;
- SQL Server LocalDB through the same SQL Server boundary;
- Windows Integrated authentication;
- SQL password authentication;
- server/instance;
- port;
- database;
- encryption settings;
- trust-server-certificate policy;
- create-database-if-missing policy;
- command timeout;
- bootstrap credential reference.

The connection test remains non-destructive:

- no database creation;
- no migration;
- no schema mutation;
- explicit database/schema state.

Schema initialization/migration remains a separate lifecycle operation.

---

## 8. 1.12-D — Refactor Settings UI onto Hive UI Controls

The existing Settings implementation currently uses a custom Settings navigation `ListBox` and page-local layout/list patterns.

Replace that implementation with the existing Hive UI foundation.

### Required direction

```
HiveSettingsView
    ↓
HiveNavigationTree
    ↓
replaceable Settings page

Provider page
    ↓
HiveListPageLayout
    ↓
HiveCrudPage<TItem> / HiveListView
    ↓
domain editor

Agent page
    ↓
HiveListPageLayout
    ↓
HiveCrudPage<TItem> / HiveListView
    ↓
domain editor

Persistence page
    ↓
HiveEditorLayout
```

Do not copy or reimplement the behavior already provided by:

- `HiveNavigationTree`;
- `HiveListView`;
- `HiveListPageLayout`;
- `HiveCrudPage<TItem>`;
- `HiveEditorLayout`.

If an existing control lacks a genuinely required behavior, extend the reusable UI control only when the behavior belongs to the reusable UI boundary. Do not add a one-off Settings-only renderer.

---

## 9. 1.12-E — Settings as Real Application State

Settings must edit the same persisted state used by the rest of the host.

Required behavior:

1. Open Settings.
2. Load current persisted Providers, Accounts, Targets, Agents, and Persistence configuration.
3. Change configuration.
4. Save through Management.
5. Close Settings.
6. Host reloads/recomposes the affected state.
7. Subsequent normal operations use the new state.

There must be no "Settings saved" state that the running application ignores.

### Change classes

**Persistence-boundary changes**

Examples:

- server;
- port;
- database;
- authentication mode;
- bootstrap credential;
- encryption/connection security.

These require persistence service recomposition/reopen.

**Resource changes**

Examples:

- Provider;
- ProviderAccount;
- ExecutionTarget;
- AgentDefinition.

These only require state refresh from Management; they must not cause unnecessary reconstruction of the entire persistence stack.

### Running executions

A running execution uses its already-established effective configuration snapshot.

Settings changes affect subsequent work, not an execution that has already captured its effective configuration.

---

## 10. 1.12-F — Example Host as a Real Consumer

The Example Host must behave like an ordinary application.

### Host-level Settings

Provide a normal application Settings/Configuration entry in the Example Host shell.

It must not require the user to navigate to a special Settings Example just to configure Hive.

The Settings Example may still exist as a public API/verification scenario for inspecting the configuration boundary, but it is secondary to the host configuration experience. The global Settings center itself is host infrastructure, not a leaf Example.

### Example startup

At startup the Example Host must:

1. load persisted Hive persistence configuration;
2. compose the configured Management/runtime services;
3. load Providers and AgentDefinitions;
4. populate the configured Agent selection UI;
5. report a useful status when configuration cannot be loaded.

### After Settings closes

The host must:

1. detect persistence configuration changes;
2. recompose persistence-backed services when required;
3. reload Providers/AgentDefinitions;
4. preserve the selected Agent when it still exists;
5. clear/revalidate the selection when it no longer exists or is unusable.

### Normal examples

Examples that require configured execution should use the selected persisted AgentDefinition and its configured execution resources through public Hive APIs.

The intended flow is:

```
Settings
  ↓
Provider
  ↓
ProviderAccount + credential
  ↓
ExecutionTarget
  ↓
AgentDefinition
  ↓
close Settings
  ↓
select configured Agent
  ↓
run normal Agent scenario
  ↓
Hive uses configured target
```

The successful configured operation is part of Settings verification.

---

## 11. 1.12-G — Example Classification

Do not blindly convert every existing Example into configuration-dependent behavior.

Keep two categories.

### Isolated contract examples

Used to demonstrate deterministic low-level behavior.

They may:

- use local fake provider infrastructure;
- create temporary resources;
- use dedicated example databases;
- avoid external credentials.

They prove a specific contract, not host configuration.

### Configured-host examples

Used to prove that Hive configuration drives real application behavior.

They should:

- read persisted Providers/Accounts/Targets/Agents;
- select a configured Agent;
- run through public Hive boundaries;
- expose meaningful configured-resource output;
- fail clearly when required configuration is absent.

The strongest Settings proof is the second category.

Existing examples that hard-code `HiveDatabaseOptions.LocalDevelopment()` must be reviewed and classified. They should only be changed when the example's purpose requires actual host configuration.

---

## 12. 1.12-H — Tests

Focused automated tests must cover the configuration/composition boundary.

### Persistence configuration

- default configuration behavior;
- save/load round-trip;
- invalid configuration;
- no plaintext credential serialization;
- bootstrap credential storage/retrieval;
- missing bootstrap credential;
- `FromConfiguration` behavior for each supported authentication mode.

### Composition

- configured Persistence stores are built from saved configuration;
- LocalDevelopment is only the default path;
- configured database information is propagated to the composition boundary;
- unavailable persistence produces typed failure;
- replacing persistence configuration does not retain stale stores;
- owned persistence services are disposed when replaced.

### Management/resource configuration

- Provider CRUD;
- ProviderAccount CRUD;
- ExecutionTarget CRUD;
- AgentDefinition CRUD;
- ownership/scope enforcement;
- lifecycle/retirement;
- version/concurrency rules;
- invalid relationships;
- missing referenced resources.

### Settings integration

- Settings save is visible through the next Management read;
- Provider/Agent lists reload after Settings changes;
- persistence changes trigger recomposition;
- resource-only changes do not unnecessarily rebuild persistence;
- selected Agent is preserved when valid;
- selected Agent is cleared when retired/missing/disabled where required.

### Security

- bootstrap credential absent from JSON;
- bootstrap credential absent from diagnostics;
- Hive resource credential absent from normal output;
- Secret Store material is only resolved at the necessary boundary;
- no password appears in Example output.

### Connection tests

- non-destructive persistence connection test;
- database-state classification;
- schema-state classification;
- provider connection-test boundary;
- cancellation and transport failure cases appropriate to the implementation.

---

## 13. 1.12-I — Example Verification

The Example Host must provide an externally meaningful configured flow.

Primary manual scenario:

```
Settings
 → configure Persistence
 → configure Provider
 → configure ProviderAccount
 → configure credential
 → configure ExecutionTarget
 → configure AgentDefinition
 → close Settings
 → select Agent
 → run configured Agent example
```

Developer must verify:

- saved persistence settings are actually used;
- Providers reload;
- AgentDefinitions reload;
- selected Agent reflects saved state;
- configured ExecutionTarget is the one used by the normal operation;
- changing the configured target changes later operations;
- disabling/retiring the selected resource is handled clearly;
- missing configuration produces actionable failure;
- no secret material appears in Example output.

The existing Settings Example can remain for direct configuration-contract inspection, but `Capture configuration` alone does not establish completion.

---

## 14. 1.12-J — UI/UX Review Requirements

Because Settings is a permanent application surface, it must use the same desktop UI standards as the rest of Hive.

Review:

- Settings navigation hierarchy;
- Category/group/leaf clarity;
- header/action/content rhythm;
- typography;
- density;
- resizing;
- editor spacing;
- ListView column sizing;
- empty/loading/error states;
- Add/Edit/Delete presentation;
- selected/hover/disabled states;
- Light/Dark/System;
- preservation of TreeView selection and scroll position on theme changes;
- correct disposal of replaced pages/editors;
- no duplicate renderers or theme logic;
- no layout jumps when switching pages or theme.

Use the existing `HiveNavigationTree` behavior specifically to preserve navigation state.

Use `HiveCrudPage<TItem>` and `HiveListView` specifically to avoid reintroducing the UI problems already solved by the shared foundation.

---

## 15. 1.12-K — Documentation

Before structural implementation changes:

- update `docs/architecture.md` with the host configuration/composition boundary and bootstrap-secret distinction;
- update `docs/ui/forms.md` with the final Settings page/control composition;
- update `docs/ui/examples.md` with the configured-host Example usage and exact navigation path;
- keep `docs/Hive_Active_Work.md` synchronized with the current 1.12 sub-stage and verification gate.

Do not mark `docs/Hive_Current_Status.md` complete until the user has actually verified the required flow.

Create the Phase 1 verification record only after actual verification.

---

## 16. Explicit Non-Goals

This workload does not implement:

- Phase 1.13 Image Input / Host Context;
- Business-App Integration;
- Vision Routing;
- Structured Extraction;
- Business-App Write Tool;
- the full MAF sequential V1 pipeline;
- configuration import/export/portability;
- multi-user authentication;
- future cognitive generations;
- a second orchestration engine;
- a second WinForms control toolkit.

---

## 17. Completion Gate

Phase 1.12 remains open until all of the following are true:

1. Saved persistence configuration controls the actual host persistence boundary.
2. SQL-password bootstrap credentials can be resolved without accessing the target Hive database first.
3. Settings uses `Hive.Host.WinForms.UI` controls, especially `HiveNavigationTree`, `HiveListPageLayout`, `HiveCrudPage<TItem>`, and `HiveListView` where applicable.
4. Provider, ProviderAccount, ExecutionTarget, and AgentDefinition configuration is authoritative Management state.
5. Settings has no direct SQL/provider transport/migration logic.
6. The Example Host has a normal Settings entry.
7. The Example Host loads Providers and Agents from the configured persistence boundary.
8. A configured Agent can be selected and used by a normal Example operation.
9. Changing Settings changes subsequent application behavior.
10. Persistence changes cause correct service recomposition; resource changes cause refresh without unnecessary persistence reconstruction.
11. Running executions retain their existing effective configuration snapshot.
12. Bootstrap credentials and Hive resource credentials never appear in plaintext configuration or diagnostics.
13. Focused tests cover the new boundaries.
14. The required Example scenario exists and uses public Hive APIs.
15. Manual developer verification succeeds.
16. Broader `Hive.Tests` verification succeeds.
17. `Hive_Active_Work.md` and status documentation reflect actual verification results.

---

## 18. Implementation Order

The implementation should be performed as separate bounded sub-stages so each change remains reviewable:

1. **1.12-A — Host configuration/composition boundary**
2. **1.12-B — Bootstrap credential boundary**
3. **1.12-C — Settings Management surface/resource editing**
4. **1.12-D — Settings UI migration to Hive UI foundation**
5. **1.12-E — Settings-driven runtime state/reload**
6. **1.12-F — Example Host configured-consumer integration**
7. **1.12-G — Example classification and configured examples**
8. **1.12-H — Focused automated coverage**
9. **1.12-I — Manual configured-host verification**
10. **1.12-J — Final UI/UX review**
11. **1.12-K — Documentation/status closure**

A sub-stage is not complete merely because source code exists. Use the repository's verification gate and user-provided runtime results before closing the corresponding work.

## 19. Guiding Rule

**Settings must configure Hive, not merely display Hive configuration. The same authoritative state edited by Settings must drive the host application's subsequent behavior, and the Example Host must prove that through normal public APIs.**
