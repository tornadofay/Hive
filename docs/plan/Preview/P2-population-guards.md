# Preview — Autonomous Population & Two-Level Guards [Target]

Objective: implement governed autonomous specialist creation with two-level guards (absolute ceilings + configurable operating limits) and atomic admission/reservation.

Scope:
- absolute ceilings never toggleable: recursion depth, creation rate, creation count, authorization, sandbox (canonical path/traversal/symlink/reparse), no UI automation/host DB/reflection, secret redaction, process timeout+cancellation, exact duplicate prevention, provider/platform quotas;
- configurable operating limits bounded by ceilings: persistent membership cap (default 12, 4–64), task population cap (default 6, 1–16), concurrent execution cap (default 4, 1–8), duplicate strictness (semantic threshold), per-run budget cap (tokens+cost, shared current usage), dormancy policy, consequential-action approval, creation-log visibility;
- atomic admission/reservation for concurrency; immutable effective policy snapshot per run; Settings changes apply to new runs only;
- concurrent specialty creation dedups at the catalog boundary;
- reversible dormancy instead of destructive auto-retirement;
- guard decisions recorded as attributable evidence.

Verify:
- concurrent requests do not exceed concurrent cap;
- failed/interrupted runs do not double-release or permanently hold a reservation;
- Settings change mid-run does not alter in-flight authority;
- two concurrent identical creation requests yield one specialist;
- exact duplicate never possible regardless of strictness setting;
- dormancy preserves identity/memory/history and is reversible.
"