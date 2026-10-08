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

### Verification failure — 2026-10-08 10:54

Developer reported a real Slice 5 manual/runtime failure:

- Database Setup connection test reported `Connection test failed: SQL Server connection test failed.`

The same feedback establishes the migration requirement that Source and Destination are independent editable endpoints. The previous Slice 5 implementation incorrectly derived the migration source from Hive's currently active persistence configuration in Management; this was corrected by making the Management request carry both source and destination configurations explicitly.

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
- The migration direction must remain user-selectable instead of being silently rewritten from the active backend. Source and Destination are now independent explicit endpoint configurations, and the public Management migration request carries both endpoints.

These remain within the authorized Slice 5 presentation/interaction and Management-boundary remediation. Slice 5 explicitly authorizes the corrected public migration request carrying SourceConfiguration + DestinationConfiguration. They do not authorize Slice 6 or 1.19.

### Remediation completed

- Database Setup uses compact/bounded control widths; the Backend selector is compact, the Embedded database-file editor remains responsively wide with Browse immediately beside it, and hidden backend sections do not leave large blank regions.
- SQL Server instance discovery combines network enumeration with installed local-instance registry inventory, normalizes installed local instances to `localhost` / `localhost\\<Instance>`, automatically refreshes when SQL Server is selected, and prefers an installed local instance when one is available.
- SQL Server port handling no longer forces 1433 onto named instances. Default/custom endpoints use **1433** when no explicit port is supplied; named instances leave Port unset so SQL Server instance resolution can determine the effective port unless the user explicitly enters one.
- A blank SQL database name falls back to Hive's deterministic package database name. Connection-test status identifies the server/database being tested without exposing credentials.
- Data Migration now has two independent editable endpoint editors. Source and Destination each expose the applicable SQL Server or Embedded endpoint configuration. Direction only selects which backend is assigned to each endpoint; neither endpoint is derived from the active Hive backend.
- The public `HivePersistenceMigrationRequest` carries explicit `SourceConfiguration` and `DestinationConfiguration`. `Hive.Management` passes both directly to the existing persistence migrator instead of loading the active persistence configuration as the source.
- Embedded source/destination path inputs are single-line. SQL endpoint editors are arranged and bounded for the half-width Source/Destination cards.
- The Embedded → SQL migration path now uses the destination SQL credential for the destination connection.
- Focused regression coverage checks compact setup sizing, named-instance port handling, endpoint editability/order, explicit-source Management migration, and normal-workspace scroll state.
- The Slice 5 implementation and documentation were re-reviewed after the reported runtime/UX feedback. No Slice 6 or 1.19 work was introduced.

### Compilation failure — 2026-10-08 18:45

Developer verification reported eight in-scope Slice 5 compilation failures after the explicit editable-endpoint remediation:

- \`HivePersistenceDataMigrationSettingsView\`: incorrect \`SqlAuthenticationSelector\` type exposure in two locations.
- \`HivePersistenceDataMigrationSettingsView\`: nested endpoint editor called \`FindForm()\` as though it were itself a Control.
- \`HivePersistenceDataMigrationSettingsView\`: outer top-row layout referenced the endpoint editor's private \`CreateFieldBlock\` helper.
- \`HivePersistenceDataMigrationSettingsView\`: nullable Management test values reached \`FormatStatus\` without a non-null guard.
- \`HivePersistenceDataMigrator\`: Embedded → SQL helper parameter did not match the \`destinationSqlCredential\` caller.
- \`PersistenceDataMigrationExampleView\`: Example Host still used the old one-argument \`HivePersistenceMigrationRequest\` constructor.

These are same-slice remediation defects in the implementation just changed; no new roadmap capability is being requested.

### Verification failure — 2026-10-08 18:55

Developer rerun reported **758 tests: 752 passed, 6 failed, 0 skipped**. The six failures are within the Slice 5 UI/picker remediation boundary:

- `HivePersistenceSettingsView_UsesSqlServerPickerCustomChoiceAndFooterStatus`: `HivePersistenceDataMigrationSettingsView.MigrationEndpointEditor` construction throws `ArgumentException` because its fixed-size one-cell `TableLayoutPanel` attempts to add both backend panels.
- `HivePersistenceDataMigrationView_UsesTopDirectionAndScopeAndSqlAuthentication`: the same endpoint-editor fixed-size table-layout construction failure.
- `HiveSqlServerInstancePicker_DefaultsPortByInstanceKind`: after the picker is reset with only `localhost` and a null preferred server, the stale named-instance selection is retained as a custom endpoint instead of selecting the discovered local default and applying port 1433.

These failures are same-slice regressions. They do not authorize Slice 6 or 1.19 work.

### Compilation remediation completed — 2026-10-08 18:45

- \`HivePersistenceDataMigrationSettingsView\` now exposes the SQL authentication selector as the actual \`HiveComboBox\`; the enum-valued authentication state remains a separate property.
- Embedded migration Browse now uses the nested endpoint editor's actual root control when resolving the owning Form.
- The outer Direction / Scope row now uses its own field-block helper instead of reaching into the nested endpoint editor.
- Successful source/destination connection-test results are null-guarded before status formatting.
- \`HivePersistenceDataMigrator.MigrateEmbeddedToSqlAsync\` now receives the destination SQL credential under the same parameter name used by the caller.
- \`PersistenceDataMigrationExampleView\` now constructs \`HivePersistenceMigrationRequest\` with both explicit source and destination configurations.
- The full \`HivePersistenceDataMigrationTests\` file was previously restored after an accidental reduction; the final diff against the Slice 5 activation checkpoint is now bounded to the intended migration-test edits.

### Latest verification remediation — 2026-10-08 18:55

- Replaced the migration endpoint editor's overlapping fixed-size `TableLayoutPanel` host with a dedicated `Panel` that safely overlays the two backend-specific editors and brings the selected backend to the front. This removes the constructor-time `Additional Rows or Columns cannot be created` failure.
- Kept both Source and Destination endpoint editors independent; no migration contract or backend scope was widened.
- Corrected `HiveSqlServerInstancePicker.SetDiscoveredInstances` so a null preferred server means "choose the discovered local/default instance", while `RefreshAsync` still supplies the current server explicitly when preserving an existing selection is intended. This prevents a stale named-instance selection from becoming an unintended Custom endpoint during a fresh discovered-instance population.

Developer re-verification is still required; the reported 758-test run remains the latest executed result.

### Verification failure — 2026-10-08 21:24

Developer rerun reported **758 tests: 756 passed, 2 failed, 0 skipped**. The two failures remain within the authorized Slice 5 UI/layout boundary:

- `HivePersistenceSettingsView_UsesDatabaseSetupAndDataMigrationTabs`: the SQL Authentication selector is measured at **946 px**, outside the required bounded field width of **180–240 px**. The failing assertion is the focused compact-sizing contract for the Database Setup surface.
- `HivePersistenceSettingsAndMigrationFitNormalWorkspaceWithoutScrollOverflow`: the Database Setup `HiveScrollHost` still reports `VerticalScrollState.CanScroll == true` at the normal 1160×760 workspace size, violating the Slice 5 requirement to fit the intended settings surface without avoidable vertical scrolling.

These are same-slice remediation failures. They do not authorize Slice 6 or 1.19 work.

### Verification failure — 2026-10-08 21:xx (latest developer rerun)

Developer rerun reported **758 tests: 756 passed, 2 failed, 0 skipped**.

- `HivePersistenceSettingsAndMigrationFitNormalWorkspaceWithoutScrollOverflow`: the Database Setup `HiveScrollHost` still reports `VerticalScrollState.CanScroll == true` at the normal 1160×760 workspace size. The remaining failure is within the existing Slice 5 setup-layout/vertical-fit boundary.
- `HivePersistenceSettingsView_UsesDatabaseSetupAndDataMigrationTabs`: the Data Migration tab's hosted control is expected to have `DockStyle.Fill`, but the first control currently reports `DockStyle.None`. The remaining failure is within the existing Slice 5 tab-host composition boundary.

These failures are same-slice remediation defects. They do not authorize Slice 6 or 1.19 work.

### Remediation status

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

Implementation changes may now proceed only for the two recorded failures above. After remediation, Active Work must return to **VERIFICATION PENDING** with the focused/full-suite, zero-warning build, and Example Host verification requirements preserved.

### Remediation completed — 2026-10-08 21:24

- The Database Setup Backend selector now has a structurally bounded 80px field host, so the shared TableLayoutPanel sizing cannot stretch it to the full workspace width.
- All explicitly bounded Database Setup editors now use fixed-width hosts plus matching minimum/maximum width constraints, preserving the requested professional field widths through nested WinForms layout.
- The SQL password credential row now collapses its containing SQL section row when password authentication is not selected, preventing hidden credential controls from consuming layout height.
- Database Setup vertical spacing was tightened within this Slice 5 view so the normal workspace does not reserve avoidable padding between sections, fields, and descriptions.
- No migration contract, persistence backend, host composition boundary, Slice 6 behavior, or 1.19 work was changed.

Current state: **VERIFICATION PENDING**.

Developer rerun is required against the corrected source. Preserve the existing focused/full-suite, zero-warning build, and Example Host manual verification requirements below.

### Verification handoff

Developer rerun is required:

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests` for the persistence UX/discovery changes; then the full `Hive.Tests` suite and a zero-warning developer build under the repository's standing **Treat warnings as errors** configuration.
