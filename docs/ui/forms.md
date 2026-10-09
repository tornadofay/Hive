# Hive Forms — Agent Reference

## List page

```text
HiveForm
└─ BodyPanel
   └─ HiveListPageLayout
      ├─ HeaderPanel
      ├─ ActionBarPanel
      └─ ContentPanel
```

Use `HiveCrudPage<TItem>` inside ContentPanel for generic CRUD.

## Editor page

```text
HiveForm
└─ BodyPanel
   └─ HiveEditorLayout
      ├─ FieldsPanel
      └─ FooterPanel
```

Use `AddField(...)` and `AddActionButton(...)`.

## Theme

```csharp
IHiveThemeManager
```

Members: `Mode`, `Theme`, `ThemeChanged`, `SetMode(...)`, `Apply(Control)`.

Modes: `Light`, `Dark`, `System`.

After constructing form content:
```csharp
ThemeManager.Apply(BodyPanel);
```

Before showing a dynamically created view:
```csharp
ThemeManager.Apply(view);
host.Controls.Add(view);
```

## Layout

Use normal WinForms:
`Dock`, `Anchor`, `TableLayoutPanel`, `FlowLayoutPanel`.

## Ownership

The owner of a dynamically replaced child disposes the previous child.

UI controls should consume application/Management APIs; do not put SQL or provider transport into reusable UI controls.

## Settings form

`HiveSettingsForm` is the application-window shell for the **global Hive package configuration center**. It is the single Settings entry point for durable Hive-owned package configuration. The Settings view opens on a dedicated **Overview** page by default; the Overview is informational and does not initialize or require the configured Hive database. Individual configuration domains appear as selectable pages inside the same Settings center as their contracts become available:

```csharp
var form = new HiveSettingsForm(
    management,
    accessContext,
    themeManager);

form.ShowDialog(owner);
```

`HiveSettingsForm` owns window/header composition only. `HiveSettingsView` owns global Settings navigation and page composition. The normal Providers page is the user-facing CRUD surface for configured Provider resources. ProviderAccount and ExecutionTarget remain separate durable resource domains, but their administrative CRUD pages live under the generalized Advanced Provider Configuration entry point rather than as child leaves of the normal Providers page. AgentDefinition remains its own Settings domain. Persistence is different: it is one global configuration document, so its leaf uses a dedicated editor rather than CRUD.

Settings pages call the appropriate public Management/application boundaries. Settings pages do not construct or own the host Hive service graph; host composition/lifetime remains outside the UI. Future durable Hive configuration domains extend this same Settings center rather than creating parallel top-level settings forms.

Execution Target editors present **Management before Capabilities** so target ownership is established before the capability controls are read. Favorite ExecutionTarget preferences are managed on a second tab of the normal Providers Settings page. The Favorites tab is a simple preference CRUD surface, not another ExecutionTarget resource model: it lists only saved favorites and provides Add Favorite, Remove, and Refresh. Add Favorite opens a compact Provider → Account → Execution Target picker; the Provider and Account selectors exist there only to narrow the target choices instead of presenting a long global target list. Adding/removing a favorite persists immediately through Management. Agent and other ExecutionTarget selection dialogs/editors are outside this slice; they retain their existing behavior here and will be defined by their owning phase. The V1 Agent interaction phase is expected to use saved favorites conditionally: Auto uses the normal full eligible target set when the favorite list is empty and only favorites when one or more favorites exist; the Favorites source lists only saved favorites for explicit selection. Provider onboarding and general provider discovery are surfaced from the normal Providers Settings page. Rich model inspection is surfaced through Advanced Provider Configuration → Model Information. The normal Providers page offers explicit `Refresh`, which requests fresh provider/account/endpoint discovery for active configured providers and reconciles automatically managed ExecutionTargets. The UI presents configured providers and safe operational summary; it does not expose ProviderAccount or ExecutionTarget administration during normal onboarding. Advanced Provider Configuration exposes the generalized Providers / Accounts / Credentials / Execution Targets administration pages when an administrator needs multiple accounts, custom endpoints, local/self-hosted models, manual targets, or explicit capability overrides. Discovered availability, health, rate-limit metadata, and normalized capability states remain observational; configured ExecutionTarget capability overrides remain authoritative. Failed or stale discovery preserves existing durable targets and must never be interpreted as an empty model catalog.

### Advanced Provider Configuration — Model Information

The Advanced Provider Configuration window is a focused administrative surface with resource-management pages and one read-only discovery-information page:

```text
Advanced Provider Configuration
├── Providers
├── Accounts / Credentials
├── Execution Targets
└── Model Information
```

There is no separate Overview tab in this window. The surrounding normal Providers Settings surface remains the entry point for simple provider onboarding.

Model Information presents the discovered provider/model profile without becoming a configuration store. Provider and Account / Credential are compact HiveComboBox selectors in the same borderless responsive filter surface as the inspection controls. The page does not expose a discovery-endpoint selector; discovery uses the first active ExecutionTarget endpoint for the selected account, ordered deterministically by endpoint URI, with the built-in provider default endpoint as a fallback when no active target endpoint exists. Endpoint administration remains in the Execution Targets page. The discovered model catalog uses the shared Hive CRUD / HiveListView presentation with one confirmed Add to Favorites action, Text / Vision / Tools / Structured / Reasoning / Thinking capability columns whose state cells render as `✓` / `✕` / `—`, minimum/maximum comparable token-price sliders using $0.01 increments with a data-sized practical maximum, and a capability/state filter. A maximum price of $0 is the free-model view: it includes models with explicit free-pricing evidence or explicitly zero comparable input/output token pricing; missing pricing is not treated as free. The filter compares the highest USD input/output token rate after provider-unit normalization. The selected model is shown in a lightweight reusable detail text surface with a compact fixed right-side width rather than a recreated card tree. Capability columns use compact `✓` / `✕` / `—` symbols only in the list; detail text uses `Supported`, `Unsupported`, or `Unknown / unreported`. The page header comes before the selectors and inspection filters. Identity, inputs, outputs, capabilities, reasoning/thinking, limits, pricing/economics, operational state, and bounded provider-specific evidence remain separately readable, while missing information is shown as `—`.

The target capability editor is structured rather than free-form. It uses bounded Hive capability identities and Supported / Unsupported / Unknown states. The first-look layout is:

```text
Capability           Set state                 Current
Text generation      Supported ▼              Supported • override
Vision               Not configured ▼         Supported • discovered
Tool calling         Unsupported ▼            Unsupported • override
```

Every known capability uses the same state-selector column. **Current** is the effective state and briefly identifies its source as discovered, override, or not reported. For Automatic targets the selector is disabled and displays **Managed by discovery**; the Current value remains the provider/discovery-managed effective state. Manual targets can choose a state override or leave the capability Not configured so applicable discovery evidence supplies the effective state. Unknown/unreported capability evidence remains distinct from Unsupported.

This is the implemented Phase 1.16 follow-up capability presentation. See [Phase 1.16 Follow-Up — Model Information](../plan/Phase1/1.16-Follow-Up.md) for the detailed scope and verification contract.

The Persistence page is one global `HivePersistenceConfiguration` editor using the existing HiveTabControl. It uses a structured section hierarchy rather than the generic repeated-field editor: backend selection first, then one backend-specific connection section, with consistent label-above-input field blocks and grouped related settings:

`[ Database Setup ] [ Data Migration ]`

**Database Setup** contains the backend selector:

`Backend: [Embedded | SQL Server]`

For **Embedded**, the page shows the configured local Hive storage location plus backend status and explicit initialization/readiness actions. It does not require an externally installed database server and does not expose a SQL connection string.

For **SQL Server**, the Server / port field uses a bounded Hive SQL Server instance picker rather than an unnecessarily full-width editor. It combines currently advertised SQL Server instances from the SQL client enumerator with installed local SQL Server instance names from the Windows SQL Server instance registry inventory, normalizes installed local instances to `localhost` / `localhost\\<Instance>`, de-duplicates them, automatically refreshes when SQL Server is selected, keeps `Custom...` as the final choice, and reveals a free-form server/instance textbox directly under the instance selector for custom targets. Discovery remains best-effort for network visibility, so `Custom...` is always retained. The default TCP port presented by the UI is 1433 for a bare/default SQL Server endpoint or custom server. Named SQL Server instances leave the port unset by default so instance resolution can determine the effective port. The Database field remains read-only and is always populated from Hive's package database name, using the generated name as the UI fallback when a loaded configuration is blank.

Save and readiness/connection Test remain non-destructive. The explicit `Initialize Hive` lifecycle action may create or initialize the selected persistence backend when allowed and apply its schema migrations. Initialization must never occur implicitly from Save, Test, or ordinary Settings-page navigation.

**Data Migration** is the complete bidirectional migration surface. The page is a full-width two-column workspace rather than a free-form editor:

`SQL Server → Embedded`
`Embedded → SQL Server`

The compact, content-sized top row keeps **Direction** and the fixed **Scope: All Hive Data** together without stretching or clipping the Direction selector. Below it, the UI always shows **SOURCE** on the left and **DESTINATION** on the right. Both endpoint cards are real editable migration endpoints. The selected direction swaps which backend editor appears in each endpoint: SQL Server → Embedded shows an editable SQL Server SOURCE and Embedded DESTINATION; Embedded → SQL Server shows an editable Embedded SOURCE and SQL Server DESTINATION. On first opening the tab, when the current Database Setup backend matches the selected SOURCE role, the UI pre-fills that endpoint from the current Database Setup editor values so a connection that just worked there keeps the same server/instance, port, database, authentication, encryption, and certificate-trust options. If the current editor snapshot is unavailable, it may prefill from the saved Management configuration. This is only an editable starting value: it must not change Direction, fill DESTINATION from the active store, or replace either explicit endpoint during migration execution. SQL Server endpoint configuration uses the discovered-instance/custom picker, authentication selector, Windows-integrated versus SQL-password credential presentation, Server / Port, SQL credentials, Connection Security, and Command Timeout controls. SQL input fields use the available half-card width rather than forcing fixed widths that can overflow; the instance picker keeps its auto-sizing so the Custom server row and discovery status remain visible. Related row spacing stays compact, and Connection Security can wrap on narrower cards. Embedded endpoint configuration uses a single-line database-file path textbox with AutoSize enabled, filling the available path-row width with Browse immediately beside it. Source endpoints never initialize or create their source database; destination creation remains an explicit migration option. Opening Data Migration may discover SQL Server instances but does not test database connections implicitly; the explicit Refresh action checks readiness and Migrate repeats endpoint preflight. Endpoint/preflight, invalid-configuration, and migration failures show a themed **HiveMessageBox** dialog instead of putting error details in a textbox below the workspace. The footer status remains concise. The dialog contains a short user-facing message plus expanded diagnostic details with a stable error code and, for SQL Server exceptions, the endpoint role, configured server/database, native SQL error number/state/class, and bounded next-step guidance. HiveMessageBox supplies the copy-details action. Raw SQL exception text, connection strings, and credentials are omitted from curated migration diagnostics. No inline diagnostic textbox or Copy details footer action is used. Status messages and migration actions live together in the footer bar. It transfers **All Hive Data** as one migration scope. In 1.18A, migration is a full logical transfer of every Hive-owned durable record represented by the current persistence contracts, not just Providers/WorkItems. Both endpoint configurations use the authoritative `HivePersistenceConfiguration` contract, but remain transient migration inputs until migration completes.

Before migration, the active host graph's Management operation gate rejects new ordinary Management operations and drains already admitted calls, then holds an exclusive quiescence lease while migration runs. Calls attempted during migration receive a retryable conflict rather than racing persistence access. For an independently supplied source endpoint that is not the active Hive backend, this gate does not claim to stop external writers; the migration instead relies on source re-verification/fingerprint consistency to reject concurrent source changes.

Migration completion requires verification of the full logical dataset, including stable identities, relationships, versions/lifecycle state, event/snapshot/outbox consistency, work state, and protected Secret Store records. The source remains unchanged, and successful migration does not automatically activate the destination.

The Persistence page consumes Management operations only. It must not construct SQLite/SQL Server connections, expose raw credentials, or implement backend-specific persistence rules.

## Structured extraction and batch input UI

Phase 1.17 uses explicit input selection modes:

```text
[ Single File ]   [ Folder ]
```

Single File selects one input item. Folder selects an input scope that becomes one bounded batch. A folder may contain multiple Excel files, multiple images, and unsupported files together. Folder enumeration is non-recursive by default; an explicit Include Subfolders option may enable bounded recursive enumeration.

The UI should present processing at the batch level while retaining per-file/per-item identity and status. Unsupported or failed items remain visible without hiding successful items.

Before processing begins, the selected batch is subject to the processing authorization checkpoint when policy requires it. One batch-level authorization can cover many image model calls; there is no inherent one-authorization-per-image requirement.

After processing, the UI should present a second candidate/mapping checkpoint. Spreadsheet mappings and image-extracted candidates are shown for review; the user can correct mappings and candidate values, exclude failed or uncertain items, inspect failed items, open original sources where supported, and then authorize the accepted candidate set for downstream use.

This second authorization does not perform the host business write. Consequential host mutation remains the later business-operation boundary.

For spreadsheet extraction, the UI should show the detected workbook/worksheet/header/sample context and the proposed column-to-target-field mapping before downstream use. The mapping must be editable. After the mapping is accepted, it is reused deterministically rather than asking the LLM to remap every row.

For image extraction, the UI should present batch progress and independent per-image outcomes. A failed image should show its file name and a safe failure reason; selecting it should open the original image where the host surface permits.

Candidate review should allow inspection/correction of extracted values before downstream business-operation work. Mapping/candidate review is distinct from authorization to perform a consequential host operation. The generic Approve / Reject intervention UI belongs to the later governance boundary.
