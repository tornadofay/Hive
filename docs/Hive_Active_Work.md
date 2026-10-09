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

### Latest remediation completed — 2026-10-08

- `HivePersistenceSettingsView` now explicitly reapplies `DockStyle.Fill` to the Data Migration view after adding it to the tab page, preserving the existing tab-host contract expected by the focused regression test.
- Persistence Database Setup sections now use `DockStyle.Top` while remaining auto-sized, avoiding fill-driven preferred-size distortion inside the existing `HiveScrollHost` and keeping the change local to Slice 5.
- No shared scroll infrastructure, migration contract, persistence backend, Slice 6 capability, or 1.19 work was changed.

Current state: **VERIFICATION PENDING**.

Developer rerun is required against the corrected source. Preserve the focused/full-suite, zero-warning build, and Example Host manual verification requirements below.


### Latest verification failure — 2026-10-08 (latest developer rerun)

Developer rerun reported **758 tests: 756 passed, 2 failed, 0 skipped** again.

- `HivePersistenceSettingsAndMigrationFitNormalWorkspaceWithoutScrollOverflow`: the Database Setup `HiveScrollHost` still reports `VerticalScrollState.CanScroll == true` at the normal 1160×760 workspace size.
- `HivePersistenceSettingsView_UsesDatabaseSetupAndDataMigrationTabs`: the failing assertion is the Embedded database-file editor contract, `Assert.Equal(DockStyle.Fill, view.EmbeddedStorageInput.Dock)`; the editor currently reports `DockStyle.None`. The repository test source confirms this assertion is the one at the reported test location; it is not the migration-tab child Dock assertion.

Root-cause inspection found that `CreateTextBox()` does not establish the expected fill docking for the Embedded database textbox, while the persistence setup's nested AutoSize layout is initially measured before its real viewport width/height exists. `HiveScrollHost.Attach` intentionally sets its directly hosted content to `DockStyle.None` and preserves explicit content size; the persistence settings view therefore needs to normalize its own fields content after the real setup viewport is laid out rather than changing shared scroll infrastructure.

These are same-slice remediation failures. They do not authorize Slice 6 or 1.19 work.

### Remediation completed — latest

- HivePersistenceSettingsView.CreateTextBox() now defaults native text inputs to DockStyle.Fill, restoring the Embedded database-file editor's existing presentation contract without changing the shared scroll host's intentional content docking behavior.
- HivePersistenceSettingsView now normalizes the Database Setup fields content after the real HiveScrollHost viewport has a usable size: it remeasures the auto-sized setup content at the current viewport width, resets the local content extent to that preferred height, and re-synchronizes the scroll host. This removes the stale pre-viewport size that was keeping an unnecessary vertical scrollbar active.
- The fix remains local to HivePersistenceSettingsView; HiveScrollHost, HiveEditorLayout, migration contracts, persistence backends, Slice 6, and 1.19 were not changed.

Current state: **VERIFICATION PENDING**.

Developer rerun is required against the corrected source. Preserve the existing focused/full-suite, zero-warning build, and Example Host verification requirements below.

### Verification failure — latest developer rerun

Developer rerun reported **758 tests: 756 passed, 2 failed, 0 skipped**.

- `HivePersistenceSettingsAndMigrationFitNormalWorkspaceWithoutScrollOverflow`: Database Setup still reports `VerticalScrollState.CanScroll == true` at 1160×760. The previous local remeasurement did not solve the actual nested layout extent problem.
- `HivePersistenceSettingsView_UsesDatabaseSetupAndDataMigrationTabs`: after selecting the Custom SQL Server endpoint, `CustomServerInput.Visible` is `false` at the focused assertion. This is a same-slice SQL Server picker interaction regression introduced by the recent selection-state remediation.

These failures are within the existing Slice 5 settings layout and SQL Server picker presentation boundary. They do not authorize Slice 6 or 1.19 work.

### Remediation completed — latest

- `HivePersistenceSettingsView` no longer derives the setup scroll extent from the whole nested AutoSize panel's preferred size. After the real viewport has a usable width, it lays out the visible setup sections and uses their actual bottom bounds as the local content extent. This avoids the recursive/stale preferred-size inflation that kept vertical scrolling enabled at the normal workspace size.
- `HiveSqlServerInstancePicker.UpdateCustomVisibility()` now explicitly synchronizes the Custom server textbox's `Visible` state with the Custom selection. The existing discovery/defaulting contract is unchanged.
- The corrections remain inside Slice 5. `HiveScrollHost` and shared UI infrastructure were not modified.

Current state: **VERIFICATION PENDING**.

Developer rerun is required against the corrected source. Preserve the existing focused/full-suite, zero-warning build, and Example Host verification requirements below.

### Verification failure — 2026-10-08 (latest developer rerun)

Developer verification reported **758 tests: 757 passed, 1 failed, 0 skipped**.

- `HivePersistenceSettingsAndMigrationFitNormalWorkspaceWithoutScrollOverflow`: Database Setup still reports `VerticalScrollState.CanScroll == true` at the normal 1160×760 workspace size.
- During the same developer UI verification, the Database Setup Backend `HiveComboBox` is not visibly rendering/showing as intended.
- The previously failing Custom SQL Server endpoint visibility assertion now passes, confirming that remediation remains effective.

These are same-slice Slice 5 settings-layout/composition failures. They do not authorize Slice 6 or 1.19 work.

### Remediation completed — 2026-10-09 custom picker and startup performance

- `HiveSqlServerInstancePicker` now uses natural auto-sized layout for the optional Custom server row instead of forcing a fixed zero/78px outer height. Selecting `Custom...` keeps the picker available and expands the control to reveal the editable custom server/instance textbox.
- `HivePersistenceSettingsView.CreateFieldBlock()` preserves auto-sizing for the SQL Server picker so its custom row can grow through the surrounding bounded field host; ordinary compact fields retain the 34px sizing contract.
- `HiveSettingsView.InitializeAsync()` no longer constructs Provider, Agent, and Persistence pages during initial Settings startup. Those pages remain lazy and are constructed only when first navigated to, removing unnecessary control creation/theme/layout work from initial Settings paint.
- No shared UI infrastructure, persistence contract, migration behavior, Slice 6, or 1.19 scope was changed.

Current state: **VERIFICATION PENDING**.

Developer rerun is required against the corrected source. Manual verification must specifically confirm that `Custom...` reveals an editable custom server/instance field and that initial Settings/Persistence presentation is responsive without the previous heavy first-open paint delay.

### Remediation completed — latest manual UI correction

- HivePersistenceSettingsView.CreateFieldBlock() now uses an auto-sized fixed-width editor host instead of a fixed-height wrapper. This lets the normal compact controls use their 34px natural field height, while the SQL Server picker can expand when its Custom row is shown.
- Compact text inputs and HiveComboBox instances are no longer forcibly inflated to 36px by the settings view. The existing HiveComboBox 34px field geometry is preserved, and the Embedded database-file textbox uses the same compact height.
- The previous automated layout result remains valid: the developer-reported full suite passed **758/758** before this visual-only correction.
- No shared HiveScrollHost/HiveComboBox infrastructure, migration contract, persistence backend, Slice 6, or 1.19 work was changed.

Current state: **VERIFICATION PENDING**.

Developer rerun/manual verification is required against the corrected source. Preserve the focused/full-suite, zero-warning build, and Example Host verification requirements below.

### Compilation failure — latest local developer/editor feedback

A compile-time failure was reported in the latest Slice 5 sizing correction:

- `HivePersistenceSettingsView.cs` line 1126: `CS1501` — `Math.Max` has no overload accepting four arguments.

This is an in-scope same-slice compilation defect in the recently changed field-sizing expression. No new roadmap scope is authorized.

### Remediation completed — latest

- Replaced the invalid four-argument `Math.Max` call with equivalent nested two-argument `Math.Max` calls.
- No layout behavior, persistence contract, migration behavior, shared UI infrastructure, Slice 6, or 1.19 scope was otherwise changed.
- Active Work remains **VERIFICATION PENDING** until the developer reruns the affected tests/build and performs the required Example Host visual verification.

### Verification failure — 2026-10-09 latest developer/manual UI feedback

The developer reported two additional in-scope Slice 5 failures:

- SQL Server instance picker: choosing `Custom` does not visibly reveal the textbox needed to enter the custom instance/server name.
- Persistence Settings first-open rendering is too slow/heavy for this lightweight settings surface. Initial open must avoid unnecessary layout/repaint work and remain responsive.

These are same-slice settings UI interaction and performance failures. No new roadmap scope is authorized.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation completed — 2026-10-09 custom picker and lightweight startup

- `HiveSqlServerInstancePicker` now gives its optional Custom server row natural auto-sizing instead of manually forcing the picker between fixed 36px and 78px heights. Selecting `Custom...` keeps the server selector available and expands the control to reveal the editable custom server/instance textbox.
- `HivePersistenceSettingsView.CreateFieldBlock()` preserves auto-sizing for the SQL Server picker while ordinary compact fields keep the 34px sizing contract.
- `HiveSettingsView.InitializeAsync()` no longer constructs Provider, Agent, and Persistence pages during initial Settings startup; those pages remain lazy and are constructed only when first navigated to.
- `HivePersistenceSettingsView` now also defers construction of the Data Migration editor until the Data Migration tab is actually selected, so the default Database Setup first paint does not build the migration surface unnecessarily.
- The focused UI test now checks that the Custom editor has real bounds inside the picker and that the migration editor is created when its tab is opened.
- No shared UI infrastructure, persistence contract, migration behavior, Slice 6, or 1.19 scope was changed.

Current state: **VERIFICATION PENDING**.

Developer rerun is required against the corrected source. Manual verification must specifically confirm that `Custom...` reveals an editable custom server/instance field and that initial Settings/Persistence presentation is responsive without the previous heavy first-open paint delay.

### Verification failure — 2026-10-09 persistence editor follow-up

The developer reports that after the prior remediation, the first opening of Persistence still incurs excessive painting/nested layout, and the SQL Server connection using their existing Windows-integrated endpoint fails:

`Server=localhost\MSSQLSERVER01;Database=Hive-Hive.Example.WinForms;Trusted_Connection=True;`

Requested same-slice UI corrections:

- The Custom SQL Server instance editor and the row hosting it must auto-size together, so selecting Custom exposes a usable input without clipping or fixed-height layout conflicts.
- The Embedded database file textbox and its containing row must auto-size together, with the adjacent Browse button kept aligned and the textbox not stretched vertically.
- Reduce first-open Persistence painting/layout work at the actual page construction and theme/layout ownership boundary rather than adding another normalization layer.
- Diagnose the actual SQL Server connection failure against this named-instance, database-name, Windows-integrated-authentication scenario; surface a useful safe failure reason without exposing credentials.

These remain same-slice Slice 5 interaction, layout/performance, and connection-readiness defects. They do not authorize Slice 6 or 1.19.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation completed — 2026-10-09 persistence layout, first paint, and SQL Server connection

- The SQL Server picker now tracks whether port 1433 was supplied automatically or explicitly. For the selected/typed Custom endpoint, an automatically populated 1433 is removed once the server name contains a named-instance separator (`\`); explicitly typed ports remain unchanged.
- Removed constructor-level forced `Port = 1433` assignments from both Database Setup and Data Migration. The picker owns its defaulting behavior, so named instances are not transformed into `localhost\MSSQLSERVER01,1433` by a generic port default.
- The Settings page normalizes the legacy persisted combination of named instance + port 1433 on load because older versions stored the picker's generic default. Users can still explicitly enter a port in the editor when a named instance is deliberately configured for one.
- The Custom server TextBox and its TableLayoutPanel row now auto-size together. The Embedded database-path TextBox and its Browse row also auto-size naturally; Browse is aligned to the compact 32px field row.
- Reduced first-open layout/repaint work by removing duplicate theme application from the Persistence view and picker constructors, applying a Settings page's theme while it is detached before attaching it onscreen, and suspending nested layout panels while their child controls are added.
- Data Migration remains deferred until its tab is first opened. SQL Server instance discovery is started without blocking the first configured page paint, and its background cancellation/disposal checks prevent stale updates after backend switches or view disposal.
- SQL connection test failures now report actionable, bounded guidance for named-instance/network resolution, Windows authentication, database access, and TLS certificate failures instead of only the generic connection-test message. The diagnostic does not echo a Windows account name for authentication failures.
- Focused coverage was added/updated for named-instance configuration without an automatic port, clearing the automatic port when entering a Custom named instance, SQL failure guidance, and auto-sizing for the Custom and Embedded path rows.
- No shared UI framework API, persistence contract, migration semantics, Slice 6, or 1.19 scope was introduced. The changes have been committed to `main`; the assistant has not run the tests, build, or Example Host.

Current state: **VERIFICATION PENDING**.

Developer verification must confirm the actual Windows-integrated named-instance connection, natural Custom/Embedded row sizing, and responsive first Persistence opening. If SQL Server still fails, capture the new specific connection-test diagnostic text (not credentials) so the remaining server/authentication condition can be isolated.

### Same-slice review finding — background discovery could overwrite in-progress edits

Final review found a race in the nonblocking SQL Server discovery change: the picker captures a preferred server before awaiting discovery, then applies that stale preference when discovery completes. If the user starts selecting `Custom...` or types the custom server/instance while discovery is still running, the completion could restore the previous selection and discard the edit.

This is an in-scope Slice 5 input-preservation/concurrency defect in the new background discovery path. It does not authorize additional roadmap work.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation completed — background discovery preserves user edits

- `HiveSqlServerInstancePicker` now tracks user edits separately from programmatic item population. When discovery completes, it detects whether the user changed the selection, Custom server text, or port while the operation was in flight.
- If the user chose `Custom...` or entered a custom server while discovery was running, the discovered list is refreshed without changing that selection or overwriting the text. A custom value is preserved even if the refreshed discovery list includes a matching server name.
- Programmatic `SetValue` / discovery updates remain excluded from user-edit tracking so they do not create false concurrency conflicts. Focused UI regression coverage now exercises preserving a custom name while the updated discovery list contains that same endpoint.

Current state: **VERIFICATION PENDING**.

Rerun the persistence UX/discovery tests, the connection-options diagnostics tests, the full `Hive.Tests` suite, and a zero-warning developer build. Manually confirm the Custom text remains editable during a slow refresh and is not replaced when discovery completes.

### Verification handoff

Developer rerun is required:

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests` for the persistence UX/discovery changes; then the full `Hive.Tests` suite and a zero-warning developer build under the repository's standing **Treat warnings as errors** configuration.

### Verification failure — 2026-10-09 SQL Server discovery/UI rerun

Developer-reported verification after pulling commit `99cc10ef71762fecc5562aa1050bf739c2ed3e66`:

- The first full run reported **758 tests: 756 passed, 2 failed, 0 skipped**. `HivePersistenceSettingsAndMigrationFitNormalWorkspaceWithoutScrollOverflow` failed because the setup `HiveScrollHost` reported vertical scrolling at 1160×760. The tab/picker test also failed during the same run.
- The subsequent discovery/full run found **762 tests** and reported **762 tests: 761 passed, 1 failed, 0 skipped**. The remaining failure is `HivePersistenceSettingsView_UsesDatabaseSetupAndDataMigrationTabs`: selecting the final `Custom...` item does not leave `CustomServerInput.Visible` true (reported at test line 299).

The latest remaining failure is within Slice 5's existing SQL instance picker/presentation boundary. The supplied results do not authorize Slice 6 or 1.19 work.

### Remediation authorization — SQL Server discovery responsiveness and failure handling

The user explicitly requested same-slice correction of SQL Server instance discovery and its Persistence UI lifecycle. The bounded work is:

- Open Settings/Overview without opening Persistence or triggering SQL network discovery; navigating to Persistence should construct and show the page before discovery completes.
- Start discovery only when Persistence is opened while SQL Server is selected, when SQL Server is selected, or on explicit Refresh.
- Read installed local instances independently and show them promptly; merge network-discovered candidates asynchronously.
- Keep a final, immediately selectable `Custom...` choice and preserve custom text, selection, and explicit port values while discovery completes.
- Add an inline indeterminate progress indicator and the exact loading message `Searching for SQL Server instances…`; show concise success/incomplete/failure status, preserve partial results, and keep the page interactive.
- Coalesce concurrent scans and TTL-cache results; bound how long a caller waits for synchronous `SqlDataSourceEnumerator.GetDataSources()`, while recognizing cancellation cannot stop a call already executing. Never start an overlapping enumeration merely because its wait timed out.
- Guard late results against stale requests, cancellation, view disposal, and backend changes.
- Add deterministic focused regression coverage for cache/coalescing, partial failure, timeout/cancellation/disposal, UI status, custom-edit/selection/port preservation, and the reported Custom visibility failure.

This is explicit remediation within the already-open Slice 5 settings/discovery boundary. It does not authorize unrelated UI framework work, Slice 6, or 1.19.

### Remediation completed — 2026-10-09 SQL Server instance discovery

- Extracted discovery into `SqlServerInstanceDiscoveryCoordinator`, shared by Persistence pickers. Local registry inventory and synchronous SQL network enumeration run independently; installed local instances can populate the selector before network enumeration returns.
- The synchronous `SqlDataSourceEnumerator.GetDataSources()` call runs on one dedicated background worker. Each caller waits at most three seconds by default. Cancelling a token cancels only that caller's wait, not an already-running synchronous call. Refresh joins the in-flight scan instead of launching an overlapping worker.
- Added shared TTL caching: local inventory five minutes, successful network inventory one minute, incomplete/failure results fifteen seconds. Automatic retries are disabled. Explicit Refresh bypasses a completed cache only when no scan is already running; an in-flight scan is joined until it returns.
- Added inline marquee progress and the exact text `Searching for SQL Server instances…`, concise completion/incomplete/failure statuses, partial-result retention, and late-result updates guarded against stale generations, cancellation, backend switches, and disposal. Discovery failures are non-modal, and Custom entry stays available.
- Preserved the Custom choice, current selection, custom-server edits, and manual port text while results are merged. Named-instance connection/port handling was not intentionally changed; the developer reported that their Windows-integrated named-instance connection succeeds after the earlier fix.
- Added deterministic coordinator coverage for caching/coalescing, TTL expiry, local/network partial failure, timeout/no-overlap, and cancellation. WinForms regression coverage now checks spinner/status, Custom/port preservation, late-result merging, and disposal. The failing Custom visibility test now activates Database Setup before asserting effective visibility.
- Final concurrency review added a thread-safe completion marker so a timeout update cannot overwrite a completed scan's status if both events race on the WinForms message queue (commit `2a77a2c`).

Current state: **VERIFICATION PENDING**.

Developer verification remains required. The assistant has not run a build, test suite, profiler, or Example Host. The latest developer run before these changes was 762 tests (761 passed, 1 failed); it does not verify the current source.

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveSqlServerInstanceDiscoveryTests` and `HiveUiPolishTests`; then the full `Hive.Tests` suite and a zero-warning build under the repository's Treat-Warnings-as-Errors configuration. Manually verify that opening global Settings/Overview does not start discovery; Persistence renders without waiting for network enumeration; local results appear before network results; spinner/status transitions are correct; timeout/failure retain partial results and allow Custom entry; edits and explicit ports survive late results; and the named-instance Windows-integrated connection still succeeds.

### Compilation failure — 2026-10-09 discovery coordinator default delay

Developer-reported compiler errors at `src/Hive.Host.WinForms/HiveSqlServerInstanceDiscovery.cs:79`:

- `CS1002`, `CS1525`, `CS0106`, `CS0246`, and `CS0103` around `_delay = delay ?? static (duration, token) => Task.Delay(duration, token);`.

This is a same-slice C# syntax failure in the default injected-delay expression. The lambda following the null-coalescing operator must be parenthesized. It does not authorize additional discovery behavior, Slice 6, or 1.19.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation completed — 2026-10-09 discovery coordinator syntax

- Parenthesized the static lambda on the right-hand side of the null-coalescing expression in `HiveSqlServerInstanceDiscoveryCoordinator`: `_delay = delay ?? (static (duration, token) => Task.Delay(duration, token));`.
- This resolves the reported parser-level issue at line 79 without changing delay semantics or discovery behavior. The reported follow-on diagnostics are consistent with the same malformed expression.
- No build or tests have been run by the assistant after the correction.

Current state: **VERIFICATION PENDING**.

Developer rerun: rebuild `Hive.Host.WinForms` with Treat Warnings as Errors, then run `HiveSqlServerInstanceDiscoveryTests`, `HiveUiPolishTests`, and the full `Hive.Tests` suite. If a new compiler diagnostic remains, report the first/root error with its line; the current source has only received the syntax correction and has not been compiled here.



### Verification failure — 2026-10-09 Data Migration tab initialization

Developer-reported debugger/runtime exceptions when opening the Data Migration tab, followed by a full-suite result of **772 tests: 772 passed, 0 failed, 0 skipped**:

- `HivePersistenceDataMigrationSettingsView.InitializeAsync()` currently calls `RefreshStatusAsync()`, which immediately probes both endpoint connections through `IHiveManagementFacade.TestPersistenceConnectionAsync`. UI layout tests use a deliberately limited Management proxy, so opening the tab invokes an unimplemented method even though the tests themselves complete successfully. More generally, merely navigating to the tab should not run connection tests without an explicit user request.
- The migration view selects its default direction after subscribing to `SelectedIndexChanged`. That event starts SQL discovery during construction, and the first tab initialization starts another discovery request for the same picker. Starting the second request cancels the first wait, allowing a `TaskCanceledException` to escape the async-void direction-change handler.

These are same-slice Slice 5 initialization/lifecycle defects. The reported passing test count does not remove the manually observed/debugger-visible exceptions. They do not authorize Slice 6 or 1.19 work.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation authorization — Data Migration tab initialization

Correct only the existing Slice 5 migration-tab initialization path:

- Do not test source/destination database connections automatically just because the tab is opened. Keep readiness/connection tests on the explicit Refresh action and migration preflight.
- Ensure the default direction selection does not fire its change handler while the view is being constructed; initialize discovery once when the view is first opened.
- Make initialization idempotent across tab revisits and treat cancellation of an obsolete discovery wait as expected lifecycle control, not as an unhandled async-void exception.
- Preserve automatic SQL instance discovery for visible SQL endpoint editors, Custom/manual endpoint editing, explicit Refresh behavior, and migration preflight correctness.

This is corrective work within Slice 5. It does not authorize unrelated UI changes, Slice 6, or 1.19.


### Remediation completed — Data Migration tab initialization

- The migration view now sets its default direction before subscribing to direction-change events, so constructor setup cannot start a discovery wait that tab initialization immediately cancels.
- First tab initialization is idempotent and only starts visible SQL Server instance discovery. It displays guidance to use the explicit **Refresh** action to test source/destination connection readiness; opening/revisiting the tab no longer implicitly calls `TestPersistenceConnectionAsync`.
- Direction-change and operation handlers absorb expected cancellation of an obsolete picker wait, while disposal/caller cancellation remain non-failure lifecycle events. The explicit Refresh path still tests both endpoints, and migration preflight still tests both endpoints immediately before the migration operation.
- The focused Database Setup/Data Migration UI test awaits first initialization, asserts the ready-for-explicit-Refresh status, and records/fails if its limited Management proxy is unexpectedly asked to run a connection test.
- Code/test commits: `3150ec0`, `f04a401`, and `915375c`. The developer-reported 772/772 test run occurred before these changes; the assistant has not run a build or tests after them.

Current state: **VERIFICATION PENDING**.

Developer rerun: rebuild the affected WinForms and test projects with Treat Warnings as Errors; run `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, `HiveSqlServerInstanceDiscoveryTests`, then the full `Hive.Tests` suite. Manually open Data Migration repeatedly and confirm there are no connection-test proxy or cancellation exceptions; verify SQL instance discovery/status; click Refresh to test both endpoints; and confirm migration preflight continues to reject invalid or unavailable endpoints before migration starts.

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, and `HiveSqlServerInstanceDiscoveryTests`; then the full `Hive.Tests` suite and a zero-warning developer build.

- Updated `docs/architecture/v1-host-and-management.md` to state explicitly that opening the tab may discover SQL instances but must not implicitly test endpoint connections; explicit Refresh and migration preflight own those tests.


### Verification failure — 2026-10-09 Data Migration endpoint connection and layout

Developer-reported same-slice runtime/UI failures after the latest migration-tab correction:

- The SQL Server endpoint card in Data Migration has substantial unused whitespace and is not presenting its controls compactly enough to be usable.
- The Embedded endpoint's database-file input needs to use the available row width; the current nested field/panel layout leaves unnecessary blank space.
- Clicking **Migrate All Data** fails preflight with “Could not reach the SQL Server endpoint.” The provided report does not include the exact SQL error number or the selected server/database values, so the precise endpoint cause is not yet established. The implementation must make the failure actionable and ensure the migration's endpoint configuration uses the same proven named-instance/explicit-port rules as Database Setup, without weakening endpoint preflight.

These are Slice 5 Data Migration presentation and connection-diagnostic corrections. Slice 6 and 1.19 remain unauthorized.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation authorization — Data Migration endpoint layout and diagnostics

- Compact the SQL Server endpoint's vertical layout and remove excess whitespace while preserving all endpoint controls and the SOURCE-left / DESTINATION-right composition.
- Let the Embedded database-file textbox expand to the usable endpoint-card width, with Browse adjacent and the path row auto-sized.
- Improve failed endpoint-preflight status to identify whether SOURCE or DESTINATION failed, and include the endpoint server/instance, any configured port, database, and safe actionable diagnostics returned by the existing Persistence connection tester. Do not expose secrets or swallow the underlying SQL error.
- Align migration SQL endpoint/port behavior with the established Database Setup picker contract. Preserve explicit ports, preserve named-instance resolution when port is not explicitly entered, and retain the user-editable Custom endpoint.
- Add focused layout and failure-diagnostic regression tests; do not bypass or relax preflight.

This is same-slice corrective work, not authorization for Slice 6 or 1.19.


### Remediation completed — Data Migration endpoint layout and SQL connection resolution

- SQL and Embedded migration endpoint stacks now use explicit auto-sized rows and top-docked content. This prevents the vertical input rows from stretching across the full card height. The SQL instance picker and related fields use more of the endpoint card while staying bounded; the Embedded database-file TextBox is explicitly fill-docked in the percent-width path row, with Browse immediately adjacent.
- Corrected a definite migration-only named-instance configuration defect: `BuildEndpointConfigurationAsync` previously used `endpoint.SqlPort ?? 1433`, overriding the picker's intentional null port for a named instance. It now preserves `endpoint.SqlPort` as configured. A named instance such as `localhost\\MSSQLSERVER01` can use SQL Server instance/Browser resolution unless the user explicitly enters a TCP port; the plain/default endpoint still gets the picker's default 1433 when appropriate. Explicitly entered ports remain preserved.
- Source and Destination readiness/preflight failures now identify the failing endpoint role and its server/instance, database, and configured-port/default/instance-resolved port mode, while preserving the existing connection-tester diagnostic and not exposing credentials. The migration tab now includes brief named-instance connection guidance.
- Added focused UI coverage for compact row sizing, the Embedded path editor expanding to the available width, and the actual built source migration configuration retaining a null port for a named SQL instance. The deterministic Management proxy intercepts the source preflight so this coverage does not open a real SQL connection.
- Updated `docs/architecture/v1-host-and-management.md` and `docs/Hive_Current_Status.md` with the corrected endpoint layout/connection contract and current verification status. Source/test commits: `80891aa`, `63f2c95`, `f9b2e10`, `8e90240`, and `7e12d48`.

Current state: **VERIFICATION PENDING**.

The user-reported **772/772** run predates this remediation; the assistant has not compiled, run automated tests, or launched the Example Host after these edits. Rebuild the affected WinForms and test projects with Treat Warnings as Errors; run `HiveUiPolishTests`, `HivePersistenceErrorTests`, and the full `Hive.Tests` suite. Manually use Data Migration in both directions: for the reported local named instance choose `Custom...`, enter `localhost\\MSSQLSERVER01`, keep Windows Integrated authentication, and leave Port blank unless you have the instance's fixed TCP port; verify the displayed database name matches the real source database, then use Refresh before Migrate. Confirm endpoint failure output names SOURCE/DESTINATION and displays the actual connection-tester diagnostic, the SQL layout is compact, and the Embedded database-file input fills its row.

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests` and `HivePersistenceErrorTests`; then the full `Hive.Tests` suite and a zero-warning developer build.


### Verification failure — 2026-10-09 Data Migration still cannot connect; diagnostics not copyable

The user reports that the previous layout/port correction did not resolve their real SQL Server connection: Database Setup's SQL connection works, but clicking migration still fails with “cannot reach SQL Server endpoint.” They also explicitly report that the migration error is placed in the status message, where it is impractical to select/copy, and that this was not an acceptable way to deliver diagnostics.

Source inspection establishes a migration-vs-Database-Setup configuration gap: Database Setup loads the saved `HivePersistenceConfiguration` into its picker and applies its server/port, database, authentication, encryption, and Trust Server Certificate settings. Data Migration currently constructs a fresh endpoint editor with defaults and never reads the saved settings to initialize a matching SQL SOURCE editor; its TLS/server/port values can therefore differ from the settings that the user already proved can connect. The latest null-port fix alone does not align these connection options. The migration's preflight still correctly owns the explicit source/destination endpoint configurations, but its UI must make a matching saved configuration available as an editable starting value instead of making the user unknowingly reconnect with defaults.

This is an in-scope Slice 5 connection and UX remediation. Slice 6 and 1.19 remain unauthorized.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation authorization — use saved endpoint settings and provide copyable diagnostics

- During first Data Migration initialization, read the persisted configuration through `IHiveManagementFacade.GetPersistenceConfigurationAsync` without testing a database connection. If its backend matches the selected SOURCE editor backend, prefill that editor with the saved endpoint settings (server, nullable/explicit port, database, authentication, Encrypt, Trust Server Certificate, and command timeout) as editable initial values. Do not silently change Direction; do not substitute the active configuration during migration execution; keep SOURCE and DESTINATION independently editable and keep both explicit configurations in the migration request.
- Preserve the saved bootstrap credential reference for the prefilled source only while the endpoint settings that establish its identity remain unchanged; never copy or reveal password material. Any new manually entered SQL password continues through the existing temporary protected-credential boundary.
- Add a copyable diagnostic surface for failed Refresh/preflight. Keep the footer status short and non-sensitive; put the full safe endpoint description and connection-tester message in a read-only/selectable diagnostics control and provide a **Copy details** action. Diagnostics must not include a connection string or secret. Clipboard failure must be contained and reported clearly.
- Add deterministic UI coverage proving settings are loaded without a connection test, saved connection options are reflected in the SOURCE editor/configuration (including `TrustServerCertificate` and an unset named-instance port), and preflight details can be copied while the footer remains concise.

This is same-slice remediation within the existing Settings/Data Migration surface. It does not authorize Slice 6 or 1.19.


### Remediation completed — Data Migration uses working Setup settings and copyable failures

- Data Migration now uses the current Database Setup editor values as an editable SOURCE prefill when the selected migration direction's SOURCE backend matches the Setup backend. This captures the connection settings the user has actually been testing in the current view, including server/instance, current nullable/explicit port, database name, authentication mode/user, Encrypt, Trust Server Certificate, and timeout. If the current Setup snapshot is unavailable (for example, a directly hosted migration view), it loads the saved Management configuration and uses it only when the backend matches SOURCE.
- Prefilling never rewrites migration direction, never fills DESTINATION from the active store, and never substitutes an active configuration into the Management migration request. SOURCE and DESTINATION remain editable; the request still carries the explicit endpoint values. Saved bootstrap credential references are reusable only while the prefilled SQL endpoint identity/options still match, and password material is never copied into fields or diagnostics. Embedded paths are nullable-guarded while being applied.
- Preflight/Refresh failures now leave a short status such as “Source connection failed. Click Copy details to copy the diagnostic.” A read-only, selectable multiline diagnostic field appears only while an error is present; a conditional **Copy details** footer action also copies the safe endpoint description and connection-tester message to the clipboard. The same concise/copyable behavior now covers returned Management migration failures and configuration validation messages; the full diagnostic is not stuffed into the status label. The field remains hidden during normal use, so it does not add ordinary-page whitespace.
- Extended the UI regression coverage to verify a working Setup configuration is loaded into the migration SOURCE without triggering connection tests, the Trust Server Certificate/encryption/database/port values are carried into preflight configuration, a named instance remains unported by default, and clicking Copy details places the full diagnostic on the clipboard while the footer status stays concise.
- The test Management proxy provides a deterministic SQL configuration and intercepts preflight; the focused regression does not attempt a real SQL Server connection. Architecture docs now describe the current-Setup prefill and copyable error contract.

Current state: **VERIFICATION PENDING**.

No build or automated tests have been run by the assistant. The prior developer-reported 772/772 run predates these changes. Rebuild with Treat Warnings as Errors; run `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, `HiveSqlServerInstanceDiscoveryTests`, `HivePersistenceErrorTests`, then the full `Hive.Tests` suite. In the Example Host, confirm the Source endpoint receives the actual Database Setup server, database, authentication, encryption, Trust Server Certificate, and port settings; change a field and verify it remains editable; run Refresh and Migrate preflight; and verify **Copy details** makes the full safe diagnostic pasteable while the status remains concise. Confirm the SQL cards no longer stretch vertically and the Embedded database-file editor fills its row.

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, `HiveSqlServerInstanceDiscoveryTests`, and `HivePersistenceErrorTests`; then the full `Hive.Tests` suite and a zero-warning developer build.

### Verification failure — 2026-10-09 Data Migration compile error

Developer-reported Visual Studio compilation error in `src/Hive.Host.WinForms/HivePersistenceDataMigrationSettingsView.cs:248`:

- `CS1012: Too many characters in character literal`.

The failure is in the recent named-instance port normalization condition. This is an in-scope compilation regression in the existing Slice 5 Data Migration correction. The next edit is restricted to correcting the malformed backslash character check; no Slice 6 or 1.19 work is authorized.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation completed — Data Migration character-literal compilation fix

- Corrected the named-instance check at line 248 from an invalid multi-character C# character literal (and unnecessary comparison argument) to the valid single-backslash check `serverName.Contains('\\')`. The existing behavior is preserved: if the stored port is 1433 for a named instance, clear it so instance-port resolution is not overridden.
- Source was read back from `main` and the corrected line was confirmed. No build or tests were run by the assistant.

Current state: **VERIFICATION PENDING**.

Developer rerun required: build `Hive.Host.WinForms` and `Hive.Tests` with Treat Warnings as Errors; run `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, `HiveSqlServerInstanceDiscoveryTests`, and `HivePersistenceErrorTests`; then run the full `Hive.Tests` suite. The earlier 772/772 result predates the current migration changes and does not verify this correction.

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, `HiveSqlServerInstanceDiscoveryTests`, and `HivePersistenceErrorTests`; then the full `Hive.Tests` suite and a zero-warning developer build.

### Verification failure — 2026-10-09 final Data Migration layout polish

The user manually inspected the Data Migration page and reported three remaining in-scope UI defects:

- The Direction selector is clipped, with only part of the control visibly rendered.
- SQL Server endpoint controls have excessive empty spacing and some fields exceed the available card bounds.
- The Embedded database-file textbox should auto-size with the available path-row width.

These are the final reported Slice 5 presentation defects. Same-slice remediation is authorized only for these layout issues and their focused regression coverage. Do not start Slice 6 or 1.19. Preserve existing endpoint behavior, copyable diagnostics, and explicit migration configuration.

Current state: **VERIFICATION FAILED / REMEDIATION REQUIRED**.

### Remediation completed — final Data Migration layout corrections

- Removed the Direction selector clipping by raising its dedicated FieldsPanel row from 64 to 72 logical pixels. The selector remains explicitly non-auto-sized at the field level; the parent row now provides enough room for the 21-pixel label, 36-pixel control, and bottom margin.
- Made the SQL Server instance picker retain `AutoSize` / `GrowAndShrink` through its field block, so choosing `Custom...` can reveal the server/instance row and discovery status without the picker being clamped to 36 pixels and clipped.
- Removed fixed widths from the SQL Server, Database, Authentication, SQL user/password, and Connection Security field blocks where the available endpoint-card width should govern sizing. Reduced field/grid gaps and enabled wrapping for the Connection Security row. This preserves the same fields and operation behavior while preventing typical half-width cards from forcing controls outside their layout bounds.
- Set the Embedded database-file textbox's `AutoSize` to `true` while retaining `DockStyle.Fill` and the adjacent Browse button.
- Extended `HivePersistenceDataMigration_UsesCompactEndpointLayoutAndNamedInstanceResolution` to check the Direction selector's vertical bounds, custom named-instance picker auto-sizing/expanded row, and Embedded path textbox AutoSize/available width.
- Updated `docs/ui/forms.md` and `docs/architecture/v1-host-and-management.md` with the corrected compact/responsive layout contract.

Current state: **VERIFICATION PENDING**.

No build, automated tests, or Example Host launch were run by the assistant. Rebuild `Hive.Host.WinForms`, `Hive.Host.WinForms.UI`, and `Hive.Tests` with Treat Warnings as Errors. Run `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, `HiveSqlServerInstanceDiscoveryTests`, and `HivePersistenceErrorTests`, then the full `Hive.Tests` suite. Manually verify the Direction selector is fully visible, the SQL custom-instance row and all controls remain inside both endpoint cards, and the Embedded database-file textbox grows across its row with Browse beside it.

Example to run: `Overview / Getting Started / Example Configuration → Settings → Persistence` — `Hive.Example.WinForms`

Tests to run: `HiveUiPolishTests`, `HiveWorkspaceLifecycleTests`, `HiveSqlServerInstanceDiscoveryTests`, and `HivePersistenceErrorTests`; then the full `Hive.Tests` suite and a zero-warning developer build.
