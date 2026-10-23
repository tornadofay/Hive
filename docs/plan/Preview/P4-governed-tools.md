# Preview — Governed Tools & Authorization Boundary [Target]

Objective: define the file/artifact Tool contracts, project-directory scope enforcement, and execution behavior for the Preview 1 Workbench, building on the general governance boundary in Phase 1.22.

Scope:
- Tool contracts for reading/writing project files and artifacts (inputs, outputs, error types);
- project-directory scoping enforced at the Tool boundary (canonical path, traversal, symlink, reparse-point validation);
- authorization: caller must hold the required permission; Tools never bypass `Hive.Management`;
- no universal UI automation, host DB access, or arbitrary reflection;
- secret redaction across logs/errors/tool results/telemetry/evidence;
- build/test execution explicitly deferred to Preview 2 behind an isolation toggle (project-directory restriction alone is not a sandbox: child processes, network/machine-resource access, cancellation, cleanup).

Verify:
- out-of-scope path rejected with typed error;
- symlink/reparse traversal rejected;
- authorization failure returns typed denial, not partial execution;
- secret-like values redacted in all surfaces;
- project files scoped to permitted directories.
