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

Current state: **VERIFICATION PENDING**.

Developer verification evidence received on 2026-10-08: `Hive.Tests` completed with **756 tests: 752 passed, 4 failed, 0 skipped**.

Concrete same-slice failures reported:
- `HivePersistenceSettingsView` throws `ArgumentNullException (control)` during construction because `CreateSqlSection()` passes the not-yet-assigned `_sqlCredentialsField` into `CreateSection()`.
- Clicking Persistence in the Example Host reaches the same construction failure.
- `HiveSqlServerInstanceDiscovery_FormatsLocalInstancesForLocalConnection` expects installed local SQL Server instances to be formatted as `localhost` / `localhost\\HiveSql`, but the implementation currently uses the supplied machine name (`DEVBOX`), producing `DEVBOX` / `DEVBOX\\HiveSql`.
- These failures are within the already-authorized Slice 5 settings/discovery presentation boundary and do not authorize Slice 6 or 1.19 work.

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

- The SQL credentials field is now created before `CreateSqlSection()` consumes it; the structured section/field hierarchy is unchanged.
- Installed local SQL Server instance names are now normalized to `localhost` for the default instance and `localhost\\<Instance>` for named instances. Network-discovered remote names remain unchanged.
- The focused discovery coverage now asserts the `localhost` representation and no longer passes an obsolete machine-name formatting argument.
- The affected Slice 5 implementation and documentation were re-reviewed for this failure boundary only. No implementation outside the recorded remediation boundary was changed.

Developer rerun is now required:

### Remediation completed

- Database Setup single-value editors now use bounded widths: Backend and Authentication are compact, Database is bounded, SQL Server picker is bounded while retaining a useful path for Custom input, and the Embedded database file path keeps the main available width with Browse directly beside it.
- Hidden backend sections now collapse their editor row instead of leaving a large blank area. Initialization controls are grouped earlier and compactly so the normal Database Setup surface does not grow vertically without need.
- SQL Server now presents port **1433** by default, and a blank loaded SQL database name falls back to Hive's deterministic package database name instead of rendering empty.
- Selecting SQL Server now triggers instance discovery automatically; the explicit Refresh action remains available as a retry/manual refresh.
- Custom SQL server input expands directly below the instance selector with a layout-aware picker height.
- Data Migration now presents SOURCE on the left and DESTINATION on the right. SQL destination editors use widths appropriate for the split card, while Embedded storage uses the available destination width.
- Migration direction remains user-selectable. The UI does not silently rewrite the user's choice on refresh; when the selected source backend is not the active Hive backend, the source is marked INACTIVE and migration/readiness execution is blocked until a matching direction is selected. This preserves the current Management contract without introducing a new source-configuration migration API.
- Focused automated coverage now checks the bounded setup fields, default database/port, custom-server presentation, source/destination column order, and normal-workspace scroll state.
- The affected Slice 5 implementation and documentation were re-reviewed after the reported UX feedback. No Slice 6, new migration contract, backend, or 1.19 work was introduced.

### Verification handoff

Developer rerun is required:

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests` for the persistence UX/discovery changes; then the full `Hive.Tests` suite and a zero-warning developer build under the repository's standing **Treat warnings as errors** configuration.
