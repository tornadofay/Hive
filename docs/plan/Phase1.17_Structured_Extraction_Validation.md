# Phase 1.17 — Structured Extraction & Validation

## Purpose

Phase 1.17 turns the completed Phase 1.15 prepared-input boundary into a source-neutral, typed candidate boundary that can later feed business-operation proposals.

The phase covers practical batch intake, semantic field mapping, image/vision extraction, candidate normalization, validation, provenance, and human-reviewable results. It does not mutate the host business application.

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

Folder enumeration must have explicit recursion behavior and bounded resource limits. Unsupported files are reported at item level and must not discard safe supported items.

The existing InputSubmission / InputItem model remains the normalized internal boundary.

### 2. Target Schema & Semantic Field Contract

Extraction and mapping consume target-field information from the owning host/business semantic boundary.

A target field may expose:
- stable semantic field identity;
- display name;
- expected type;
- requiredness;
- parent/child placement;
- bounded lookup/reference semantics where applicable.

Database column names, control names, and display labels can be supporting evidence, but they are not durable semantic identity.

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

A mapping context is normally one workbook structure. Multiple Excel files in one batch may share one mapping when their compatible structure can be established; otherwise each distinct structure receives its own mapping.

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

Ther candidate supports:
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

The user can edit mappings and candidate values before downstream business-operation work.

Ther result handed forward is a stable, reviewable candidate set. It contains no host mutation.

## Human-review and authorization model

Phase 1.17 distinguishes three concepts:

1. Processing authorization — permission to perform the selected processing work, potentially for an entire selected batch.
2. Mapping/candidate review — inspection and correction of interpretation results before downstream use.
3. Business-operation authorization — later permission to perform the consequential host mutation.

Phase 1.17 defines the artifacts and review points needed for these distinctions, but the generalized authoritative Approve / Reject intervention state machine remains owned by Phase 1.22. Phase 1.17 must not introduce a second parallel authorization subsystem.

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
