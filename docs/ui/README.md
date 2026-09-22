# Hive WinForms UI and Example Development Guide

This directory is the practical consumer/development guide for Hive's WinForms UI foundation and the permanent Hive.Example.WinForms developer/example host.

## Authority

- AGENTS.md defines the repository workflow and engineering rules.
- docs/architecture.md defines the intended UI architecture, ownership, and boundaries.
- docs/ui/ is the practical usage guide for the current UI contracts and Example Host conventions.
- Source code remains authoritative for the implemented API surface and actual behavior.
- When these guides and source disagree, follow the source evidence and correct the guide. Do not invent a new API to make the documentation appear consistent.
- The guides document consumer-facing contracts and important behavior; they are not a replacement for XML/API documentation or source inspection when a question is genuinely outside the documented contract.

## Read this before UI or Example work

For changes involving Hive.Host.WinForms.UI, Hive.Host.WinForms, or Hive.Example.WinForms, read:

1. AGENTS.md
2. the relevant sections of docs/architecture.md
3. this directory's relevant guide
4. source only where the documented contract does not answer the implementation question

Use controls.md for control selection and public behavior.
Use forms.md for form composition, theming, layout, lifecycle, and responsiveness.
Use examples.md for Example Host discovery, scenario structure, shared services, and verification.

Do not start by reconstructing UI behavior from control internals when the consumer contract is already documented.

## Core consumer rule

Use the smallest existing Hive UI contract that fully satisfies the screen.

Native WinForms controls remain valid when their native behavior plus Hive's shared theme application are sufficient. A Hive-prefixed control should exist because Hive needs a real consumer-facing behavior, styling contract, or capability, not merely because a native control has a different name.

Feature/domain behavior stays in the owning application or Management layer. Shared UI primitives should not acquire persistence, authorization, provider, or domain rules merely because a form needs them.

## Theme application lifecycle

Theme application is recursive but explicit.

HiveForm applies its theme during base-form construction. A derived form normally creates its feature controls afterward, so newly added controls are not automatically themed merely because the form derives from HiveForm.

After composing a form body or dynamically creating a UserControl tree, apply the shared theme to the newly composed root:

~~~csharp
BodyPanel.Controls.Add(page);
ThemeManager.Apply(BodyPanel);
~~~

For a view created after the form's initial construction:

~~~csharp
ThemeManager.Apply(view);
~~~

Use the owning form's shared IHiveThemeManager. Do not create a second theme manager inside a feature view just to color its controls.

Theme changes after that are propagated by the normal Hive theme manager path.

## Standard compositions

Typical list/configuration page:

~~~text
HiveForm
  └── BodyPanel
       └── HiveListPageLayout
            ├── HeaderPanel
            ├── ActionBarPanel
            └── ContentPanel
                 └── feature content
~~~

Typical editor page:

~~~text
HiveForm
  └── BodyPanel
       └── HiveEditorLayout
            ├── FieldsPanel
            └── FooterPanel
~~~

Generic CRUD page:

~~~text
HiveForm
  └── BodyPanel
       └── HiveCrudPage<TItem>
~~~

For a dynamic Example Host view:

~~~text
HiveExampleHostForm
  └── view host
       └── one active UserControl
            └── Example scenario
~~~

The reusable UI owns presentation and interaction orchestration that belongs to that UI primitive. The consuming feature supplies domain data, validation, authorization, persistence, and specialized behavior.

## Current consumer-facing UI vocabulary

The practical guides cover:

- HiveForm
- HiveButton
- HiveMessageBox / HiveMessageOptions
- HiveListPageLayout
- HiveCrudPage<TItem> / HiveCrudColumn<TItem>
- HiveEditorLayout
- HivePaginationBar
- HiveNavigationTree
- HiveListView
- HiveExampleTestSurface
- HiveExampleOutputView / IHiveExampleOutput
- IHiveThemeManager / HiveThemeManager
- the internal Example Host contract and its discovery conventions

Some implementation controls are intentionally not consumer contracts, such as HiveWindowHeader and HiveBorderPanel when used only internally by the foundation.

## Example Host rule

Hive.Example.WinForms is a permanent developer-facing public-API example and manual-verification host.

Every new meaningful externally usable capability must have a matching Example scenario in the same implementation run. The Example must exercise the real public Hive API, not a test-only substitute or fake success path.

Every new meaningful capability also requires the corresponding Hive.Tests coverage required by the active slice. The Example and automated tests serve different purposes and neither replaces the other.

## UI quality expectations

For user-facing UI work, preserve:

- responsive resize behavior;
- Light / Dark / System theme behavior;
- selection and focus state;
- hover / pressed / disabled states;
- deterministic resource ownership;
- UI-thread affinity;
- accessible names, descriptions, and keyboard operation where applicable;
- loading, empty, no-match, cancellation, and failure states where the feature needs them.

Static inspection does not establish visual correctness. When the active slice requires manual UI verification, the developer must actually run and inspect the application.

## Boundary reminder

Do not put SQL/database access, provider transport, authorization policy, or host business rules into Hive.Host.WinForms.UI merely because a UI control needs to call them.

UI consumes the appropriate public Management/application contract or, where explicitly allowed by the architecture, another public platform contract. It should not bypass ownership boundaries to make a screen easier to implement.
