# Post-1.17 — Plain Text Input Support — Closure Verification

Date: 2026-10-07

## Authorization

This bounded capability was explicitly authorized after Phase 1.17 closure:

" I authorize .txt only "

The scope was limited to first-class plain-text `.txt` / `text/plain` input support in the existing input-preparation → structured-extraction pipeline.

Excluded from this slice: PDF, DOC/DOCX, Markdown-specific parsing, CSV/table parsing, OCR, generalized Document abstractions, replacement provider/orchestration/persistence architecture, and future roadmap slices.

## Final automated verification

Developer-reported final full automated verification:

708 Tests (708 Passed, 0 Failed, 0 Skipped)
Run time: approximately 1.5 minutes

This final run includes the focused text preparation, structured extraction, Management batch-processing, durability, failure-isolation, malformed-output, cancellation, and routing coverage.

## Example Host verification

The required Phase 1.17 Example Host scenario was manually exercised with the authorized text capability included:

`Workspace / WorkItem Operations / Structured Extraction & Validation`

Developer-reported result:

- Source items: 5
- Prepared inputs: 5
- Preparation failures: 1
- Mapping LLM calls: 1
- Structured candidate calls: 2
- Mapping contexts: 1
- Final batch status: Accepted
- Reloaded status: Accepted

Accepted items:

- Image `invoice-a.png`
- Spreadsheet `invoice-a.xlsx` / `Orders!row 2`
- Spreadsheet `invoice-a.xlsx` / `Orders!row 3`
- Spreadsheet `invoice-b.xlsx` / `Orders!row 2`
- Text `notes.txt`

The `notes.txt` item produced a valid structured candidate with `Text` provenance:

- invoice.number = `TXT-001`
- customer.name = `Text Customer`
- invoice.amount = `30.5`

The unsupported `extra.bin` item failed with the existing unsupported-input boundary:

`hive.input.unsupported` for `application/octet-stream`

The run reported:

- Business writes performed: 0
- Host controls or business database mutated: 0

Durable reload returned `Accepted`, confirming the text result survived the persistence/reload path exercised by the scenario.

## Remediation history

The first 708-test verification attempt found two failures within this authorized slice.

1. The unsupported-input regression used `notes.txt`, which had become supported by this slice. The fixture was corrected to `notes.bin` / `application/octet-stream` so the test continued to exercise a genuinely unsupported input.

2. Durable prepared-input matching relied on the original submission `ItemIndex`. A spreadsheet source can expand into multiple durable row items, so later prepared image/text items can have a different durable logical index. The matcher was corrected to use source identity for image/text inputs (file name plus source fingerprint), while spreadsheet rows retain file/worksheet/row identity.

After remediation, the developer reran the full suite and reported 708/708 passing.

## Delivered capability

The authorized slice provides:

- `.txt` file detection as `text/plain`;
- `InputSourceKind.Text` and bounded `PreparedTextInput`;
- strict UTF-8 decoding with BOM handling;
- a 1 MiB text-file bound and 1,000,000-character prepared-text bound;
- structured candidate extraction through the existing structured-output execution boundary;
- text provenance and SHA-256 source fingerprinting;
- durable Management processing and restart reload;
- deterministic failures for empty text and invalid UTF-8;
- focused automated regression coverage;
- Example Host coverage proving supported text processing and isolation of an unsupported binary input.

## Scope closure

This bounded post-1.17 capability is complete and verified on 2026-10-07.

No new roadmap phase was activated, and no broader document/input capability was introduced.
