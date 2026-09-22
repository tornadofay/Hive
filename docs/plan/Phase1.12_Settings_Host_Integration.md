# Phase 1.12 — Settings, Configuration, and Real Host Consumption Plan

## Purpose

Phase 1.12 turns Hive Settings into the **global Hive package configuration center** and makes the saved configuration actually drive the host application.

The target is not a better Settings screen. The target is one authoritative configuration path:

\`\`\`
Saved Hive configuration
        ↓
Host composition boundary
        ↓
Configured Hive services
        ↓
Hive.Management
        ↓
Normal host operations / execution
\`\`\`

Settings edits authoritative state; the host consumes that same state.

This remains **Phase 1.12** only. It does not authorize Phase 1.13 or later.

---

## 0. Global Package Configuration Principle

Hive Settings is the permanent first-class configuration center for the Hive package.

It is not:

- a Provider Settings window;
- a Persistence Settings window;
- a collection of unrelated \`FooSettingsForm\` roots;
- a configuration-inspection example.

Conceptually:

\`\`\`
Hive Settings
├── Overview
├── Providers
│   ├── Provider Configuration
│   ├── Accounts / Credentials
│   └── Execution Targets
├── Agents
├── Persistence
├── Tools
├── Policy / permissions
├── Workspace / application behavior
├── Runtime / execution defaults
├── Cognition / resource configuration
├── Integration / host configuration
└── other Hive-owned durable configuration
\`\`\`

Only domains with an authoritative contract are implemented in the phase that owns them. Future durable Hive configuration extends this same center.

A UI page is therefore a consumer of an existing configuration contract, not the owner that creates one. Settings must not invent durable state merely because a category appears in the global navigation.

### Rules

1. One canonical Hive Settings entry point exists for the package.
2. Durable package configuration belongs under that entry point.
3. Domain ownership remains in Management/Core/Persistence/etc.; Settings is the application-facing configuration surface.
4. The Settings shell must be extensible without creating parallel top-level settings forms.
5. The Example Host exposes the same Settings experience a normal Hive WinForms application consumes.
6. The Settings shell itself is infrastructure; Example scenarios prove behavior produced by configured state.
7. Read-only diagnostics/configuration inspection may exist under Settings, but it is not a replacement for real configuration consumption.

### Concrete Phase 1.12 domains

- Providers;
- Provider Accounts;
- Execution Targets;
- AgentDefinitions and their configured execution relationship;
- Persistence;
- the reusable Settings navigation/editor infrastructure.

---

## 1. Current Checkpoint and Authorized Scope

The first Phase 1.12 Settings implementation already exists and is verification-pending.

The current authorized sub-stage is:

**1.12-D — Settings UI on the Hive UI Foundation**

1.12-A — Host Configuration and Runtime Composition is complete and verified.
1.12-B — Bootstrap Credential Boundary is complete and verified.
1.12-C — Real Settings Management is complete and verified.

Only implement 1.12-D and supporting contracts required to make 1.12-D complete.

1.12-C must build on the authoritative Management/Core contracts and the completed host/bootstrap boundaries. Do not implement later Settings UI migration, runtime recomposition, configured-host Example, or final verification/closure work in the same run unless it is a direct dependency of 1.12-C.

Tests and examples are not a final-phase activity: each sub-stage adds the focused automated coverage and externally usable Example work required by the capability it actually introduces. 1.12-H is the final coverage consolidation/audit, not permission to defer all testing until then.

The ordered sub-stages are:

1. 1.12-A — Host composition boundary
2. 1.12-B — Bootstrap credential boundary
3. 1.12-C — Management/settings resource editing
4. 1.12-D — Settings UI migration to Hive UI foundation
5. 1.12-E — Settings-driven reload/recomposition
6. 1.12-F — Example Host configured consumption
7. 1.12-G — Example classification
8. 1.12-H — Focused automated coverage
9. 1.12-I — Manual configured-host verification
10. 1.12-J — Final UI/UX review
11. 1.12-K — Documentation/status closure

A sub-stage closes only after its required implementation, tests/examples/docs, and actual verification are complete.

---

## 2. Target Architecture

The host application layer must have one composition/lifetime boundary that owns the current Hive service graph. `Hive.Example.WinForms` consumes that boundary; it must not become the architectural owner of Hive runtime composition.

\`\`\`
persisted Hive configuration + bootstrap credential boundary
              ↓
      host composition boundary
              ↓
      persistence-backed stores
              ↓
       HiveManagementFacade
              ↓
          host services / execution
\`\`\`

Settings uses the public configuration/Management boundaries rather than accessing SQL, provider transport, migrations, or secret material directly.

### Composition invariant

A replacement service graph is built as a complete candidate before publication:

\`\`\`
current graph
    ↓
load + validate new configuration
    ↓
resolve required bootstrap material
    ↓
construct candidate graph
    ↓
publish candidate
    ↓
dispose previous graph
\`\`\`

Never publish a partially constructed graph.

Never destroy the currently usable graph merely because a replacement failed.

On startup, failure to construct the configured graph produces an explicit unavailable/unconfigured state rather than silently switching to a different saved configuration or overwriting the saved configuration.

### Concurrency/lifetime rules

- Settings changes and startup composition use one serialized reconfiguration boundary.
- Only the current published graph is used for new host operations.
- Components owned by the graph are disposed exactly once when replaced.
- Running executions retain their already-established effective configuration snapshot.
- A later Settings change never mutates an execution already in progress.

---

## 3. Persistence Configuration Rules

The persisted Hive configuration is bootstrap-readable without requiring the target Hive database to be open.

Important startup rule:

- **No saved configuration exists:** use typed \`LocalDevelopment()\` as the first-run default.
- **Saved configuration exists:** load and validate it.
- **Saved configuration is invalid/unusable:** fail clearly; do not silently fall back to \`LocalDevelopment()\`.
- **Saved configuration requires a bootstrap credential that is missing:** fail clearly; do not fall back.
- **Saved configuration points to an unavailable database:** preserve the saved configuration and expose the unavailable state; do not replace it with a different configuration.

\`LocalDevelopment()\` is therefore a first-run default, not permanent runtime wiring.

### Persistence changes that require recomposition

- server/instance;
- port;
- database;
- authentication mode;
- bootstrap credential/reference;
- encryption/trust policy;
- database creation policy when it affects the constructed persistence behavior;
- command timeout when it is part of the constructed store options.

Resource-only changes must not reconstruct the persistence graph.

The persisted `create-database-if-missing` value is configuration state, not authorization for a connection test or ordinary startup composition to mutate the database. Phase 1.12 connection tests and composition remain non-destructive; database creation/schema initialization stays an explicit lifecycle operation.

---

## 4. Critical Configuration Contract Corrections

### 4.1 Bootstrap credential is not a Hive database Secret

The current Hive Secret Store is SQL-backed.

A SQL password is needed before that same SQL database can be opened.

Therefore the SQL-password credential cannot depend on the database-backed Secret Store.

Phase 1.12 must introduce a separate bootstrap-secret boundary:

\`\`\`
Bootstrap credential
      ↓
open Hive database
      ↓
Hive Secret Store
      ↓
Provider/account/resource secrets
\`\`\`

The bootstrap secret must:

- live outside the target Hive database;
- be protected with Windows DPAPI and user scope;
- be available before Hive.Persistence is constructed;
- be referenced by configuration rather than stored as plaintext;
- never be emitted in JSON, diagnostics, Example output, or normal logs.

The existing Hive resource Secret Store remains authoritative for ProviderAccount and other Hive-owned resource secrets.

The bootstrap store belongs to the host/bootstrap infrastructure boundary. Settings may submit a new credential or replacement credential through a public application boundary, but it never reads raw bootstrap material back and never accesses the bootstrap store directly.

### 4.2 Do not reuse the Hive Secret reference for SQL bootstrap

The current persistence configuration contains a secret reference used by the existing implementation.

That reference must **not** remain the mechanism for obtaining the SQL password once the bootstrap boundary is implemented.

The implementation must introduce an intentional bootstrap credential reference/boundary and define its compatibility with the existing persisted configuration. Do not silently reinterpret an existing field with different semantics.

The persisted configuration contains only the bootstrap credential reference/identifier and other non-secret connection settings. The bootstrap store contains the protected material. These are separate records with separate ownership and lifecycle.

### 4.3 AgentDefinition must have durable execution configuration

The current \`AgentDefinition\` stores identity/display/generation only, while execution currently receives an \`ExecutionTarget\` directly.

That is insufficient for configured-host behavior.

Phase 1.12 shall add a durable relationship from \`AgentDefinition\` to an existing \`ExecutionTarget\`.

The first configured-host contract is intentionally simple:

\`\`\`
AgentDefinition
    ↓
configured ExecutionTargetId (nullable for legacy/unconfigured definitions)
    ↓
Management resolves existing ExecutionTarget
    ↓
ExecutionTarget → ProviderAccount → Provider
    ↓
execution boundary
\`\`\`

The AgentDefinition must not duplicate:

- provider endpoint;
- account identity;
- credentials;
- model/deployment;
- capability state.

The existing ExecutionTarget remains authoritative.

### Selection boundary

Do not add a second selection algorithm in WinForms.

Phase 1.12 uses one explicit configured target reference. Advanced preferred/automatic target selection remains owned by the existing execution-target selection architecture and is outside this slice.

A configured-host execution requires a usable target reference. Missing, unauthorized, retired, or otherwise unusable target references are explicit configuration/runtime failures.

### Persistence implications

The AgentDefinition → ExecutionTarget relationship is a durable-schema contract change and requires:

- next ordered migration;
- appropriate FK/index strategy compatible with existing lifecycle rules;
- load/save/update support in the AgentDefinition persistence store;
- Management validation across resource relationships;
- explicit behavior for referenced target retirement/deletion;
- stale/missing/unauthorized target handling;
- concurrency/version tests.

Existing AgentDefinitions must remain readable after migration. A null target is allowed for an unconfigured definition; a newly configured-host Agent requires a usable target reference. A target may be retired without deleting the AgentDefinition, but execution must reject that configuration clearly until it is repaired.

Do not require a hard database foreign key if the existing resource lifecycle intentionally retains retired records; enforce the relationship through the Management boundary and use an index/constraint strategy compatible with that lifecycle.

---

## 5. 1.12-A — Host Configuration and Runtime Composition

### Goal

Remove hard-coded runtime persistence wiring from the host application's normal composition path. The reusable composition/lifetime boundary belongs to the host layer; the Example Host consumes it.

### Current defect

The Example Host currently constructs persistence using:

\`\`\`csharp
HiveDatabaseOptions.LocalDevelopment()
\`\`\`

inside service composition.

That means Settings can save another configuration while the running host still uses the hard-coded default.

### Required implementation

Create one host-owned composition/lifetime boundary that:

1. loads the persisted Hive persistence configuration;
2. applies the first-run \`LocalDevelopment()\` default only when no configuration exists;
3. validates the selected configuration;
4. depends on an explicit bootstrap-credential abstraction for SQL-password cases;
5. creates \`HiveDatabaseOptions.FromConfiguration(...)\` or the existing equivalent;
6. constructs the configured persistence-backed stores;
7. constructs the Management facade and other host-owned services from that graph;
8. publishes the complete graph atomically;
9. exposes the current graph/status to the host;
10. replaces and disposes the previous graph only after the new graph is ready.

The composition boundary must not require a UI form to perform service construction. Settings is a caller of the configuration boundary; it is not the owner of the service graph.

### Required failure states

The composition boundary must distinguish at least:

- configuration missing;
- configuration invalid;
- bootstrap credential missing;
- server/database unavailable;
- database missing;
- Hive schema missing/uninitialized;
- schema requires migration;
- unsupported/future schema;
- configured Management/resource graph unavailable;
- selected Agent missing/unusable;
- selected ExecutionTarget missing/unusable;
- provider credential unavailable;
- provider connection failure.

The composition layer must preserve these distinctions. UI may translate them into user-friendly messages.

### 1.12-A exit gate

This stage proves, with focused automated coverage and code inspection, that the host has one composition/lifetime boundary and can consume the persisted configuration through the bootstrap-credential abstraction without embedding \`LocalDevelopment()\` as permanent wiring. The concrete DPAPI-backed bootstrap store is completed in 1.12-B; 1.12-A depends only on its stable application-facing contract.

Required focus:

- no-saved-config → LocalDevelopment default;
- saved-config → configured database options;
- invalid saved config does not fall back;
- bootstrap-required config is rejected when bootstrap material is unavailable;
- candidate graph construction is isolated from publication;
- failed replacement preserves the current usable graph;
- owned graph resources are disposed exactly once on successful replacement.

It does **not** yet complete the Settings editor, configured Agent example, or final UI migration.

---

## 6. 1.12-B — Bootstrap Credential Boundary

Implement the bootstrap credential mechanism required by 1.12-A.

### Contract requirements

The bootstrap boundary must support the minimum lifecycle required by Settings and startup:

- set/create;
- replace;
- resolve internally for composition;
- clear/remove only when the persisted configuration no longer references it and the boundary explicitly supports cleanup.

Raw material may be supplied and resolved internally, but normal configuration reads expose only non-secret configuration and the bootstrap reference. Settings never receives the stored secret value back.

### Storage requirements

- Windows DPAPI;
- user scope;
- file/system storage outside the target Hive DB;
- no plaintext credential in \`hive-settings.json\`;
- no credential in Example output or diagnostics;
- deterministic disposal of secret material.

### Integration rule

The bootstrap boundary is available before Hive.Persistence is constructed.

The host composition boundary depends only on the bootstrap-credential contract. Its concrete storage implementation remains replaceable behind that contract.

Do not store the bootstrap SQL password inside the Hive database-backed Secret Store.

---

## 7. 1.12-C — Real Settings Management

Once the composition/bootstrap prerequisites exist, complete the authoritative Settings management surface.

### Providers

Manage:

- Provider records;
- ProviderAccounts;
- credential references;
- ExecutionTargets;
- supported connection tests.

The UI must preserve the actual Provider → Account → Target relationship.

### Agents

Manage:

- AgentDefinitions;
- display/key/generation;
- explicit configured ExecutionTarget reference;
- lifecycle/version/concurrency semantics;
- clear configured/unconfigured state.

Agents must not create hidden Provider/Account/Target records.

### Persistence

Manage:

- SQL Server / LocalDB;
- server/instance;
- port;
- database;
- Windows Integrated / SQL Password;
- encryption/trust policy;
- create-database-if-missing policy;
- command timeout;
- bootstrap credential reference.

Persistence connection testing remains non-destructive:

- no database creation;
- no migration;
- no schema mutation;
- explicit database state;
- explicit Hive schema state.

Settings does not execute migrations.

---

## 8. 1.12-D — Settings UI on the Hive UI Foundation

Replace the current Settings-specific navigation/list presentation with the existing reusable UI foundation.

### Required controls

Navigation:

\`\`\`
HiveNavigationTree
\`\`\`

List/CRUD pages:

\`\`\`
HiveListPageLayout
HiveCrudPage<TItem>
HiveListView
\`\`\`

Editors/actions:

\`\`\`
HiveEditorLayout
HiveButton
HiveMessageBox
\`\`\`

Theme:

\`\`\`
IHiveThemeManager
\`\`\`

Do not reintroduce:

- owner-drawn Settings \`ListBox\` navigation;
- Settings-specific ListView renderers;
- duplicate CRUD abstractions;
- duplicate theme/state logic.

The Settings hierarchy should use the established tree/navigation behavior and preserve selection/top-node/scroll position across theme changes.

---

## 9. 1.12-E — Settings-Driven Runtime State

Settings changes must become application state, not saved-but-ignored data.

Required flow:

\`\`\`
Open Settings
   ↓
edit authoritative configuration
   ↓
save
   ↓
host detects affected change
   ↓
recompose or refresh as required
   ↓
close/return to normal host UI
   ↓
next operation uses new state
\`\`\`

### Change classes

**Persistence-boundary change**

Recompose the persistence graph.

**Provider/ProviderAccount/ExecutionTarget/AgentDefinition change**

Refresh authoritative Management/resource state without rebuilding persistence.

### Safe apply behavior

If a new persistence graph cannot be constructed:

- do not publish it;
- do not dispose a still-usable current graph;
- report the typed failure;
- keep the saved configuration available for retry/startup.

A later successful apply may replace the current graph.

---

## 10. 1.12-F — Example Host as a Real Consumer

The Example Host must behave like an ordinary Hive WinForms application.

### Host-level Settings

Expose a normal application Settings/Configuration command from the host shell.

A user must not navigate to a special Settings Example merely to configure Hive.

The existing Settings Example may remain as a direct public-API/configuration-boundary example, but it is secondary.

### Startup

The host must:

1. load saved Hive persistence configuration;
2. compose the configured service graph;
3. load Providers and AgentDefinitions when Management is available;
4. populate configured Agent selection;
5. expose useful unavailable/unconfigured status when composition cannot complete.

### After Settings closes

The host must:

1. detect affected configuration changes;
2. recompose persistence when required;
3. refresh resources when only resource configuration changed;
4. preserve the selected Agent when still valid;
5. clear/revalidate selection when missing, retired, or unusable.

### Real configured flow

The final configured-host path is:

\`\`\`
Settings
  ↓
Provider
  ↓
ProviderAccount + credential
  ↓
ExecutionTarget
  ↓
AgentDefinition + target reference
  ↓
close Settings
  ↓
select configured Agent
  ↓
run normal public-API operation
  ↓
Hive resolves the configured target
\`\`\`

A successful configured operation, not a configuration dump, is the primary proof that Settings works.

---

## 11. 1.12-G — Example Classification

Do not make every example configuration-dependent.

### Isolated contract examples

These may use:

- fakes;
- deterministic local infrastructure;
- temporary/example resources;
- LocalDevelopment when it is intrinsic to the example purpose.

They prove a specific contract.

### Configured-host examples

These:

- consume persisted Providers/Accounts/Targets/Agents;
- use the selected configured Agent;
- use public Hive APIs;
- fail clearly when configuration is absent/unusable;
- demonstrate that Settings changes affect later host behavior.

Every existing hard-coded \`HiveDatabaseOptions.LocalDevelopment()\` use must be reviewed and explicitly classified. Change it only when the example is intended to prove configured-host behavior.

---

## 12. 1.12-H — Focused Automated Coverage

Coverage must match the actual boundary implemented by each sub-stage. The numbered sub-stage is a consolidation/audit point, not a reason to postpone tests until after UI work. Tests for 1.12-A and 1.12-B are written with those implementations; resource/UI integration tests are added with their owning sub-stages.

### Persistence/configuration

- no saved configuration → LocalDevelopment default;
- saved configuration round-trip;
- invalid saved configuration is not silently replaced;
- authentication-mode validation;
- bootstrap reference serialization;
- no plaintext bootstrap credential;
- bootstrap missing/failure cases;
- \`FromConfiguration\` option mapping.

### Composition

- saved configuration constructs the intended persistence options;
- LocalDevelopment is only the no-saved-config default;
- configured candidate graph is complete before publication;
- failed replacement preserves the usable current graph;
- replaced owned components are disposed;
- concurrent reconfiguration is serialized;
- current graph/status remains deterministic.

### Resource relationships

- Provider CRUD;
- ProviderAccount CRUD;
- ExecutionTarget CRUD;
- AgentDefinition CRUD;
- AgentDefinition target persistence;
- relationship ownership/scope;
- lifecycle/retirement behavior;
- missing/retired/unauthorized target handling;
- version/concurrency behavior.

### Settings integration

- saved Settings state is visible on subsequent Management reads;
- resource changes refresh without persistence reconstruction;
- persistence changes recompose;
- invalid replacement does not destroy the active graph;
- selected Agent preservation/clearing rules;
- Settings cannot read back raw bootstrap material.

### Security

- bootstrap credential absent from JSON;
- bootstrap credential absent from diagnostics/output;
- Hive resource secrets absent from ordinary output;
- secret material disposed after use;
- no password appears in Example output.

### Connection/provider boundaries

- non-destructive persistence connection test;
- database/schema state classification;
- provider connection-test boundary;
- cancellation/failure classification appropriate to the implemented boundary.

---

## 13. 1.12-I — Manual Configured-Host Verification

Primary manual scenario:

\`\`\`
Example Host
  → Settings
  → configure Persistence
  → configure Provider
  → configure ProviderAccount
  → configure credential
  → configure ExecutionTarget
  → configure AgentDefinition
  → close Settings
  → select Agent
  → run configured Agent example
\`\`\`

Developer verification must establish:

- saved persistence configuration is actually consumed;
- the host no longer relies on hard-coded LocalDevelopment wiring;
- Providers reload;
- AgentDefinitions reload;
- the selected Agent reflects persisted state;
- the configured ExecutionTarget is actually used;
- changing the configured target affects later operations;
- invalid/missing/retired configuration is surfaced clearly;
- no secret material appears in Example output;
- persistence changes recompose correctly;
- resource-only changes do not rebuild persistence.

The existing “Capture configuration” example is supplemental and does not establish completion.

---

## 14. 1.12-J — Final UI/UX Review

Settings is a permanent application surface and must match the shared Hive desktop UI contract.

Review:

- navigation hierarchy;
- category/group clarity;
- header/action/content rhythm;
- typography;
- density;
- resizing;
- editor spacing;
- ListView columns;
- empty/no-match/loading/error states;
- Add/Edit/Delete behavior;
- selected/hover/disabled states;
- Light/Dark/System;
- TreeView selection/scroll preservation on theme changes;
- page/editor disposal;
- no duplicate renderer/theme logic;
- no layout jumps when switching pages or themes.

The shared controls are the acceptance surface; do not solve these concerns again inside Settings.

---

## 15. 1.12-K — Documentation and Closure

Documentation changes required by a structural decision are made before the corresponding structural code change, in accordance with `AGENTS.md`. Final usage/verification documentation is updated when the owning sub-stage is complete.

Update only the source-of-truth documents whose state changed.

Required documentation updates include:

- \`docs/architecture.md\` only when global architectural intent/indexing changes;
- \`docs/architecture/v1-host-and-management.md\` for host composition, Settings, Example Host, and Management boundary changes;
- \`docs/architecture/execution-and-persistence.md\` for persistence, execution, resource, or durable-state changes;
- \`docs/architecture/foundations.md\` when persistence/bootstrap/UI foundation contracts change;
- \`docs/ui/forms.md\` for final Settings page/control composition;
- \`docs/ui/examples.md\` for configured-host usage and exact Example navigation;
- \`docs/Hive_Active_Work.md\` for the current sub-stage and verification gate.

Do not mark \`docs/Hive_Current_Status.md\` complete until actual verification is performed.

Create historical verification records only from real results.

---

## 16. Explicit Non-Goals

Phase 1.12 does not implement:

- Phase 1.13+;
- business-app integration;
- vision routing;
- structured extraction;
- business-app write tools;
- the full MAF sequential V1 pipeline;
- configuration import/export/portability;
- multi-user authentication;
- future cognitive generations;
- a second orchestration engine;
- a second persistence system;
- a second secret system;
- a second WinForms UI toolkit.

---

## 17. Phase 1.12 Completion Gate

Phase 1.12 remains open until all are true:

1. Hive Settings is the global package configuration center.
2. Saved persistence configuration controls the actual host persistence boundary.
3. SQL-password bootstrap credentials can be resolved before target-database access.
4. Bootstrap credentials and Hive resource credentials are separated correctly.
5. Provider, ProviderAccount, ExecutionTarget, and AgentDefinition state is authoritative Management state.
6. AgentDefinition has a durable configured ExecutionTarget relationship without duplicating target data.
7. Settings uses the reusable Hive UI foundation.
8. Settings performs no direct SQL/provider transport/migration work.
9. The Example Host exposes normal host-level Settings.
10. The Example Host loads Providers and Agents from configured state.
11. A configured Agent is used by a normal public-API operation.
12. Settings changes affect subsequent behavior.
13. Persistence changes trigger safe service recomposition.
14. Resource-only changes refresh without unnecessary persistence reconstruction.
15. Running executions keep their existing effective configuration snapshot.
16. Focused tests cover configuration, composition, security, and resource relationships.
17. Required Example scenarios exist and use public Hive APIs.
18. Manual configured-host verification succeeds.
19. Broader \`Hive.Tests\` verification succeeds.
20. Active Work/status/verification documentation reflects only actual results.

---

## 18. Verification Handoff

For any capability requiring an Example, the handoff must name the exact Example path and focused tests.

### Current 1.12-A handoff

**Example to run:** No new Example is required solely to establish the internal composition boundary. Do not use the existing Settings Example as a substitute for composition tests.

**Focused tests:**
- \`tests/Hive.Tests/HiveHostCompositionTests.cs\` — 1.12-A host composition boundary (create in this sub-stage).
- existing \`HiveConfigurationTests.cs\` / \`HivePersistenceOptionsTests.cs\` — preserve relevant configuration/options coverage and extend only where the new composition contract requires it.

**Inspection target:**
- host composition/lifetime owner must not live in \`Hive.Example.WinForms\` as a one-off service graph;
- no production runtime path may hard-code \`HiveDatabaseOptions.LocalDevelopment()\` after a saved configuration exists.

### Final Phase 1.12 handoff

**Example to run:**
\`Settings / Configuration / Hive Settings / Provider & Persistence — Hive.Example.WinForms\`

**Configured-host scenario:** the host-level Settings command followed by the configured Agent operation specified in 1.12-I.

**Focused tests:** the relevant 1.12 test classes/files produced by the sub-stages, plus the existing configuration/persistence/provider tests where still applicable.

**Broader verification:** full \`Hive.Tests\` execution is required before Phase 1.12 closure.

Never record a passing result unless it was actually executed.

---

## 19. Guiding Rule

**Hive Settings is the global Hive package configuration center. Settings must configure Hive, not merely display Hive configuration. The same authoritative state edited by Settings must drive subsequent host behavior, and the Example Host must prove that through normal public APIs.**

The key architectural rule is:

\`\`\`
configure once
    ↓
persist once
    ↓
compose from the persisted state
    ↓
consume the same authoritative state
\`\`\`

No parallel configuration model, no hidden UI-only state, and no hard-coded runtime bypass.
