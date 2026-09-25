# Hive Workflow Modes

## Continue

Command: Hive: Continue

Meaning: Continue the current authorized task context from the repository checkpoint. For implementation work, this means the current Active Work item. For an explicitly requested governance/documentation task, this means that same governance/documentation scope.

If the current Active Work item is verification-pending or otherwise establishes a developer-verification gate, Continue stops at that gate and returns the exact required verification handoff. Revision, Maintenance, Again, Polish again, and other implementation-affecting follow-ups also stop at that gate; a newly worded implementation request does not silently supersede it. None may resume implementation while the gate remains open. Resume only after the developer supplies the required verification results and the repository reflects the authorized task transition. Separately authorized governance/documentation work may proceed only within its explicitly affected documentation scope and must not weaken or remove the gate.

Do not rewrite Active Work, Status, roadmap, or verification records to make an implementation task appear authorized or verified.

Do not reinterpret completion as permission to start the next roadmap slice.

## Revision

Command: Hive: Revision

Meaning: Re-review the work just performed in the same task context. Do not infer a different task from the current Active Work if the immediately preceding task context is unavailable.

Check whether anything was missed, incorrectly implemented, inconsistently designed, inadequately protected, accidentally changed, or left incomplete.

Fix concrete problems within the same scope and to production depth. Revision is not a minimal-diff exercise; correct root causes and necessary supporting changes when they are within scope.

A second or third Revision is still the same scope.

Revision never advances the roadmap.

## Maintenance

Commands:

Hive: Maintenance — Backend
Hive: Maintenance — UI
Hive: Maintenance — Host/UI

Meaning: Perform a production audit and correction pass in that domain. When Active Work is open, the pass is bounded by its authorization. When Active Work is closed or absent, this explicit Maintenance request establishes a new temporary Active Work slice before implementation, scoped exactly to the request. The same idle-state rule applies to clearly bounded non-roadmap corrective tasks such as bug fixes and regression fixes only when the entire request restores, preserves, or corrects existing behavior. Mixed corrective + feature requests do not qualify for auto-opening unless the user explicitly separates and authorizes the scopes. New capabilities, materially expanded public behavior, or roadmap implementation still require explicit roadmap authorization.

Maintenance is for concrete production defects and quality problems, not speculative future features. Audit the authorized boundary comprehensively enough to leave it production-safe; do not stop at the first symptom-level fix. If a discovered fix requires a new capability or materially expands a public contract, stop that portion and require separate authorization rather than widening the maintenance slice.

Maintenance never advances the roadmap.

## Architecture

Command: Hive: Architecture

Follow with the architecture question or proposed change.

Meaning: Analyze the design, ownership, contracts, trade-offs, and affected source-of-truth documents.

Do not modify implementation merely because a design is discussed.

## Verification

Command: Hive: Verification

Follow with the actual developer build/test/manual results.

Meaning: Reconcile those results against the repository and update only the records justified by evidence.

Do not invent missing verification.

If required verification fails or is partial, classify each failure against the current Active Work scope. For in-scope defects, transition Active Work to **VERIFICATION FAILED / REMEDIATION REQUIRED**, perform same-slice remediation to production depth, then return Active Work to **VERIFICATION PENDING** with updated verification targets. For out-of-scope or new-capability work, stop that portion and require separate authorization.

When verification closes the current slice, move historical verification detail to `docs/verification/` when applicable and leave `docs/Hive_Active_Work.md` current-only: remove the closed slice and retain only the minimal no-active-slice state unless another slice is explicitly authorized.

## Explicit roadmap advancement

Command example: Hive: Start Phase X.Y

This authorizes moving to the named roadmap slice.

A request such as "start the next roadmap slice" also explicitly authorizes advancement; identify the next slice from the repository before changing Active Work.

Completion, maintenance, revision, polish, or "continue/again" language does not authorize advancement.

## Context rule

After a clearly established task:

Continue -> same task

Revision -> review same task

Again -> same task/revision pass

Polish again -> same maintenance scope

An explicit new task may intentionally change mode or narrow scope. When no Active Work slice is open, an explicitly bounded non-roadmap corrective task may establish a temporary Active Work slice only after repository evidence confirms the entire request is corrective rather than new capability work; replace the inactive placeholder with the slice before implementation and do not append it to historical entries. Mixed corrective + feature requests require explicit separation/authorization. New capabilities, materially expanded public behavior, or roadmap implementation require explicit roadmap authorization.

When Active Work exists, a new task may not silently broaden or replace its implementation boundary. If it conflicts with Active Work, report the conflict instead of guessing.

Only explicit roadmap advancement changes the roadmap slice.
