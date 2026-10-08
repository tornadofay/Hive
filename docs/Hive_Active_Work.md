# Hive — Active Work

Status: **IN PROGRESS**

## Phase 1.18A Slice 5 — Host Composition, Settings UI & First-Run Default

Authorized by explicit implementation instruction:

`Hive: Start Phase 1.18A Slice 5`

Repository checkpoint at activation: `ae02f2de479e709a8e5df1af045c9b40768592cc`.

### Authorized scope

Implement only the Phase 1.18A Slice 5 boundary:

- wire the authoritative `HivePersistenceConfiguration` into the real host/application persistence composition for both SQL Server and Embedded backends;
- preserve safe candidate-graph replacement so a failed candidate does not displace the published graph;
- complete the existing Persistence Settings page with `Database Setup` and `Data Migration` tabs;
- expose the `Embedded | SQL Server` backend selector and backend-appropriate configuration/status/lifecycle controls;
- keep persistence configuration, readiness/testing, initialization, and full-data migration behind Hive.Management public boundaries;
- preserve the existing SQL Server configuration path and developer/test LocalDB default;
- implement the intended normal end-user first-run Embedded default;
- update the real Example Host Settings flow as required to demonstrate the Slice 5 surface without creating a competing configuration/persistence graph;
- add/update bounded automated coverage for the Slice 5 contracts and host/UI behavior required by the changed implementation.

### Explicit exclusions

- no Phase 1.18A Slice 6 cross-backend hardening/closure;
- no 1.19 vector storage/indexing/search work;
- no new persistence backend;
- no live synchronization, merge migration, partial resource-family migration, or destructive overwrite;
- no unrelated refactoring or unrelated roadmap advancement.

### Verification boundary

Slice 5 stops at its own verification gate:

- real host composition supports both persistence backends without bypassing Hive.Management;
- a failed candidate composition leaves the currently published graph intact;
- backend selection/configuration does not perform implicit data migration;
- migration remains an explicit operation and does not silently activate the destination;
- normal end-user first-run behavior selects Embedded;
- developer/test LocalDB remains SQL Server-based;
- required automated tests/builds are developer-run and recorded from actual results;
- required Example Host manual verification is recorded only when actually performed.

Current state: **IMPLEMENTATION COMPLETE; VERIFICATION PENDING**.

Developer verification evidence received on 2026-10-08: `Hive.Tests` completed with **754 tests: 750 passed, 4 failed, 0 skipped**. All four failures were the same in-scope persistence-settings construction defect: `HivePersistenceDataMigrationSettingsView` assigned `HiveEditorLayout.LabelColumnWidth = 0`, while `HiveEditorLayout` rejects zero values. The same constructor exception was also observed when opening Hive Configuration and when opening Persistence. The invalid migration-only assignment has now been removed; developer rerun is required.

Same-slice remediation completed within the recorded Slice 5 boundary. It remains limited to Database Setup and Data Migration presentation, SQL Server instance discovery/custom selection, authentication-aware field presentation, Embedded storage browsing, source/destination migration layout, top-row Direction + Scope placement, and footer status/actions. No new backend, migration capability, persistence contract, Slice 6 work, or 1.19 work was introduced.

Implementation checkpoint:
- backend-aware real host composition for SQL Server and Embedded;
- Embedded first-run default under application-owned Local AppData storage while the direct/default developer configuration store remains SQL Server LocalDB;
- Persistence Settings `Database Setup | Data Migration` tabs with backend selection, readiness/initialization controls, fixed `All Hive Data` migration scope, and Management-only migration invocation;
- SQL Server instance discovery/custom picker, Browse-enabled Embedded storage, compact grouped setup/migration rows, Destination-left / Source-right migration roles, authentication-aware SQL destination controls, top-row Direction + Scope, and footer status/actions;
- bounded automated coverage for Embedded host composition, first-run defaults, configuration fallback, Settings tabs/backend selector, and the persistence UX behavior.

Verification status: the earlier post-remediation compilation failures and the subsequent runtime construction defect have been remediated within the recorded Slice 5 failure boundary. The latest code change is unverified until the focused/full test rerun, zero-warning developer build, and required Example Host manual verification are completed.

### Verification handoff

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests` for the persistence UX changes; then the full `Hive.Tests` suite and a zero-warning developer build under the repository's standing **Treat warnings as errors** configuration. Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`.
