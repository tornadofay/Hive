# Phase 0 — Foundations

This document contains the detailed ordered plan for this phase. It does not authorize implementation; authorization remains in `docs/Hive_Active_Work.md`.

## 0.1 — Solution & project scaffolding
Objective: create the complete initial solution structure:
- `Hive.Core`;
- `Hive.Agents`;
- `Hive.Persistence`;
- `Hive.Coordination`;
- `Hive.Tools`;
- `Hive.Providers.OpenAICompatible`;
- `Hive.Management`;
- `Hive.Host.WinForms`;
- `Hive.Host.WinForms.UI`;
- `Hive.Example.WinForms`;
- `Hive.Tests`.

Establish the dependency direction from the beginning. `Hive.Host.WinForms.UI` owns the Hive WinForms presentation implementation, `Hive.Host.WinForms` consumes the UI foundation, and `Hive.Example.WinForms` consumes public platform contracts plus the WinForms/UI layers. No core/platform project may depend on the Example host.

Verify: solution builds; forbidden references are absent; the Hive UI implementation remains behind `Hive.Host.WinForms.UI`; the Example host is isolated from test-framework internals.

## 0.2 — Common infrastructure
Objective: IDs, immutable value objects, typed errors/results, `IClock`, event envelope with event type and payload schema version, correlation/causation IDs, event upcasting compatibility boundary, and one JSON serialization stack.
Verify: normal/invalid/boundary unit tests, JSON round-trip, older-event payload upcast tests, and rejection of unsupported event schema versions.

## 0.3 — Identity, WorkItem & Resource foundation
Objective: Deployment/Tenant/Principal/User/Session/Workspace/Agent/Hive/Runtime/Execution/WorkItem identity, Resource envelope, ownership, scope, provenance, lifecycle/version metadata.
V1 work-unit rule: a WorkItem is the durable unit of user-visible work and represents one logical business operation when a business operation is required. A single input submission may produce one or multiple independent WorkItems. A submission/batch is an operational grouping, not a replacement for WorkItem identity, lifecycle, provenance, authorization, or any applicable operation receipt or Review state.
Verify: scope matrix, missing-identity fail-closed cases, immutable identity snapshots, WorkItem lifecycle and provenance isolation.

## 0.4 — Persistence bootstrap
Objective: Hive-owned SQL Server database, LocalDB development setup, DbUp migrations, schema-version tracking, indexes.
Verify: clean install, repeat migration, failed migration, incompatible future schema.

## 0.5 — Test harness
Objective: xUnit scaffolding, fake provider infrastructure, fake clock, test-database strategy, deterministic event-test conventions.
Verify: baseline tests pass and automated tests make no real vendor/network calls.

## 0.6 — WinForms UI/UX Foundation
Objective: establish the shared WinForms visual foundation used by Hive.Host.WinForms and Hive.Example.WinForms. Hive owns the theme contract, semantic design tokens, and the small set of Hive-specific controls required by consumers. The current implementation uses native WinForms controls and custom System.Drawing rendering; application forms consume Hive-owned contracts.

The initial foundation includes:
- Light / Dark / System theme modes;
- Hive-owned palette, typography, spacing, and common visual-state tokens;
- HiveForm as the reusable application-window shell;
- HiveButton with Primary / Secondary / Navigation styles;
- HiveMessageBox with semantic message types and optional technical details;
- Hive-specific controls only where Hive needs behavior or styling beyond ordinary WinForms controls;
- native WinForms controls and custom `System.Drawing` rendering owned by `Hive.Host.WinForms.UI`;
- reusable data-page composition primitives: a header/action/content list layout and an optional pagination bar;
- `HiveCrudPage<TItem>` for generic Add/Edit/Delete/Refresh UI orchestration over consumer-supplied callbacks, including compact toolbar layout and an integrated `HivePaginationBar` footer;
- the CRUD presentation standard: clear title/description hierarchy, optional search, primary Add action separated from contextual Edit/Delete actions, predictable loading/empty/no-match states, keyboard-friendly list interaction, and compact record-count/status feedback;
- reusable editor-layout composition for repeated labeled-field and action-footer patterns; `HiveEditorLayout` supplies presentation only and does not own field semantics or validation, while standardizing field rhythm and action-footer alignment;
- domain pages keep their own schemas, columns, filters, validation, authorization, specialized editors, and persistence behavior.

Do not create a complete replacement control toolkit or wrap every WinForms control merely to rename it. Keep rendering implementation details inside `Hive.Host.WinForms.UI` so consuming forms depend only on Hive-owned UI contracts. A different renderer may be introduced later only through an explicit architectural decision.

Verify: a representative sample form renders in Light and Dark modes, shared styling is consistent, consuming forms use only Hive-owned UI contracts, and the UI implementation can evolve without changing consumer-facing Hive UI contracts.

## 0.7 — First-Class Example Host Shell
Objective: make `Hive.Example.WinForms` a permanent developer-facing application rather than a temporary demonstration.

The shell uses scalable navigation:
- Category;
- Subcategory;
- Example.

Use a left-side tree/list navigation surface and a right-side replaceable example `UserControl`. Do not use nested Category → Subcategory → Example TabPages as the primary navigation model.

Define a small discovery contract such as `IHiveExample` with category, subcategory, title, and a `CreateView(IServiceProvider services)` factory. Discover only designated example assemblies so adding an example requires implementing the contract without manual shell wiring.

Examples are grouped by feature area and grow with the platform. The shell itself uses only Hive-owned UI contracts.

Verify: adding one new example implementation makes it appear in navigation without additional shell wiring; selecting an example replaces the content view correctly; navigation remains usable with many examples; Light/Dark/System theme changes preserve navigation selection and scroll position; representative CRUD and dialog surfaces remain readable and consistent at supported compact and normal window sizes; no avoidable UI warnings or resource-lifetime regressions are introduced.

---
