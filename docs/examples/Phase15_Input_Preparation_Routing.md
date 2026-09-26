# Phase 1.15 — Input Preparation & Routing

## Purpose

Phase 1.15 establishes the V1 input-preparation boundary before typed structured-candidate extraction.

The initial supported paths are:

```text
Image
  → required vision capability
  → selected ExecutionTarget
  → PreparedImageInput

.xlsx spreadsheet
  → workbook
  → worksheets
  → header/data rows
  → PreparedSpreadsheetRowInput
```

Both paths converge on the prepared-input boundary. Typed candidate extraction and validation belong to Phase 1.16.

## Input submission

`InputSubmission` groups a bounded set of `InputItem` values under one submission identity. The grouping is operational context only. One submission may produce multiple prepared inputs, and each prepared input retains the originating submission and source-item index.

Input items copy their content at construction time and reject unsafe file names, empty content, and content above the common 32 MiB input bound. Source-specific limits are then enforced by the preparation engine:

- images: the existing 10 MiB WorkItem image-content limit;
- `.xlsx`: 16 MiB;
- a submission: 32 items and 32 MiB total content.

## Image routing

Image preparation requires an `ExecutionTarget` with the existing `vision` capability explicitly marked `Supported`. `Unknown` and `Unsupported` states do not satisfy the required capability.

The existing `ExecutionTargetSelector` performs the selection with a required `vision` capability requirement. The selected target identity and routing diagnostics are preserved in `PreparedImageInput`.

Phase 1.15 does not invoke the provider. The selected target is only the routing destination for the later image interpretation/extraction boundary.

## Spreadsheet preparation

The initial spreadsheet boundary accepts OOXML `.xlsx` packages. Workbook relationships identify the worksheets to process. Row 1 is the header row; subsequent non-empty rows become independent `PreparedSpreadsheetRowInput` values.

Cell values are prepared as bounded strings. Shared strings, inline strings, booleans, and cached/default cell values are supported. Formatting and business-specific type interpretation remain outside this phase.

Package safety and resource limits include bounded archive entries, XML entry size, document character count, shared-string count/size, worksheet count, physical row count, column count, and cell value length. Relationship targets that attempt to leave the package are rejected.

## Failure isolation

Unsupported input types and malformed/oversized individual items produce typed `InputPreparationFailure` entries while safe independent inputs continue.

Worksheet and row failures are isolated where the package remains safely processable. Cancellation remains submission-level and is propagated rather than converted into a per-item failure.

The preparation result may therefore contain both prepared inputs and failures.

## Management boundary

`IHiveManagementFacade.PrepareInputAsync` is the consumer-facing entry point. Management retrieves active, access-scoped execution targets only when the submission contains an image. Spreadsheet-only preparation does not require a provider-resource lookup.

No provider transport, host mutation, SQL/database business access, candidate validation, approval, business operation, receipt, Review, or end-to-end MAF pipeline is introduced by Phase 1.15.

## Example

The externally usable Example Host scenario is:

`Workspace → WorkItem Operations → Input Preparation & Routing`

It exercises the public Management facade with a mixed submission containing:

- one image routed to a vision-capable target;
- one `.xlsx` workbook producing two prepared worksheet rows;
- one unsupported text item producing an isolated failure.

The Example Host is the manual developer verification surface for this capability.
