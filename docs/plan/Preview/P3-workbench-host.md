# Preview — Standalone Workbench Host [Target]

Objective: deliver the standalone Hive Workbench WinForms host consuming existing Workspace shell and neutral Management contracts.

Scope:
- onboarding: provider key (DPAPI) or local OpenAI-compatible endpoint; Embedded persistence; project files; reproducible samples; quickstart; packaging;
- Workspace UX redesign: conversation + Hive activity + projects + tools, reusing Slice 1 shell;
- module registration uses the public `IHiveWorkspaceModule` seam (promotion from internal is a prerequisite implementation task; built-in module implementations stay internal);
- project-directory scoping at the Tool boundary (not a UI toggle);
- no fork of Workspace, no separate coordination engine, no bypass of Hive.Management;
- build/test execution deferred to Preview 2 behind isolation toggle.
- Workspace UX specification (required before UI implementation): main Workspace layout and navigation (conversation, Hive activity, projects, tools panes); observed activity (specialist assignments, tool calls, guard decisions, budget status); how pause/stop/approvals behave; how final artifacts are presented; UI acceptance covers initial state, long-running tasks, failures, cancellation, empty states, and resizing.

Verify:
- single-instance behavior: target is application-wide single instance for the standalone executable (one window per user session), implemented via a session-local named mutex plus inter-process activation of the existing window. Scope decision: one instance per user session (not cross-session); use a session-local mutex name Local\HiveWorkbench.SingleInstance.<profile> (no Global\ prefix, which would span terminal-server sessions and contradict the per-session target) and a session-local activation channel. Access control: the mutex and activation channel are secured with an ACL limited to the owning user (owner SID); only processes running as that user may signal the existing instance; the activation request carries a validation token and is rejected on mismatch. Startup race handling: distinguish "no instance owns the mutex" from "an instance owns it but activation failed." In the latter case, retry activation, then report a startup error or exit safely; do not silently start a second instance. Per-launcher reuse is not an automatic race fallback that preserves the cross-process guarantee. Abandoning the application-wide guarantee in favor of per-launcher reuse remains a conscious product decision, recorded explicitly. Design details to be specified before implementation: mutex name template, how the second process locates and activates the first window (e.g., a session-local named pipe/WM_COPYDATA/registered window message carrying the activation request and token), handling of startup races (mutex acquisition timeout, retry, then fail with a recorded error rather than start a duplicate), and cleanup on abnormal exit. Cross-session single-instance is explicitly not targeted; if ever required, it must be a separate design with cross-session activation semantics. Per-launcher reuse (repeated Show() reusing the existing window) is the minimum guaranteed fallback when no mutex is held. If application-wide single-instance cannot be delivered, narrow the acceptance test to the per-launcher guarantee and record the decision.
- modules load on first activation and reload on Management recomposition; third-party modules participate in refresh via a public lifecycle contract (or by subscribing to the public context event and refreshing safely);
- onboarding completes without SQL Server;
- project files scoped to permitted directories;
- Example scenario runs end-to-end with fake provider.
