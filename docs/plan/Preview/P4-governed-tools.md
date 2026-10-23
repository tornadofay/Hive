# Preview — Governed Tools & Authorization Boundary [Target]

Objective: define the file/artifact Tool contracts, project-directory scope enforcement, and execution behavior for the Preview 1 Workbench, building on the general governance boundary in Phase 1.22.

Scope:
- Tool contracts for reading/writing project files and artifacts (inputs, outputs, error types);
- concrete operations: ReadFile, WriteFile, ListDirectory, DeleteFile, plus artifact read/write; each with typed input/output records and a typed error result (success | typed failure) — no exceptions across the boundary;
- permission requirements: caller must hold the required permission for each operation; authorization evaluated per invocation against current authorization (never snapshot);
- project ownership: every operation is bound to a project id; the project's root directory is the scope boundary;
- size limits: max file size and max directory listing count, enforced before read/write;
- cancellation: every operation accepts a cancellation token and stops promptly; partial writes are not left on cancellation;
- write semantics: atomic write via a temp file in the same directory; the final replacement must be performed through a handle-bound mechanism (e.g., a handle-based file-information/rename operation over the validated file and directory handles, with replace-if-exists semantics), not through path-based APIs such as `File.Replace` or `MoveFileEx`, which re-resolve by path and defeat the TOCTOU protection. The exact handle-based replacement semantics must be verified during implementation; if a safe atomic replacement cannot be bound to the validated handles, the operation fails closed rather than falling back to a path-based replace. Denied/cancelled/substituted targets leave no partial artifact.
- project-directory scoping enforced at the Tool boundary (canonical path, traversal, symlink, reparse-point validation);
- TOCTOU safety: canonical path validation alone is insufficient. The implementation must bind the file operation to the validated target using a safe handle-based mechanism: resolve and validate the canonical path; open the containing directory with a safe handle; open the target relative to that directory handle with reparse-point following disabled (inspect via FILE_FLAG_OPEN_REPARSE_POINT where needed); verify the opened handle's final path matches the canonical path via GetFinalPathNameByHandle (or equivalent); reject on mismatch. Directory-handle sharing (e.g., FILE_SHARE_READ without FILE_SHARE_DELETE) prevents the directory itself from being renamed/moved while the handle is held, but it does not by itself prevent a child entry from being substituted, because sharing flags govern access to the opened directory object, not to its children. Therefore the no-substitution guarantee for the destination must be established separately and provably: either hold a handle to the destination that denies delete/rename sharing for the duration and perform the replacement through a mechanism that does not require releasing that handle, or use an equivalent OS-supported atomic compare-and-swap/identity check that fails if the destination entry changed. If neither can be guaranteed, the operation fails closed. Write semantics: create the temp file through the directory handle, then perform the final replacement through a handle-bound rename/file-information operation over the validated handles (not File.Replace/MoveFileEx); if no safe handle-bound replacement is available, fail closed. Ancestor stability is established by validating and holding each ancestor handle in the same way, or by failing closed. Verification must prove that a substituted destination is never overwritten, not only that the operation stays within the correct directory.
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
