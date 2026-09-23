# Hive — Active Work

Last updated: 2026-09-23

## Active slice

**1.12 — Settings, Configuration, and Real Host Consumption**

### Current sub-stage

**1.12-F — Example Host as a Real Consumer**

Detailed workload and ordering: `docs/plan/Phase1.12_Settings_Host_Integration.md`

Phase 0 — Foundations and Phase 1.1 through Phase 1.11 are complete and verified.
Phase 1.12 is the authorized implementation slice.

## Objective

Establish Hive Settings as the permanent global Hive package configuration center, then make the configured state actually drive the host application's persistence/resources/runtime consumption. Phase 1.12 establishes the first concrete domains; later Hive capabilities extend this same configuration center rather than creating parallel settings roots.

## Scope

The complete Phase 1.12 program is subdivided into bounded sub-stages described in `docs/plan/Phase1.12_Settings_Host_Integration.md`:

- host configuration/runtime composition;
- persistence bootstrap credential boundary;
- Provider/ProviderAccount/ExecutionTarget and AgentDefinition Settings management;
- migration of Settings UI onto `Hive.Host.WinForms.UI` reusable navigation/list/editor controls;
- Settings-driven reload/recomposition;
- real Example Host consumption of configured state;
- focused verification and documentation closure.

1.12-A through 1.12-E are complete and verified/accepted. Current sub-stage 1.12-F makes the Example Host consume configured Providers, Accounts, ExecutionTargets, and AgentDefinitions through the real host composition and public APIs. Do not implement later 1.12 sub-stages in the same run unless a dependency is required to complete 1.12-F.

## Completed 1.12-B verification gate

1. Bootstrap credential storage is outside the target Hive database.
2. Windows DPAPI uses user scope for stored bootstrap material.
3. Bootstrap set/create, replace, resolve, and clear lifecycle operations are explicit and do not expose material through configuration reads.
4. Bootstrap references are distinct from Hive database-backed Secret Store references.
5. `hive-settings.json` contains only the bootstrap reference/identifier and never raw SQL password material.
6. Missing, corrupt, undecryptable, and unsupported bootstrap material produce typed failures without secret leakage.
7. Resolved `SecretMaterial` is deterministically disposed by its caller.
8. Focused automated coverage exists for the bootstrap boundary.
9. No configured-host Example or final Settings UI work is implemented in this sub-stage.

## Completed 1.12-A verification gate

1. The host-layer composition boundary, not `Hive.Example.WinForms`, owns the current Hive service graph.
2. No saved persistence configuration uses the typed `LocalDevelopment()` first-run default.
3. A saved persistence configuration is loaded and validated without silent fallback when invalid/unavailable.
4. SQL-password composition depends on the separate bootstrap-credential boundary rather than the database-backed Hive Secret Store.
5. Candidate service graphs are fully constructed before publication.
6. A failed replacement preserves the currently usable graph.
7. Successfully replaced graph resources are disposed exactly once.
8. Focused automated coverage exists for the 1.12-A composition boundary.
9. No Phase 1.12 UI/resource/example sub-stage is marked complete by this handoff.

Final Phase 1.12 verification additionally requires the full Settings flow, configured Agent operation, focused tests for all introduced boundaries, manual developer verification, and broader `Hive.Tests` execution.

## Constraints

- Do not implement Phase 1.13 or later.
- Preserve existing Hive.Management and Hive.Persistence boundaries.
- Do not duplicate provider transport, secret storage, SQL connection/migration, or bootstrap logic in the Settings UI.
- Use the existing Example Host pattern for the public example required by the slice. The global Settings center itself is host/application infrastructure; its Examples demonstrate configured behavior rather than replacing the Settings center.

## Implementation checkpoint

The original Phase 1.12 Settings implementation and the 1.12-A/1.12-B infrastructure slices are present on `main`; 1.12-A and 1.12-B automated verification are recorded, while manual host acceptance and later Phase 1.12 Settings/runtime-consumption work remain open. The Settings-to-host consumption gap was identified against the HAgent reference implementation and is now the active 1.12 program. The implementation must proceed through the ordered sub-stages in the plan.

Implemented in the active slice:

- typed SQL Server/LocalDB persistence configuration contracts;
- Management save/load boundary backed by an atomic JSON settings file containing only non-secret persistence fields plus the dedicated bootstrap credential reference; legacy `credentialSecretId` settings are rejected explicitly rather than silently reinterpreted;
- non-destructive SQL Server persistence connection test reporting database and Hive schema state separately;
- ProviderAccount credential Secret Store reference support;
- Management-mediated provider connection-test boundary and OpenAI-compatible concrete tester;
- first-class public WinForms Settings shell with Provider and Persistence pages;
- Provider setup for Provider, ProviderAccount, ExecutionTarget, credential references, and provider connection testing;
- Persistence settings for server/port/database/authentication/security/initialization policy/timeout;
- dedicated `HiveBootstrapCredentialReference`, host DPAPI implementation, and Management set/remove boundary for SQL-password bootstrap material;
- persistence connection testing now resolves SQL passwords through the bootstrap boundary rather than the database-backed Hive Secret Store;
- Hive Persistence Settings now submits SQL password material through Management and persists only the bootstrap reference;
- password replacement creates a new bootstrap reference first, publishes the new persistence configuration, and removes the old reference only after the configuration save succeeds;
- focused `HiveBootstrapCredentialStoreTests` coverage for DPAPI round-trip, replacement, clear, corruption handling, and Management reference lifecycle;
- focused configuration, persistence-option, provider-credential-reference, and provider connection-test coverage;
- 1.12-C AgentDefinition configuration now carries an optional durable `ConfiguredExecutionTargetId` reference without duplicating Provider/ProviderAccount/ExecutionTarget state;
- ordered schema migration 009 adds the AgentDefinition → ExecutionTarget foreign key and lookup index;
- `SqlAgentDefinitionResourceStore` persists and reloads the configured ExecutionTarget reference;
- `HiveManagementFacade` validates configured target existence, access, and lifecycle before creating or updating an AgentDefinition;
- focused 1.12-C Management tests cover configured-target round-trip, clearing, missing-target rejection, unauthorized-target rejection, and retired-target rejection;
- host-owned `HiveHostComposition` and `HiveHostServiceGraph` boundaries with first-run configuration loading, persisted configuration consumption, explicit bootstrap-credential injection, serialized candidate construction/publication, failed-replacement preservation, and idempotent graph disposal;
- the published Management facade uses the same authoritative configuration-store instance as the composition boundary;
- `Hive.Example.WinForms` now consumes the host-owned service graph instead of constructing a competing persistence/Management graph;
- the former `HiveSettingsExample` configuration-inspection scenario was removed because it was not the real host configuration flow and was not an acceptance surface for 1.12-A;
- focused `HiveHostCompositionTests` coverage for first-run defaults, saved configuration consumption, shared configuration state, invalid configuration, bootstrap-credential failure, failed replacement, disposal, and serialized recomposition;
- `Hive.Tests` now targets the Windows desktop target required to reference the host composition project.
- 1.12-D replaces Settings' custom owner-drawn `ListBox` navigation with `HiveNavigationTree`, preserving the existing shared theme/state model.
- 1.12-D adds an Agents Settings page using `HiveCrudPage<AgentDefinition>`, `HiveListView`, and `HiveListPageLayout` through the `Hive.Host.WinForms.UI` foundation.
- Agent editing uses `HiveEditorLayout` and the public `IHiveManagementFacade` for create/update/delete operations.
- Agent execution-target choices are loaded through the authoritative Provider → ProviderAccount → ExecutionTarget Management hierarchy; retired targets remain visible and are rejected by the existing Management validation boundary.
- 1.12-D organizes the Settings navigation as Providers → Provider Configuration, Accounts / Credentials, and Execution Targets; plus Agents and Persistence.
- 1.12-D implements Provider Configuration, Accounts / Credentials, Execution Targets, and Agents as separate CRUD resource pages using `HiveCrudPage<TItem>` and `HiveEditorLayout`; the former combined Provider settings editor is removed.
- Accounts / Credentials are scoped by Provider, and Execution Targets are scoped by Provider Account, matching the Management resource hierarchy.
- Persistence remains a single global configuration editor because it represents one persisted configuration document rather than a CRUD resource collection.
- Persistence Server / instance uses a free-form text field; Hive does not enumerate installed SQL Server instances. The database name is generated automatically as Hive-[Host-App-Name].
- 1.12-D adds `Overview / Getting Started / Example Configuration` as the normal Example Host entry point to the real Hive Settings center.
- The configuration example explains that Provider Accounts are credential/resource records rather than provider login screens, that Execution Targets contain concrete model/endpoint configuration, and that future Settings domains appear only when their authoritative contracts exist.
- The Example Host now supplies a deterministic development ResourceAccessContext for its Settings surface so configured resources remain addressable across Example Host restarts.
- The Settings shell subtitle/description now identifies it as the global Hive package configuration center.
- Settings initializes the Persistence page independently and lazy-loads database-backed resource pages when the user navigates to them, so an unavailable configured Hive database cannot prevent the global Persistence configuration surface from opening.
- Persistence Test Connection remains non-destructive: it reports `DatabaseNotFound` when the target database is absent. The Settings UI records `createDatabaseIfMissing` as initialization policy, and the explicit Management/Application `InitializePersistenceAsync` operation now creates the database when allowed and applies ordered Hive migrations. Save and Test remain non-destructive and never create or migrate the database.

- The old combined `HiveProviderSettingsView` was deleted rather than retained as a compatibility UI layer; Provider, ProviderAccount, and ExecutionTarget now have independent Settings pages and editor dialogs.

- User-visible UI errors in the existing Example Host and Settings UI now use the shared `HiveUiErrorReporter`: MessageBox plus technical Output-panel diagnostics when the Example Output sink is available; secrets are never written to either surface.

Before coding, inspect:

- `docs/plan/Phase1.12_Settings_Host_Integration.md`;
- `docs/architecture/v1-host-and-management.md`;
- `docs/architecture/execution-and-persistence.md`;
- `docs/architecture/foundations.md` where persistence/bootstrap/UI foundation boundaries are involved;
- current Management contracts and facade;
- existing Secret Store and DPAPI persistence boundary;
- HiveDatabaseOptions/migration/schema bootstrap;
- Provider/ProviderAccount/ExecutionTarget resource contracts and persistence;
- AgentDefinition and first-real-execution composition;
- Host UI conventions and `docs/ui/examples.md`;
- `Hive.Host.WinForms.UI` controls: `HiveNavigationTree`, `HiveListPageLayout`, `HiveListView`, `HiveCrudPage<TItem>`, and `HiveEditorLayout`;
- Example Host discovery/service-composition/output pattern;
- host-layer composition/lifetime ownership, candidate publication, replacement, and disposal semantics;
- existing configuration/provider/persistence/execution tests.

## Verification evidence

Developer-reported local verification before starting 1.12-B: the full `Hive.Tests` suite completed with **153/153 passed, 0 failed, 0 skipped** on 2026-09-22. The run included the focused 1.12-A composition tests.

Developer-reported local verification after 1.12-B: the full `Hive.Tests` suite completed with **158/158 passed, 0 failed, 0 skipped** on 2026-09-22. This run verifies the completed 1.12-B bootstrap credential changes together with the existing suite.

1.12-C implementation is committed and developer-verified. The full `Hive.Tests` suite completed with **159/159 passed, 0 failed, 0 skipped** on 2026-09-22.

The previously existing Settings inspection Example produced an old-schema/local-database error and was not an acceptable configured-host verification scenario; that obsolete Example has been removed.

## Completed 1.12-C verification gate

1. AgentDefinition carries an optional durable `ConfiguredExecutionTargetId` reference without duplicating ExecutionTarget state.
2. Schema migration 009 adds the AgentDefinition → ExecutionTarget foreign key and lookup index.
3. SQL AgentDefinition persistence saves and reloads the configured target reference.
4. Management validates target existence, access, and retirement before AgentDefinition create/update.
5. Focused Management coverage covers round-trip, clearing, missing-target rejection, unauthorized-target rejection, and retired-target rejection.
6. Migration integration coverage verifies the schema version 9 migration and the new AgentDefinition target column/index.
7. Developer full-suite verification completed with **159/159 passed, 0 failed, 0 skipped** on 2026-09-22.

## Completed 1.12-D verification gate

1. Settings navigation uses the reusable `HiveNavigationTree` foundation rather than the former Settings-specific owner-drawn navigation.
2. Provider, ProviderAccount, ExecutionTarget, and AgentDefinition pages use the reusable CRUD/list/editor foundation.
3. The global Settings center exposes the Provider hierarchy, Agents, and Persistence pages through the documented navigation structure.
4. The Example Host exposes the real Settings center through the Overview / Getting Started / Example Configuration path.
5. Developer accepted the current Settings UI as good enough to proceed; final UI/UX polish remains intentionally deferred to 1.12-J.
6. Developer full-suite verification completed with **160/160 passed, 0 failed, 0 skipped** on 2026-09-23 using .NET 10.0.1.

## Completed 1.12-E verification gate

1. Persistence Settings edits are persisted through the authoritative Management configuration boundary.
2. The host applies persisted configuration after the Settings dialog closes through the host-owned composition boundary.
3. Unchanged persistence configuration keeps the current graph instead of reconstructing it.
4. Changed persistence configuration creates and publishes a complete replacement graph; failed replacement preserves the current graph.
5. The Example Host refreshes the active Example against the current published Management graph after Settings closes.
6. Hive database/schema initialization is an explicit `Initialize Hive` operation; Save, Test, and normal Settings navigation remain non-destructive.
7. User manually confirmed the configured database Settings flow works and the surrounding Settings behavior is correct.
8. User manually confirmed Settings-error Output remains available after the Settings form closes.
9. Full `Hive.Tests` developer verification completed with **164/164 passed, 0 failed, 0 skipped** on 2026-09-23 using .NET 10.0.1.

## 1.12-F implementation checkpoint

- The Example Host now creates its `HiveExampleServices` from the current host-owned `HiveHostServiceGraph` and exposes the deterministic host `ResourceAccessContext` to configured-host examples.
- Startup loads persisted Providers and AgentDefinitions through the current Management facade and populates a host-level Configured Agent selector; the selector is refreshed after Settings apply and preserves the prior AgentDefinition when it remains valid.
- `IHiveManagementFacade.ExecuteConfiguredAgentAsync` is the configured Agent application boundary. It resolves the persisted AgentDefinition → ExecutionTarget → ProviderAccount → Provider relationship, resolves an optional ProviderAccount Secret Store credential through the existing Management/Persistence boundary, creates the current Base Agent/runtime, and delegates actual provider/MAF execution to `Hive.Coordination.AgentExecutionService`.
- The configured execution path does not create a competing service graph, database, provider target, credential store, or orchestration implementation. Existing isolated execution examples remain isolated contracts; this new example is the configured-host acceptance surface.
- Added `Agents / Base Agent / Configured Agent Execution` as the public Example Host scenario. Its output reports the resolved resource identities and execution result without emitting credential material.
- Settings filter selectors now load only active Providers and ProviderAccounts; retired resources remain available in the CRUD management views but cannot be selected for new filtering/configuration operations.
- Settings resource pages now refresh their Management-backed state when revisited within the same Settings form, so newly created Providers, ProviderAccounts, and ExecutionTargets are immediately available to dependent Settings pages such as AgentDefinition editing.
- AgentDefinition creation now constructs the required Hive resource envelope/identity before crossing the Management create boundary; editing an existing AgentDefinition continues to preserve its persisted resource identity.
- Configured Example views can now resolve `HiveExampleServices` through the Example Host service-provider boundary; the configured Agent execution example no longer fails during view creation.
- Added focused integration coverage for configured execution through the persisted resource graph, including target switching between two real local test endpoints and explicit rejection of an AgentDefinition with no configured target.
- The existing `First Real Agent Execution` example remains a local deterministic contract example and is intentionally not repurposed as the configured-host acceptance scenario.
## Verification handoff

Current sub-stage: **1.12-F — Example Host as a Real Consumer**

Configured-host target:
**Overview / Getting Started / Example Configuration — Hive.Example.WinForms**, followed by the normal host-level Settings flow, host-level Configured Agent selection, and `Agents / Base Agent / Configured Agent Execution`.

Tests to run:
- `tests/Hive.Tests/AgentExecutionIntegrationTests.cs` — configured Agent execution and persisted target-switching coverage;
- `tests/Hive.Tests/HiveManagementFacadeTests.cs` — preserve existing Management CRUD/relationship coverage;
- broader `Hive.Tests` execution after the focused tests.

Verification required:
- configure one Provider, ProviderAccount, credential, ExecutionTarget, and AgentDefinition;
- verify the Example Host reloads the persisted Provider and AgentDefinition state into the host-owned service graph;
- verify the Configured Agent selector shows the configured Agent and remains stable across a Settings save/apply when that Agent remains valid;
- verify a configured Agent operation uses the persisted ExecutionTarget and produces the expected local/provider response;
- change the AgentDefinition's configured ExecutionTarget, close/apply Settings, and verify the next operation uses the new target;
- verify missing, retired, or otherwise unusable configured Agent/ExecutionTarget state is surfaced clearly;
- verify no Provider credential or bootstrap SQL credential appears in Example Output, MessageBox details, or normal diagnostics;
- verify the configured-host example does not create or depend on `HiveDatabaseOptions.LocalDevelopment()`.

1.12-F implementation and focused tests are now present on `main`, but the sub-stage remains open until developer build/test/manual verification is actually performed. 1.12-E is closed by this handoff. Final UI/UX polish remains deferred to 1.12-J.

## Historical verification

Phase 1.11 completion is recorded in `docs/verification/phase-1/1.11.md`.
