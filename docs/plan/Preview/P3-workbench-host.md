# Preview — Standalone Workbench Host [Target]

Objective: deliver the standalone Hive Workbench WinForms host consuming existing Workspace shell and neutral Management contracts.

Scope:
- onboarding: provider key (DPAPI) or local OpenAI-compatible endpoint; Embedded persistence; project files; reproducible samples; quickstart; packaging;
- Workspace UX redesign: conversation + Hive activity + projects + tools, reusing Slice 1 shell;
- module registration uses the public `IHiveWorkspaceModule` seam (promotion from internal is a prerequisite implementation task; built-in module implementations stay internal);
- project-directory scoping at the Tool boundary (not a UI toggle);
- no fork of Workspace, no separate coordination engine, no bypass of Hive.Management;
- build/test execution deferred to Preview 2 behind isolation toggle.

Verify:
- single-instance behavior is defined per the required scope: repeated Show() on one launcher instance reuses the existing window (current guarantee); if launching the standalone executable twice must yield one window, an application-level single-instance + activation mechanism is required and must be specified before implementation; otherwise the acceptance test is narrowed to the per-launcher guarantee;
- modules load on first activation and reload on Management recomposition; third-party modules participate in refresh via a public lifecycle contract (or by subscribing to the public context event and refreshing safely);
- onboarding completes without SQL Server;
- project files scoped to permitted directories;
- Example scenario runs end-to-end with fake provider.
