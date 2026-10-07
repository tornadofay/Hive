# Hive — Active Work

Status: IN PROGRESS

## Authorized slice

**Phase 1.17 — Structured Extraction & Validation**

Authorization source: explicit task command **“Hive: Start Phase 1.17”**.

Starting checkpoint: `main @ 47def82ebabd60b0c81e766763a964f985f54237`  
Last known repository commit before this slice: `47def82ebabd60b0c81e766763a964f985f54237` — Correct Model Information verification path

## Scope

Implement the bounded Phase 1.17 Structured Extraction & Validation boundary defined by `docs/plan/Phase1/1.17.md`, including:

- explicit Single File / Folder batch construction with bounded, non-recursive-by-default enumeration and cancellation;
- source-neutral target semantic-field contract supplied by the existing host/business semantic boundary;
- bounded spreadsheet profiling and one-time semantic mapping with deterministic validation and reuse;
- independent image/vision extraction per prepared image;
- source-neutral typed `StructuredCandidate` normalization and deterministic required/type validation;
- parent/child candidate structure where required by the target schema;
- provenance/evidence and per-item reviewable failure/result state;
- reviewable mapping/candidate corrections and accepted-subset handling without a second authorization subsystem;
- durable Hive-owned processing state needed to retain accepted mappings, candidate/review state, and per-item processing outcomes across restart;
- matching public Example Host scenario at `Workspace / WorkItem Operations / Structured Extraction & Validation`;
- focused automated regression/contract coverage for the six Phase 1.17 implementation slices.

## Explicit exclusions

- no host business mutation or direct host SQL/database access;
- no business-operation proposal, receipt, reconciliation, or post-write Review implementation;
- no generalized Phase 1.22 Approve/Reject intervention subsystem;
- no Phase 1.18 durable Base-Agent work-state capability beyond the minimum Phase 1.17 processing-state boundary explicitly required to preserve extraction/mapping results;
- no MAF Sequential V1 pipeline composition;
- no per-row LLM remapping after an accepted mapping;
- no replacement spreadsheet-reading stack when the completed Phase 1.15 boundary is sufficient;
- no unrelated provider, UI-foundation, persistence, or architecture refactoring;
- no future roadmap slice activation.

## Implementation checkpoint

Current checkpoint: `main @ 58a369bc86fdb9922cd30778ea43df009aef7897`

Implemented in this slice so far:

- bounded Core Single File / Folder selection with deterministic ordering, subfolder depth/item limits, cancellation, and per-item selection failures;
- source-neutral target semantic-field, spreadsheet mapping, candidate, provenance, parent/child, batch-state, and review contracts;
- OpenAI-compatible bounded multimodal message support and vision request transport allowance;
- deterministic spreadsheet mapping validation/reuse and typed candidate normalization;
- independent image extraction with explicit vision/structured-output capability gates;
- durable event-backed structured-extraction batch state with optimistic concurrency and restart reload;
- Management facade/service operations for batch creation, processing authorization, processing/retry, mapping review/edit, candidate edit, accepted-subset authorization, and durable reload;
- public Example Host scenario and focused automated coverage for input selection, contracts, extraction/multimodal behavior, Management mapping reuse, malformed output, and persistence/restart.

Verification has not been run in this session. The reported compile failures were corrected through the subsequent repair commits, including the final Management result-unwrapping and XLSX fixture delimiter fixes, but the phase remains open until the required focused tests, Example Host manual verification, and full `Hive.Tests` suite are completed and reported by the developer.

## Verification gate

Status: VERIFICATION PENDING

Required verification before closure, based on the Phase 1.17 plan and repository workflow:

- focused tests covering batch selection/enumeration and bounds;
- focused contract tests for the target semantic-field boundary;
- spreadsheet mapping proposal, deterministic validation, edit/reuse, and malformed-output coverage;
- image extraction, cancellation, and per-image failure isolation coverage;
- StructuredCandidate parsing, required/type validation, parent/child structure, and malformed-output coverage;
- provenance/reviewable result and failure-state coverage;
- persistence/restart coverage for the Phase 1.17 processing-state boundary;
- manual Example Host verification for the exact Phase 1.17 scenario;
- full `Hive.Tests` suite after focused verification passes.

No verification result is claimed until the developer actually runs the required checks and reports the results.
