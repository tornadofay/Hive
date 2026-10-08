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

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

Developer verification evidence received on 2026-10-08: `Hive.Tests` completed with **756 tests: 752 passed, 4 failed, 0 skipped**.

Concrete same-slice failures reported:
- `HivePersistenceSettingsView` throws `ArgumentNullException (control)` during construction because `CreateSqlSection()` passes the not-yet-assigned `_sqlCredentialsField` into `CreateSection()`.
- Clicking Persistence in the Example Host reaches the same construction failure.
- `HiveSqlServerInstanceDiscovery_FormatsLocalInstancesForLocalConnection` expects installed local SQL Server instances to be formatted as `localhost` / `localhost\\HiveSql`, but the implementation currently uses the supplied machine name (`DEVBOX`), producing `DEVBOX` / `DEVBOX\\HiveSql`.
- These failures are within the already-authorized Slice 5 settings/discovery presentation boundary and do not authorize Slice 6 or 1.19 work.

### Verification failure — 2026-10-08 10:54

Developer reported a real Slice 5 manual/runtime failure:

- Database Setup connection test reported `Connection test failed: SQL Server connection test failed.`

The same feedback establishes the migration UI requirement that Source and Destination are independent editable endpoints. The existing Slice 5 implementation still derives the migration source from Hive's currently active persistence configuration in Management and was therefore designed too narrowly.

### Remediation authorization

The user explicitly authorized correction of this requirement after it was restated:

- Source and Destination must both be editable endpoint configurations.
- Migration direction must not depend on the current active Hive backend.
- Management migration must consume the explicit SourceConfiguration supplied by the caller rather than loading the active configuration as the source.
- SQL Server and Embedded source/destination editors must remain available in both directions.
- This is a corrective expansion of the existing Slice 5 migration operation boundary, not authorization for Slice 6 or 1.19.

### New developer/manual feedback — 2026-10-08

Additional same-slice Slice 5 UX failures were reported after the previous remediation:
- Database Setup fields are unnecessarily full-width: Backend, SQL Server database, Authentication, and related SQL inputs should use bounded professional field widths rather than filling the entire workspace.
- SQL Server instance discovery should happen automatically when SQL Server is selected/opened; the user should not need to press Refresh for the normal initial population. Refresh remains a manual retry action.
- The SQL Server database field can appear empty after configuration load even though Hive has a deterministic package database name; the UI should preserve/display the generated database name when the loaded value is blank.
- The SQL Server port should have a useful default of 1433.
- The Embedded database-file field needs a larger path editor with Browse directly adjacent.
- Unused/overflowing vertical space and unnecessary horizontal/vertical scrolling should be removed; SQL initialization controls should be positioned compactly enough that the normal settings surface fits without avoidable scrolling.
- Data Migration should place SOURCE on the left and DESTINATION on the right.
- Data Migration SQL destination controls also need bounded widths so they do not overwhelm or overflow the workspace.
- The migration direction must remain user-selectable instead of being silently rewritten from the currently active backend. The current Management migration contract still reads the source from the active persistence graph, so the UI must expose the selected direction honestly and prevent an invalid execution rather than silently changing the user's choice.

These remain within the authorized Slice 5 presentation/interaction boundary. They do not authorize Slice 6, a new migration contract, or 1.19.

### Remediation completed

- The SQL credentials field is initialized before `CreateSqlSection()` consumes it; the structured section/field hierarchy is preserved.
- Installed local SQL Server instances are normalized to `localhost` / `localhost\\<Instance>`, with network-discovered remote names left unchanged.
- Database Setup now uses bounded professional widths for Backend, Database, Authentication, SQL Server, SQL security, and credential editors. Hidden backend sections collapse their row, Embedded storage keeps a wide path editor with Browse directly beside it, and initialization controls are positioned compactly near the top of the backend section.
- SQL Server now shows port **1433** by default, and a blank loaded SQL database name falls back to Hive's deterministic package database name.
- Selecting SQL Server triggers instance discovery automatically; Refresh remains a manual retry. Custom server input expands directly below the instance selector.
- Data Migration now places SOURCE on the left and DESTINATION on the right. SQL destination editors are sized for the split destination card, while Embedded storage uses the available destination width.
- Migration direction remains user-selectable instead of being silently rewritten on refresh. The selected source is visibly marked **INACTIVE** when it does not match the active backend, and readiness/migration execution is blocked in that state rather than silently changing the user's choice. This preserves the current Management migration contract without creating a new source-configuration API.
- Focused automated coverage now checks bounded setup widths, default database/port, custom-server presentation, source/destination order, and normal-workspace scroll state.
- The affected Slice 5 implementation and documentation were re-reviewed after the new UX feedback. No Slice 6, new backend, new migration contract, or 1.19 work was introduced.

### Verification handoff

Developer rerun is required:

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests` for the persistence UX/discovery changes; then the full `Hive.Tests` suite and a zero-warning developer build under the repository's standing **Treat warnings as errors** configuration.
