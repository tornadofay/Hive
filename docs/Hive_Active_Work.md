# Hive — Active Work

Last updated: 2026-09-22

## Active slice

**1.12 — Settings, Configuration, and Real Host Consumption**

### Current sub-stage

**1.12-C — Real Settings Management**

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

1.12-A and 1.12-B are complete and verified. Current sub-stage 1.12-C implements the authoritative Settings resource-management surface. Do not implement later 1.12 sub-stages in the same run unless a dependency is required to complete 1.12-C.

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
- host-owned `HiveHostComposition` and `HiveHostServiceGraph` boundaries with first-run configuration loading, persisted configuration consumption, explicit bootstrap-credential injection, serialized candidate construction/publication, failed-replacement preservation, and idempotent graph disposal;
- the published Management facade uses the same authoritative configuration-store instance as the composition boundary;
- `Hive.Example.WinForms` now consumes the host-owned service graph instead of constructing a competing persistence/Management graph;
- the former `HiveSettingsExample` configuration-inspection scenario was removed because it was not the real host configuration flow and was not an acceptance surface for 1.12-A;
- focused `HiveHostCompositionTests` coverage for first-run defaults, saved configuration consumption, shared configuration state, invalid configuration, bootstrap-credential failure, failed replacement, disposal, and serialized recomposition;
- `Hive.Tests` now targets the Windows desktop target required to reference the host composition project.

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

The previously existing Settings inspection Example produced an old-schema/local-database error and was not an acceptable configured-host verification scenario; that obsolete Example has been removed.

## Verification handoff

Current sub-stage: **1.12-C — Real Settings Management**

Example to run: **None solely for 1.12-C until the resource-management surface is complete.** The configured-host Example remains deferred to 1.12-F.

Previous 1.12-B verification:
- `tests/Hive.Tests/HiveBootstrapCredentialStoreTests.cs` — bootstrap boundary coverage;
- `tests/Hive.Tests/HiveConfigurationTests.cs` — configuration serialization and explicit legacy-reference rejection;
- `tests/Hive.Tests/HivePersistenceOptionsTests.cs` — configuration-to-SQL option mapping;
- `tests/Hive.Tests/HiveHostCompositionTests.cs` — host composition after bootstrap-boundary integration;
- broader `Hive.Tests` execution: **158/158 passed, 0 failed, 0 skipped**.

Next 1.12-C work is limited to authoritative Provider, ProviderAccount, ExecutionTarget, and AgentDefinition Settings management plus its focused automated coverage.

Manual application verification is not recorded as complete for 1.12-A until the developer actually runs the host and exercises the current authorized behavior.

## Historical verification

Phase 1.11 completion is recorded in `docs/verification/phase-1/1.11.md`.
