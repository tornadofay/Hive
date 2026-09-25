# Hive Agent Cheat Sheet

**Authority by role:** `AGENTS.md` = workflow rules; architecture = intended design; Active Work = current authorization/scope; roadmap = future order only; source/tests = implementation/evidence; status/verification = current or historical state.

**Continue:** same task; stop at verification gate.

**Revision:** production-depth re-audit and correction of the preceding task's findings; review the result again; never next phase. With open implementation Active Work, stay inside it. With no Active Work after a preceding task with implementation findings, including Workflow Review, establish a temporary bounded corrective slice before re-auditing and correcting findings. Workflow-documentation Revision stays within the affected governance documents; new capability/public-contract expansion/roadmap work still needs authorization.

**Maintenance:** complete production audit within scope; no roadmap advancement.

**Architecture:** specific design question; no implementation unless explicitly requested.

**Review (workflow mode):** repository-wide Architecture + Production Engineering analysis; inspect beyond Active Work when useful; read-only, report in chat, no repository changes or authorization. Distinct from the business Review lifecycle/capability.

**Verification:** while **VERIFICATION PENDING** awaiting developer results, implementation stops. Failed/in-scope → record **VERIFICATION FAILED / REMEDIATION REQUIRED** first, allow same-slice remediation, then **VERIFICATION PENDING**. Out-of-scope/new capability → authorization required.

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
