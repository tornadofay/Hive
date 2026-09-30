# Phase 1.17 — Structured Extraction & Validation

## Purpose

Phase 1.17 turns the completed Phase 1.15 prepared-input boundary into a source-neutral, typed candidate boundary that can later feed business-operation proposals.

The phase covers practical batch intake, semantic field mapping, image/vision extraction, candidate normalization, validation, provenance, and human-reviewable results. It does not mutate the host business application.

## Resolved design decisions

- **Target schema source:** Phase 1.17 consumes a target semantic-field schema supplied by the existing host/business semantic boundary. It does not invent a database schema and it does not require Phase 1.17 to define the later business-operation capability itself.
- **Mapping unit:** the durable semantic mapping context is the combination of one source table-like region (normally one worksheet/table region with its headers) and one target semantic-field schema. A workbook may therefore contain multiple independent mapping contexts.
- **Cross-file mapping reuse:** different Excel files or worksheets may reuse an existing mapping only after Hive deterministically establishes compatible source structure and the same target semantic-field schema. Otherwise a new mapping context is created and mapped once.
- **Mapping durability:** once accepted, the mapping and its review state are retained with the associated durable Hive work/batch state so a restart does not force the LLM to remap the same source context or erase the user's corrections.
- **Candidate durability:** extracted/reviewed candidates and per-item processing results are retained with the associated durable work state while they are awaiting downstream use. This is Hive-owned state, not host business data.
- **Image execution:** each image has an independent extraction attempt against the applicable target schema and ExecutionTarget. The batch orchestrates many attempts, but a model request is not required to combine multiple images into one call; per-image independence is preserved for failure isolation and provenance.
- **Human correction:** mapping edits and candidate-value edits are deterministic user changes recorded as part of the reviewable state; the LLM is not called again merely because the user corrected a mapping/value.
- **Accepted set:** the second human checkpoint applies to an explicit accepted subset of the batch. Failed, rejected, or still-uncertain items remain outside that accepted set until resolved.

## Phase boundary

The flow is:

```text
Input selection
    ↓
InputSubmission / prepared inputs
    ↓
Target semantic-field contract
    ↓
Source interpretation
    ├── Spreadsheet profiling + one-time mapping
    └── Image/vision extraction
    ↓
StructuredCandidate
    ↓
Validation
    ↓
Reviewable candidate result
    ↓
later governed business-operation pipeline
```

StructuredCandidate is deliberately source-neutral. Later business-operation code must not need to know whether the candidate came from Excel, an image, or another future supported input.

## Implementation slices

### 1. Input Selection & Batch Construction

The user can explicitly choose:

```text
Single File
Folder
```

A single file becomes a one-item input submission. A folder becomes one bounded batch of discovered input items.

A folder may contain:
- multiple images;
- multiple Excel files;
- supported and unsupported file types together.

Folder enumeration is non-recursive by default. An explicit Include Subfolders choice may opt into bounded recursive enumeration; resource limits and cancellation still apply. Unsupported files are reported at item level and must not discard safe supported items.

The existing InputSubmission / InputItem model remains the normalized internal boundary.

### 2. Target Schema & Semantic Field Contract

Extraction and mapping consume target-field information from the owning host/business semantic boundary.

A target field may expose:
- stable semantic field identity;
- display name;
- expected type;
- requiredness;
- parent/child placement;
- data-source identity;
- optional database-field reference;
- bounded lookup/reference semantics where applicable.

Data-source identity, database column names, control names, and display labels can be supporting evidence, but they are never durable semantic identity; stable semantic field identity is the mapping key.

### 3. Spreadsheet Profiling & One-Time Semantic Mapping

Hive reuses the completed Phase 1.15 bounded .xlsx reader for workbook/worksheet/row mechanics.

For an applicable spreadsheet mapping context, Hive gathers:
- workbook/worksheet structure;
- header information;
- a bounded representative sample of row values;
- target semantic-field information.

The LLM is called once to propose source-column → target-semantic-field mappings.

The mapping is then validated deterministically by Hive and presented as a reviewable/editable artifact.

Once accepted, the mapping is applied deterministically to all applicable rows in that mapping context. Hive must not call the LLM once per row to rediscover the same mapping.

A mapping context is normally one workbook structure plus the target semantic-field schema. Multiple Excel files in one batch may share one mapping only after Hive deterministically establishes compatible source structure and target context; otherwise each distinct structure receives its own mapping.

The mapping retains source context/provenance and the stable target semantic-field identities.

### 4. Vision Extraction

Each image is processed against the target semantic-field contract through an eligible vision-capable ExecutionTarget.

A batch may contain many images. Processing can be authorized as one batch-level processing decision when the governance policy permits it; there is no inherent requirement for one human authorization per image merely because each image causes an individual model call.

Each image retains:
- source identity;
- processing/execution identity;
- extraction status;
- candidate or typed failure;
- provenance/evidence.

One image failure does not discard successful images in the same batch.

### 5. Structured Candidate & Validation

Extraction results are normalized into a source-neutral StructuredCandidate.

The candidate supports:
- typed field values;
- parent/child data;
- field/item validation state;
- source linkage and provenance;
- relevant extraction/mapping evidence;
- optional confidence metadata.

Hive owns deterministic required-field and type validation.

Domain/business validation remains owned by the applicable host/business semantic contract. Hive must not recreate arbitrary host business rules inside the extraction subsystem.

Malformed model output fails safely as a typed extraction/serialization failure.

Invalid or incomplete fields remain explicit and reviewable instead of being silently discarded.

### 6. Reviewable Results & Handoff

The user can review:
- the mapping proposed for a spreadsheet mapping context;
- extraction results;
- candidate values;
- validation errors;
- per-file/per-item failures.

For image failures, the UI should show the image file name and safe failure information and permit opening the original image where supported.

The user can edit mappings and candidate values before downstream business-operation work, exclude failed or uncertain items from the accepted set, and authorize that accepted set for downstream use.

The result handed forward is a stable, reviewable candidate set. It contains no host mutation.

## Human-review and authorization model

Phase 1.17 has two processing-stage human checkpoints, followed later by the business-operation authorization:

1. **Processing authorization checkpoint** — after Single File or Folder selection, the user may authorize the selected batch as one processing unit when policy requires it. This can cover many image model calls; there is no inherent requirement for one authorization per image.
2. **Candidate/mapping authorization checkpoint** — after processing, Hive presents the spreadsheet mappings and image-extracted candidates, per-item failures, and provenance. The user can inspect the original source, edit mappings/candidate values, and authorize the accepted result set to proceed to downstream use.
3. **Business-operation authorization** — later permission to perform the consequential host mutation. This is not part of the Phase 1.17 extraction boundary.

The second checkpoint approves the interpreted data for downstream use; it does not write to the host application. The actual business-operation proposal and host mutation remain later-phase concerns.

Phase 1.17 defines the artifacts and checkpoints needed for this flow, but the generalized authoritative Approve / Reject intervention state machine remains owned by Phase 1.22. Phase 1.17 must not introduce a second parallel authorization subsystem.

## Safety and determinism

- No host mutation.
- No host database access.
- No secret output.
- No per-row LLM mapping when a mapping has already been established.
- No model-name-only field mapping identity.
- Bounded file, folder, workbook, worksheet, row, cell, and model-input resources.
- Cancellation propagates through the batch.
- Per-item failures remain isolated and attributable.
- Batch grouping never erases individual provenance or failure state.

## Example Host

The matching Example Host scenario is:

Workspace / WorkItem Operations / Structured Extraction & Validation

It should demonstrate:
- Single File and Folder selection;
- a mixed folder containing multiple Excel files, multiple images, and an unsupported file;
- one-time spreadsheet mapping with deterministic reuse;
- human-editable mapping;
- independent image extraction results;
- failed item reporting and original-source inspection;
- candidate normalization and validation;
- parent/child candidate structure where applicable;
- provenance preservation;
- no host mutation.

## Verification

Focused verification should cover the six implementation slices, including:
- selection/batch construction and bounded enumeration;
- target semantic-field contract;
- spreadsheet mapping proposal/validation/reuse;
- image extraction and per-image isolation;
- candidate parsing and validation;
- provenance and reviewable failure/result behavior.

No build, test, or application launch is implied by this planning document.
