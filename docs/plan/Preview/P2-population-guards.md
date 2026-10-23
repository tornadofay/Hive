# Preview — Autonomous Population & Two-Level Guards [Target]

Objective: implement governed autonomous specialist creation with two-level guards (absolute ceilings + configurable operating limits) and atomic admission/reservation.

Absolute ceilings (concrete, non-toggleable) — proposed values, subject to developer confirmation:
- Max recursion depth: 4 levels (governing Hive = 0; a specialist created at depth 4 is rejected). Scope: per creation chain; reset: n/a (structural).
- Max creation rate: 10 specialists per 60-second sliding window. Scope: per governing Hive instance. Reset: window slides; no manual reset.
- Max creation count (lifetime): 256 specialists per governing Hive instance (includes dormant). Scope: per Hive lifetime; reset only on explicit Hive archive/reset (not on dormancy).
- Max creation count (per task): bounded by the effective persistent membership cap (≤ 64). Scope: per task run. Reset: end of task.
- Authorization enforcement, sandbox, secret redaction, process termination, exact duplicate prevention, provider/platform quotas: hard floors; not numeric.

Scope:
- Relationship to configurable limits: configurable caps (membership 4–64, task population 1–16, concurrent 1–8) are always ≤ the corresponding absolute ceiling; Settings may lower but never raise above the ceiling. Disabling a configurable restriction never disables its enforcement mechanism.
- configurable operating limits bounded by ceilings: persistent membership cap (default 12, 4–64), task population cap (default 6, 1–16), concurrent execution cap (default 4, 1–8), duplicate strictness (semantic threshold), per-run budget cap (tokens+cost, shared current usage), dormancy policy, consequential-action approval, creation-log visibility;
- atomic admission/reservation for concurrency; immutable effective policy snapshot per run; Settings changes apply to new runs only;
- concurrent specialty creation dedups at the catalog boundary;
- reversible dormancy instead of destructive auto-retirement;
- guard decisions recorded as attributable evidence.

Budget enforcement (atomic):
- Shared budget reservation: before any concurrent specialist work begins, reserve the estimated token/cost against a shared budget ledger keyed by run; admission fails closed when the reservation would exceed the per-run cap or provider/platform quota.
- Reconciliation: on completion, reconcile the reservation against actual provider-reported usage; refund unused reservation; charge overage against the run (and surface it) when actual exceeds estimate.
- Edge cases: unknown pricing or missing usage data → treat as worst-case estimate and flag for review, never silently allow; cancellation releases the reservation; failed/interrupted calls release the reservation (no double-release, no permanent hold); partial results are not billed as full.
- Disabling the soft per-run budget cap (configurable) never bypasses provider/platform quotas or other mandatory execution limits.

Verify:
- concurrent requests do not exceed concurrent cap;
- failed/interrupted runs do not double-release or permanently hold a reservation;
- Settings change mid-run does not alter in-flight authority;
- two concurrent identical creation requests yield one specialist;
- exact duplicate never possible regardless of strictness setting;
- dormancy preserves identity/memory/history and is reversible.
