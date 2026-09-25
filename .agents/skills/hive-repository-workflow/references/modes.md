# Hive Workflow Modes

## Continue

Command: Hive: Continue

Meaning: Continue the current authorized Active Work item from the repository checkpoint.

Do not reinterpret completion as permission to start the next roadmap slice.

## Revision

Command: Hive: Revision

Meaning: Re-review the work just performed in the same task context.

Check whether anything was missed, incorrectly implemented, inconsistently designed, inadequately protected, accidentally changed, or left incomplete.

Fix concrete problems within the same scope.

A second or third Revision is still the same scope.

Revision never advances the roadmap.

## Maintenance

Commands:

Hive: Maintenance — Backend
Hive: Maintenance — UI
Hive: Maintenance — Host/UI

Meaning: Perform a production audit and correction pass in that domain, bounded by the current Active Work authorization.

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

Command example: Hive: Start Phase 1.14

This authorizes moving to another roadmap slice.

If a future slice is not named, a request such as "start the next phase" is still an intentional roadmap-advance request; identify the next slice from the repository before changing Active Work.

## Context rule

After an implementation task:

Continue -> same task

Revision -> review same task

Again -> same task/revision pass

Polish again -> same maintenance scope

Only an explicit change of scope or explicit roadmap advancement changes that context.
