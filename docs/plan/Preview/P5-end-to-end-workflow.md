# Preview — End-to-End Coordinated Run (Manager / Hive / Specialists) [Target]

Objective: define the complete preview-only orchestration flow that lets the Workbench complete a substantial user request through coordinated Agents, reusing existing `Hive.Management` and MAF boundaries. This plan must not create a second coordination engine.

Scope:
- A user request becomes a durable task/run owned by the governing Hive instance and its Manager; the Manager is the single coordination owner.
- Manager decomposition: the Manager breaks the request into sub-tasks, assigns existing specialists or requests governed creation (P2), and coordinates concurrent work through MAF-owned orchestration primitives (no Hive-side second engine).
- Critic participation: a critic role reviews intermediate/final results; the Manager combines results and determines completion against the request's success criteria.
- Pause/stop/cancellation/failure/restart: define propagation to all participating Agents, reservation release (P2), and durable recovery of run state; application restart resumes or fails the run deterministically.
- Correlation: conversation history, specialist activity, generated files, and final artifacts are correlated to the run via durable identifiers; evidence recorded per guard decisions (P2) and configuration changes.
- Reuse boundaries: Hive.Management owns management/application operations; MAF owns orchestration mechanics; Hive.Host.WinForms does not bypass Management; no host business state in Hive persistence.

Verify:
- a substantial request completes end-to-end through coordinated specialists in the Example host;
- pause/stop/cancellation propagate and leave no orphaned reservations or specialists;
- restart recovers run state deterministically (resume or fail-closed);
- no second coordination engine introduced (orchestration delegated to MAF).