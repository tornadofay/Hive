# Hive — Active Work

Status: IN PROGRESS

## Maintenance — Review Corrections

Opened: 2026-09-27

Scope: Correct the concrete production problems identified by the 2026-09-27 repository-wide Review, without advancing the roadmap or introducing new capability.

Authorized correction boundary:
- bind host lookup authorization to the requested lookup identity;
- make the existing host-version expectation contract either enforced or remove the unused expectation surface; retain meaningful host-version evidence where supported;
- restore consistent production use of the existing `IClock` abstraction at affected time-sensitive boundaries and remove the identified wall-clock test polling;
- enforce structural identity/capability uniqueness in neutral host contracts where ambiguity is invalid;
- stop silently converting WinForms binding-inspection failures into incomplete semantic snapshots;
- make `HiveHostValue` DateTime semantics deterministic and timezone-explicit;
- preserve/update focused automated tests and relevant verification documentation for these corrections.

Explicit exclusions:
- no Phase 1.16+ implementation;
- no new host capabilities or new business workflow;
- no unrelated refactoring or dependency upgrades;
- no roadmap advancement.

Checkpoint: main @ a778b2414704d76190eedeba94560503c3702ffa

Verification state: IMPLEMENTATION IN PROGRESS

Verification gate: after implementation, return Active Work to VERIFICATION PENDING with exact developer rerun targets. Do not claim tests/builds/manual verification that were not actually performed.
