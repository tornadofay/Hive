# Preview — Autonomous Population & Two-Level Guards [Target]

Objective: implement governed autonomous specialist creation with two-level guards (absolute ceilings + configurable operating limits) and atomic admission/reservation.

Governing Hive identity and lifecycle (scope anchor):
- The Workbench has exactly one governing Hive instance per run/session scope; it carries a persistent identity (Hive id) that keys membership, creation, budget, and guard evidence.
- Limits, counters, and reservations are always scoped to that governing Hive identity; they never span unrelated Hives.
- Lifecycle: a governing Hive may be archived or reset only through an authorized management action; archiving/resetting establishes a fresh allowance as specified below.

Absolute ceilings (concrete, non-toggleable) — proposed values, subject to developer confirmation:
- Max recursion depth: governing Hive = depth 0. Permitted specialist depths: 1, 2, 3. A creation that would place a specialist at depth 4 is rejected. Scope: per creation chain; reset: n/a (structural).
- Max creation rate: 10 specialists per 60-second sliding window. Scope: per governing Hive instance. Counter tracks creation attempts admitted to the creation pipeline (including those later dedup-resolved), so the limit cannot be bypassed by spamming duplicates. Rejected attempts that never pass initial validation are not counted. Reset: window slides; no manual reset.
- Max creation count (lifetime): 256 specialists per governing Hive instance (includes dormant). Scope: per Hive lifetime; counter survives application restart via persisted state. Reset only on explicit authorized Hive archive/reset, which establishes a fresh allowance; dormancy does not reset it. Archiving/resetting requires the same authorization as other destructive management actions and is recorded as evidence.
- Max creation count (per task): effective task cap = min(configured task population cap, effective persistent membership allowance). Scope: per task run. Reset: end of task.
- Authorization enforcement, sandbox, secret redaction, process termination, exact duplicate prevention, provider/platform quotas: hard floors; not numeric.
- Task population vs. membership: the effective task cap can never exceed the available membership allowance (min(configured task cap, effective membership cap)). Configurable caps are always ≤ the corresponding absolute ceiling; Settings may lower but never raise above the ceiling.

Scope:
- Relationship to configurable limits: configurable caps (membership 4–64, task population 1–16, concurrent 1–8) are always ≤ the corresponding absolute ceiling; Settings may lower but never raise above the ceiling. Disabling a configurable restriction never disables its enforcement mechanism.
- configurable operating limits bounded by ceilings: persistent membership cap (default 12, 4–64), task population cap (default 6, 1–16), concurrent execution cap (default 4, 1–8), duplicate strictness (semantic threshold), per-run budget cap (tokens+cost, shared current usage), dormancy policy, consequential-action approval, creation-log visibility;
- atomic admission/reservation for concurrency; immutable effective policy snapshot per run; Settings changes apply to new runs only;
- per-run policy snapshot captures operating policy (limits/preferences) only. Live enforcement is never snapshotted: current authorization, credential validity, cancellation, emergency stop, and hard safety requirements are evaluated on every consequential action. A snapshot must never grant authority that has since been revoked.
- concurrent specialty creation dedups at the catalog boundary;
- reversible dormancy instead of destructive auto-retirement; dormancy retains specialist identity, memory, and history but does NOT retain CognitiveAgent-style learning/experience (Preview 1 retained state is limited to identity, memory, history);
- guard decisions recorded as attributable evidence.

Budget enforcement (atomic):
- Shared budget reservation: before any concurrent specialist work begins, reserve the estimated token/cost against a shared budget ledger keyed by run; admission fails closed when the reservation would exceed the per-run cap or provider/platform quota.
- Reconciliation: on completion, reconcile the reservation against actual provider-reported usage; refund unused reservation; charge overage against the run (and surface it) when actual exceeds estimate.
- Separate limits: a token limit (tokens) and a monetary limit (cost in the configured currency unit); each has its own reservation and reconciliation rule; exceeding either fails closed.
- Reservation identity and restart safety: reservations carry a stable reservation id, run id, governing Hive id, estimate basis, and timestamps; reconciliation is idempotent by reservation id (no double-settlement after restart).
- Uncertain provider outcomes: distinguish (a) a request confirmed not to have been submitted (release the reservation) from (b) a request submitted with an unknown outcome (retain a conservative reservation until reconciliation; release only the portion known to be unused). A locally cancelled or timed-out call may still have reached the provider and incurred cost.
- Worst-case estimate: when the selected provider has no usable pricing information, use a defined fail-closed outcome — deny admission rather than allow an unbounded charge. "Worst case" is only enforceable with a defined bound; absent one, admission is denied.
- Over-budget result: blocks further admissions and stops additional work for the run when appropriate; surfaces the overage.
- Edge cases: unknown pricing or missing usage data → treat as worst-case estimate and flag for review, never silently allow; cancellation releases the reservation (only the confirmed-unused portion for unknown outcomes); failed/interrupted calls release the reservation (no double-release, no permanent hold); partial results are not billed as full.
- Disabling the soft per-run budget cap (configurable) never bypasses provider/platform quotas or other mandatory execution limits.
- Mapping to Phase 1.30: the preview's shared per-run budget maps to the established per-runtime budget boundary in `docs/plan/Phase1/1.30.md`; the preview extends it with multi-Agent sharing and reservation semantics rather than introducing a second budget scope.

Verify:
- concurrent requests do not exceed concurrent cap;
- effective task cap never exceeds available membership allowance;
- failed/interrupted runs do not double-release or permanently hold a reservation; uncertain outcomes retain a conservative reservation;
- Settings change mid-run does not alter in-flight authority, but revocation/cancellation/emergency stop take effect immediately;
- two concurrent identical creation requests yield one specialist;
- exact duplicate never possible regardless of strictness setting;
- dormancy preserves identity/memory/history and is reversible;
- guard decisions and configuration changes recorded as attributable evidence (actor, time, previous and new values, affected scope, applicable policy profile);
- "creation-log visibility" changes presentation only and never disables recording of guard evidence.
- SQL Server and Embedded parity for guard/policy persistence where applicable.
