# Preview — Standalone Workbench Host [Target]

Objective: deliver the standalone Hive Workbench WinForms host consuming existing Workspace shell and neutral Management contracts.

Scope:
- onboarding: provider key (DPAPI) or local OpenAI-compatible endpoint; Embedded persistence; project files; reproducible samples; quickstart; packaging;
- Workspace UX redesign: conversation + Hive activity + projects + tools, reusing Slice 1 shell;
- project-directory scoping at the Tool boundary (not a UI toggle);
- no fork of Workspace, no separate coordination engine, no bypass of Hive.Management;
- build/test execution deferred to Preview 2 behind isolation toggle.

Verify:
- two launches produce one window;
- modules load on first activation and reload on Management recomposition;
- onboarding completes without SQL Server;
- project files scoped to permitted directories;
- Example scenario runs end-to-end with fake provider.
"