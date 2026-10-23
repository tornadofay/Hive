# Hive — Active Work

Status: VERIFICATION FAILED / REMEDIATION REQUIRED — Phase 1.20 / Slice 1 — Workspace Foundation (reopened)

## Authorization
Explicitly authorized by `Hive: Start Phase 1.20` on 2026-10-10. The owning plan is [Phase 1.20 — V1 Workspace Foundation](plan/Phase1/1.20.md). On 2026-10-23 the developer reported verification results and explicitly directed that Slice 1 be treated as incomplete and rescoped as below.

## Recorded verification outcome (developer-reported, 2026-10-23)
- Automated: `Hive.Tests` 804 passed, 0 failed, 0 skipped. The developer always builds the whole solution with Treat Warnings as Errors.
- Example `Workspace / Direct LLM / Direct LLM Conversation`: chat works.
- **Failure:** `Workspace / WorkItem Operations / V1 Workspace & WorkItem Operations` threw `InvalidOperationException: Hive Example service 'ResourceAccessContext' is not available.` Cause: `HiveExampleServices` did not expose `ResourceAccessContext`.
- **Unmet requirement:** the Workspace is not an independent window opened from the Example Host Getting Started pages like Settings. It exists only as a `UserControl` hosted inside examples. The plan requires that the Workspace opens as the normal user-facing interaction surface.
- **Unmet requirement:** the Direct LLM ExecutionTarget picker lists every eligible target. It must use the saved favorite targets when any exist, and all eligible targets only when none are set.

## Rescoped Slice 1 — Workspace foundation
- Independent modeless Workspace window (`HiveWorkspaceForm`), single instance per host, opened through a public `HiveWorkspaceLauncher`; `HiveWorkspaceView` becomes the embeddable shell.
- Module contract (`IHiveWorkspaceModule`) so later phases add Workspace features without editing the shell. Direct LLM and Work Items become modules.
- `HiveWorkspaceContext` resolves the current Management facade on every call and raises a change notification after host recomposition, so a long-lived window never uses a retired graph.
- Example Host: new `Overview / Getting Started / Workspace` page that opens the real window (like Example Configuration opens Settings); the two inline Workspace example pages are replaced; `HiveExampleServices` exposes `ResourceAccessContext`.
- Direct LLM ExecutionTarget picker applies saved favorites: no favorites means all eligible targets; favorites set but none usable means an empty list with guidance (no fallback); a favorites load failure is reported and never silently broadens the list.
- Existing Direct LLM and Work Items behavior is otherwise preserved. Focused tests and the Example scenario are updated.

## Exclusions
Chat visual redesign (Slice 2), runtime context and WorkItem/job visibility (Slice 3), restart/reload handling and phase closure (Slice 4), Agent mode (Phase 1.21), streaming, and any new Management contract.

## Work order
1. Workspace context, module contract, shell, window, launcher.
2. Direct LLM module with the favorite-filtered target catalog.
3. Work Items module.
4. Example Host: services fix, Getting Started page, host wiring.
5. Tests and documentation.
6. Return to `VERIFICATION PENDING` with exact rerun targets.

## Planned handoff after remediation
- Example to run: **Overview / Getting Started / Workspace — Hive.Example.WinForms** (open the window; check Direct LLM with and without favorites set in Settings, and Work Items).
- Tests to run: **HiveWorkspaceLifecycleTests.cs**, **HiveWorkspaceLauncherTests.cs**, **DirectLlmTargetCatalogTests.cs**, **DirectLlmConversationTests.cs**, then the broader **Hive.Tests** suite.
