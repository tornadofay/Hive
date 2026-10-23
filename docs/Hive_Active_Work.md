# Hive — Active Work

Status: VERIFICATION PENDING — Phase 1.20 / Slice 1 — Workspace Foundation (remediation implemented, awaiting developer verification)

## Authorization
Explicitly authorized by `Hive: Start Phase 1.20` on 2026-10-10. The owning plan is [Phase 1.20 — V1 Workspace Foundation](plan/Phase1/1.20.md). On 2026-10-23 the developer reported verification results and explicitly directed that Slice 1 be treated as incomplete and rescoped as below. The window and module boundary is in [architecture section 5.2](architecture/v1-host-and-management.md).

## Recorded verification outcome (developer-reported, 2026-10-23)
- Automated: `Hive.Tests` 804 passed, 0 failed, 0 skipped. The developer always builds the whole solution with Treat Warnings as Errors.
- Example `Workspace / Direct LLM / Direct LLM Conversation`: chat worked.
- **Failure:** `Workspace / WorkItem Operations / V1 Workspace & WorkItem Operations` threw `InvalidOperationException: Hive Example service 'ResourceAccessContext' is not available.`
- **Unmet:** the Workspace was not an independent window opened like Settings.
- **Unmet:** the Direct LLM ExecutionTarget picker ignored saved favorites.

## Remediation implemented (source changes not compiled or run here)
- `HiveWorkspaceContext`, `IHiveWorkspaceModule`, shell `HiveWorkspaceView`, modeless `HiveWorkspaceForm`, public single-instance `HiveWorkspaceLauncher` under `src/Hive.Host.WinForms/Workspace/`.
- Direct LLM and Work Items are modules. Direct LLM uses `DirectLlmTargetCatalog`: no favorites means all eligible targets; favorites set means only usable favorites, with an empty list and guidance when none are usable; a favorites load failure is reported and never broadens the list; a store that is not configured counts as no favorites.
- Modules load on first activation and reload when the host replaces its Management graph.
- Example Host: `HiveExampleServices` exposes `ResourceAccessContext`; new `Overview / Getting Started / Workspace` page opens the real window; the two inline Workspace example pages are removed; the host notifies the Workspace after Settings recomposition.
- Tests: `HiveWorkspaceLifecycleTests` rewritten for the shell and modules; new `HiveWorkspaceLauncherTests` and `DirectLlmTargetCatalogTests`.
- The internals of the two moved views are not yet split into presenters and panes. That belongs with the Slice 2 chat redesign.

## Exclusions
Chat visual redesign (Slice 2), runtime context and WorkItem/job visibility (Slice 3), restart/reload handling and phase closure (Slice 4), Agent mode (Phase 1.21), streaming, any new Management contract.

## Verification boundary
This status remains **VERIFICATION PENDING** until developer results are reported. Rebuild the whole solution, run the tests below, then the full suite, and exercise the Example scenario. No new slice may start until these pass and Slice 1 is explicitly closed.

## Required handoff
- Example to run: **Overview / Getting Started / Workspace — Hive.Example.WinForms**. Open the window; confirm Direct LLM loads on its own and Work Items works; add favorites in Settings (Providers, Favorite Execution Targets), refresh, and confirm only favorites are listed; remove all favorites and confirm all eligible targets return; change Settings so the host recomposes while the Workspace stays open and confirm it keeps working; open the Workspace twice and confirm one window.
- Tests to run: **HiveWorkspaceLifecycleTests.cs**, **HiveWorkspaceLauncherTests.cs**, **DirectLlmTargetCatalogTests.cs**, **DirectLlmConversationTests.cs**, then the broader **Hive.Tests** suite.
