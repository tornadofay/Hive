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

Execution Target editors present **Management before Capabilities** so target ownership is established before the capability controls are read. Favorite ExecutionTarget preferences are managed on a second tab of the normal Providers Settings page. The Favorites tab is a simple preference CRUD surface, not another ExecutionTarget resource model: it lists only saved favorites and provides Add Favorite, Remove, and Refresh. Add Favorite opens a compact Provider → Account → Execution Target picker; the Provider and Account selectors exist there only to narrow the target choices instead of presenting a long global target list. Adding/removing a favorite persists immediately through Management. Agent and other ExecutionTarget selection dialogs/editors are outside this slice; they retain their existing behavior here and will be defined by their owning phase. The V1 Agent interaction phase is expected to use saved favorites conditionally: Auto uses the normal full eligible target set when the favorite list is empty and only favorites when one or more favorites exist; the Favorites source lists only saved favorites for explicit selection. Provider/model discovery is primarily surfaced from the normal Providers Settings page. The page offers explicit `Refresh`, which requests fresh provider/account/endpoint discovery for active configured providers and reconciles automatically managed ExecutionTargets. The UI presents configured providers and safe operational summary; it does not expose ProviderAccount or ExecutionTarget administration during normal onboarding. Advanced Provider Configuration exposes the generalized Providers / Accounts / Credentials / Execution Targets administration pages when an administrator needs multiple accounts, custom endpoints, local/self-hosted models, manual targets, or explicit capability overrides. Discovered availability, health, rate-limit metadata, and normalized capability states remain observational; configured ExecutionTarget capability overrides remain authoritative. Failed or stale discovery preserves existing durable targets and must never be interpreted as an empty model catalog.

### Planned Phase 1.16 Follow-Up — Advanced Model Information

The planned rich-discovery follow-up extends the Advanced Provider Configuration window into a tree-based administrative surface with an internal Overview page and a read-only Model Information page:

```text
Advanced Provider Configuration
├── Overview
├── Providers
├── Accounts / Credentials
├── Execution Targets
└── Model Information
```

The Advanced Overview is the landing page for the tree and explains the Provider → ProviderAccount → ExecutionTarget relationship, automatic versus manual target ownership, and the distinction between discovery evidence and durable configuration.

Model Information presents the discovered provider/model profile without becoming a configuration store. It presents normalized identity, inputs, outputs, capabilities, reasoning/thinking, limits, pricing/economics, operational state, and bounded provider-specific evidence when reported. Missing information remains explicitly not reported/Unknown.

The target capability editor is structured rather than free-form. It uses bounded Hive capability identities and Supported / Unsupported / Unknown states. The first-look layout is:

```text
Capability           Set state                 Current
Text generation      Supported ▼              Supported • override
Vision               Not configured ▼         Supported • discovered
Tool calling         Unsupported ▼            Unsupported • override
```

Every known capability uses the same state-selector column. **Current** is the effective state and briefly identifies its source as discovered, override, or not reported. For Automatic targets the selector is disabled and displays **Managed by discovery**; the Current value remains the provider/discovery-managed effective state. Manual targets can choose a state override or leave the capability Not configured so applicable discovery evidence supplies the effective state. Unknown/unreported capability evidence remains distinct from Unsupported.

This is the implemented Phase 1.16 follow-up capability presentation. See [Phase 1.16 Follow-Up — Model Information](../plan/Phase1/1.16-Follow-Up.md) for the detailed scope and verification contract.

The Persistence Server / instance field is a normal free-form text box. It accepts local servers, named instances, remote hosts, IP addresses, and online SQL Server targets. Hive currently has no authoritative server-discovery/catalog contract, so the UI does not attempt to enumerate installed SQL Server instances. The Database field is read-only and assigned automatically to Hive's package database name. Save and Test are non-destructive. The `Initialize Hive` action is the explicit lifecycle operation that may create the configured database when allowed and applies Hive schema migrations; it must not be used as an implicit side effect of Save, Test, or normal Settings-page navigation.

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
