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
- single-instance behavior: target is application-wide single instance for the standalone executable (one window per user session), implemented via a named mutex plus activation of the existing window; per-launcher reuse (repeated Show() reusing the existing window) is the minimum guaranteed fallback. The concrete mechanism must be specified before implementation; if application-wide single-instance cannot be delivered, narrow the acceptance test to the per-launcher guarantee and record the decision.
- modules load on first activation and reload on Management recomposition; third-party modules participate in refresh via a public lifecycle contract (or by subscribing to the public context event and refreshing safely);
- onboarding completes without SQL Server;
- project files scoped to permitted directories;
- Example scenario runs end-to-end with fake provider.
