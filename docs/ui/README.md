
# Hive WinForms UI and Example Development Guide

This directory is the practical consumer guide for Hive's WinForms UI foundation and the permanent Hive.Example.WinForms developer/example host.

## Authority

- `AGENTS.md` defines the agent workflow and repository-wide engineering rules.
- `docs/architecture.md` defines the intended UI architecture and boundaries.
- `docs/ui/` is the practical consumer/development guide for existing UI contracts and the Example Host workflow.
- Source code is authoritative for the actual implemented API surface. When this guide and source disagree, follow the source evidence, then correct the documentation.
- This guide is not a second UI architecture document and must not invent contracts that do not exist in code.

## Read this before UI work

For changes involving Hive.Host.WinForms.UI, Hive.Host.WinForms, or Hive.Example.WinForms, read:

1. AGENTS.md
2. relevant sections of docs/architecture.md
3. this UI guide
4. relevant existing source only when the documented contract does not answer the implementation question

Do not start by rebuilding Hive's UI rules from control internals.

## Documents

- controls.md — consumer-facing Hive controls and when to use them.
- forms.md — HiveForm, page composition, layout, theming, lifecycle, and responsiveness.
- examples.md — how to implement, organize, discover, and manually verify a new Hive.Example.WinForms example.

## Core rule

Use the smallest existing Hive UI contract that fully satisfies the screen.

Do not introduce a new Hive-prefixed control merely to rename a native WinForms control. Use native WinForms controls when their behavior and the Hive theme already satisfy the requirement. Create a Hive-owned control only when Hive needs a real consumer-facing behavior, styling contract, or capability.

## Standard form composition

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

Feature/domain behavior remains outside reusable UI primitives. Reusable controls own only presentation/orchestration concerns that belong to them. The consuming feature supplies domain data, validation, authorization, persistence, and specialized behavior.

## Example Host rule

Hive.Example.WinForms is a permanent public-API example and developer-verification host. Examples are not throwaway demonstrations.

Every new meaningful externally usable capability must have a corresponding Example scenario in the same implementation run. The Example must use public Hive contracts and be understandable enough for a developer to run and inspect.
