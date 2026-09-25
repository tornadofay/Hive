# Hive Agent Cheat Sheet

## Commands

Hive: Continue
Same authorized work, unless the current Active Work is verification-pending; then stop and return the verification handoff.

Hive: Revision
Review the work just completed. Same scope; stop at any verification gate and do not resume implementation.

Hive: Maintenance — Backend
Production backend audit/fix. Same authorized scope when Active Work is open; otherwise creates a new temporary bounded slice from this request.

Hive: Maintenance — UI
Production UI/UX audit/fix. Same authorized scope when Active Work is open; otherwise creates a new temporary bounded slice from this request.

Hive: Maintenance — Host/UI
Production Host/UI boundary audit/fix. Same authorized scope when Active Work is open; otherwise creates a new temporary bounded slice from this request.

Hive: Architecture
Architecture/design only unless implementation is explicitly requested.

Hive: Verification
Reconcile actual developer verification results.

Hive: Start Phase X.Y
Explicitly advance to the named roadmap slice.

Hive: Start the next roadmap slice
Explicitly advance to the next roadmap slice selected from the repository.

## Rules

Continue = same work.

Revision = check the work just done.

Maintenance = production audit/fix.

Architecture = design.

Verification = reconcile real results.

Start Phase X.Y = explicitly advance.

## Never

"Again" does not mean next phase.

"Continue" does not mean next phase.

"Revision" does not mean next phase.

"Polish again" does not mean next phase.

Finishing a slice does not authorize the next slice.

## Verification-pending Active Work

STOP all implementation until the developer supplies the required verification results.

A newly worded implementation request does not silently supersede the gate.

Do not edit Active Work or Status to bypass the gate.

## Closed Active Work

An explicit new bounded non-roadmap corrective request (maintenance, bug fix, regression fix, audit, or polish) may establish a temporary Active Work slice before implementation only when repository evidence confirms the entire request restores/preserves/corrects existing behavior; replace the inactive Active Work placeholder with the temporary slice before implementation.

After a slice closes, remove the closed slice from `docs/Hive_Active_Work.md`. Keep historical verification details in `docs/verification/`; Active Work is current-state only. Mixed corrective + feature requests require explicit separation/authorization. New capabilities, materially expanded public behavior, or roadmap implementation still require explicit roadmap authorization.

`Continue`, `Again`, and similar continuation language still stop when no prior authorized task exists.

Roadmap advancement still requires explicit user authorization.

A corrective pass that discovers a required new capability or material public-contract expansion must stop that portion and require separate authorization.

## Revision safety

Revision follows the immediately preceding task context.

If that context cannot be safely identified, do not guess and do not modify the repository.
