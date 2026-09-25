# Hive Agent Cheat Sheet

**Authority:** `AGENTS.md` → architecture → Active Work → roadmap → source/tests → status/verification evidence.

**Continue:** same task; stop at verification gate.

**Revision:** same task; production-depth re-review; never next phase.

**Maintenance:** complete production audit within scope; no roadmap advancement.

**Architecture:** specific design question; no implementation unless explicitly requested.

**Review:** repository-wide Architecture + Production Engineering analysis; inspect beyond Active Work when useful; read-only, report in chat, no repository changes or authorization.

**Verification:** reconcile actual results. Failed/in-scope → record **VERIFICATION FAILED / REMEDIATION REQUIRED** first, remediate same slice, then **VERIFICATION PENDING**. Out-of-scope/new capability → authorization required.

**Roadmap:** only explicit user authorization advances it.

**No Active Work:** explicit bounded corrective work may create a temporary slice after repository evidence confirms it is corrective. New capability/feature work cannot auto-open.

**Current-state docs:** Active Work = current slice only. Current Status = current status only. Verification = historical evidence only.

**Evidence:** never claim unexecuted verification. Preserve earlier verification attempts.

**Engineering:** bounded scope, complete production depth; root-cause correction over symptom-only workaround.

**Required capability handoff:**
```text
Example to run: <exact Category / Subcategory / optional AdditionalNavigationPath / Example title> — Hive.Example.WinForms
Tests to run: <exact focused test class/file>; broader-suite requirement if applicable
```
