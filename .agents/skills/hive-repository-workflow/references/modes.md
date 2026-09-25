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

Fix concrete problems within the same scope.

A second or third Revision is still the same scope.

Revision never advances the roadmap.

## Maintenance

Commands:

Hive: Maintenance — Backend
Hive: Maintenance — UI
Hive: Maintenance — Host/UI

Meaning: Perform a production audit and correction pass in that domain. When Active Work is open, the pass is bounded by its authorization. When Active Work is closed or absent, this explicit Maintenance request establishes a new temporary Active Work slice before implementation, scoped exactly to the request. The same idle-state rule applies to any other explicitly bounded new implementation task.

Maintenance is for concrete production defects and quality problems, not speculative future features.

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

An explicit new task may intentionally change mode or narrow scope. When no Active Work slice is open, an explicitly bounded new implementation or Maintenance task may establish a temporary Active Work slice; it does not advance the roadmap.

When Active Work exists, a new task may not silently broaden or replace its implementation boundary. If it conflicts with Active Work, report the conflict instead of guessing.

Only explicit roadmap advancement changes the roadmap slice.
