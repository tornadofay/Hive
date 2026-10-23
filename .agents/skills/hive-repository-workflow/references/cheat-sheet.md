# Hive Agent Cheat Sheet

Pointer only. Definitions live in `AGENTS.md` section 3 and `modes.md`.

**Continue:** same authorized task. Fresh context may use open Active Work. Stop at verification gate.

**Revision:** re-audit and correct the immediately preceding task at production depth. Same scope. Never next phase.

**Again:** repeat the same task/Revision pass.

**Polish:** repeat the same maintenance/polish scope.

**Maintenance — Backend / UI / Host-UI:** complete production audit/correction in the named boundary.

**Architecture:** design/analysis only unless implementation is explicitly authorized.

**Repository Review:** repository-wide, read-only, advisory only. **Workflow Review:** governance documents only, read-only.

**Verification:** reconcile actual developer results; failed in-scope work is recorded before remediation and returned to verification pending. Partial stays pending; waivers are recorded separately.

**Phase / slice:** a phase is a roadmap item (for example 1.20); a slice is its internal verification unit. `Hive: Start next slice` stays inside the authorized phase and never while verification is pending. Only explicit user authorization advances the roadmap.

**No Active Work:** explicit bounded corrective work may create a temporary slice when genuinely corrective. New capability/feature work cannot auto-open.

**Engineering:** bounded scope, production depth, root-cause correction, no speculative unrelated work.

**Evidence:** never claim unexecuted verification; preserve historical attempts.
