# Preview — Governed Tools & Authorization Boundary [Target]

Objective: define the file/artifact Tool contracts, project-directory scope enforcement, and execution behavior for the Preview 1 Workbench, building on the general governance boundary in Phase 1.22.

Scope:
- Tool contracts for reading/writing project files and artifacts (inputs, outputs, error types);
- concrete operations: ReadFile, WriteFile, ListDirectory, DeleteFile, plus artifact read/write; each with typed input/output records and a typed error result (success | typed failure) — no exceptions across the boundary;
- permission requirements: caller must hold the required permission for each operation; authorization evaluated per invocation against current authorization (never snapshot);
- project ownership: every operation is bound to a project id; the project's root directory is the scope boundary;
- size limits: max file size and max directory listing count, enforced before read/write;
- cancellation: every operation accepts a cancellation token and stops promptly; partial writes are not left on cancellation;
- write semantics: atomic write (temp file + replace) or defined failure behavior; denied/cancelled operations leave no partial artifacts;
- project-directory scoping enforced at the Tool boundary (canonical path, traversal, symlink, reparse-point validation);
- TOCTOU safety: canonical path validation alone is insufficient. The implementation must bind the file operation to the validated target using a safe handle-based mechanism: resolve and validate the canonical path; open the containing directory with a safe handle; open the target relative to that directory handle with reparse-point following disabled (inspect via FILE_FLAG_OPEN_REPARSE_POINT where needed); verify the opened handle's final path matches the canonical path via GetFinalPathNameByHandle (or equivalent); reject on mismatch. Parent-directory substitution is prevented by holding and re-validating the directory handle identity before use. For writes, use a temp file in the same directory followed by an atomic replace (File.Replace/MoveFileEx) constrained to that directory; denied/cancelled/substituted targets leave no partial artifact. Verification must exercise the race (target substitution between validation and use), not only a pre-existing symlink.
- authorization: caller must hold the required permission; Tools never bypass `Hive.Management`;
- no universal UI automation, host DB access, or arbitrary reflection;
- secret redaction across logs/errors/tool results/telemetry/evidence;
- build/test execution explicitly deferred to Preview 2 behind an isolation toggle (project-directory restriction alone is not a sandbox: child processes, network/machine-resource access, cancellation, cleanup).

Verify:
- out-of-scope path rejected with typed error;
- symlink/reparse traversal rejected, including substitution between validation and use (race exercised);
- authorization failure returns typed denial, not partial execution;
- cancellation leaves no partial artifact;
- size limit enforced before read/write;
- secret-like values redacted in all surfaces;
- project files scoped to permitted directories.
