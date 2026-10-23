# Preview — End-to-End Coordinated Run (Manager / Hive / Specialists) [Target]

Objective: define the complete preview-only orchestration flow that lets the Workbench complete a substantial user request through coordinated Agents, reusing existing `Hive.Management` and MAF boundaries. This plan must not create a second coordination engine.

Scope:
- A user request becomes a durable task/run owned by the governing Hive instance and its Manager; the Manager is the single coordination owner.
- Manager decomposition: the Manager breaks the request into sub-tasks, assigns existing specialists or requests governed creation (P2), and coordinates concurrent work through MAF-owned orchestration primitives (no Hive-side second engine).
- Critic participation: a critic role reviews intermediate results before finalization and the final result before completion; the Manager combines results and determines completion against the request's success criteria. Critic findings that identify a safety, policy, or correctness defect block completion until resolved or explicitly accepted with recorded rationale; stylistic or optional improvements do not block. Disagreement between Manager and critic is resolved by the Manager with the rationale and both positions recorded as evidence.
- Pause/stop/cancellation/failure/restart: define propagation to all participating Agents, reservation release (P2), and durable recovery of run state; application restart resumes or fails the run deterministically.
- Correlation: conversation history, specialist activity, generated files, and final artifacts are correlated to the run via durable identifiers; evidence recorded per guard decisions (P2) and configuration changes.
- Reuse boundaries: Hive.Management owns management/application operations; MAF owns orchestration mechanics; Hive.Host.WinForms does not bypass Management; no host business state in Hive persistence.

Durable run lifecycle:
- Run states: `Created` → `Planning` → `Running` → `Paused` | `Cancelling` | `Completed` | `Failed` | `Recovering`.
- Transitions are explicit and recorded with actor, time, and reason. `Paused` resumes to `Running`; `Cancelling` settles to `Cancelled`; `Failed`/`Cancelled` are terminal; `Recovering` resolves to `Running` or `Failed`.
- Assignment/recovery: each sub-task assignment carries a durable assignment id, specialist id, and run id. On restart, assignments in `Running`/`Paused` are re-evaluated: idempotent sub-tasks resume; non-idempotent or ambiguous sub-tasks fail closed and are re-planned by the Manager. No sub-task is silently re-run without reconciliation.
- Partially completed work: completed sub-tasks are not repeated; their artifacts are reconciled against the run's durable artifact index. Reservations for unresolved provider outcomes follow P2's terminal policy.
- Restart behavior: on application start, any run not in a terminal state enters `Recovering` and is resolved deterministically (resume if all participating specialists and reservations reconcile; otherwise fail closed with a recorded reason). No automatic destructive action occurs.

Verify:
- a substantial request completes end-to-end through coordinated specialists in the Example host;
- the same scenario completes end-to-end in the standalone Workbench, exercising its own composition and onboarding;
- pause/stop/cancellation propagate and leave no untracked executions or assignments; specialists become dormant rather than destroyed;
- restart recovers run state deterministically (resume or fail-closed);
- no second coordination engine introduced (orchestration delegated to MAF).
