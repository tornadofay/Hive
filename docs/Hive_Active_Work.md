# Hive — Active Work

Status: IN PROGRESS

## Authorized slice

**Text Input Support — Plain `.txt` only**

Authorization source: explicit user authorization **“I authorize .txt only”** following the completed Phase 1.17 Structured Extraction & Validation boundary.

## Scope

Add first-class plain-text (`.txt` / `text/plain`) input support by extending the existing input-preparation → structured-extraction pipeline.

Included:
- `.txt` media-type detection during file selection;
- `InputSourceKind.Text`;
- bounded UTF-8 text preparation with safe decoding, cancellation, and text-size limits;
- source-neutral prepared text contract;
- structured candidate extraction from prepared text using the existing structured-output execution boundary;
- text provenance and durable per-item result handling;
- Management routing for prepared text;
- focused automated coverage;
- Example Host coverage demonstrating supported `.txt` processing and continued isolation of one unsupported file type.

Explicitly excluded:
- PDF;
- DOC/DOCX;
- Markdown-specific parsing;
- CSV/table parsing;
- OCR or image extraction from text documents;
- generalized Document abstraction/framework;
- replacement provider/orchestration/persistence architecture;
- any future roadmap slice.

## Implementation checkpoint

Starting checkpoint: `main @ db303747fbf654ebe730aa41429bb75abf180306`

Current checkpoint: `main @ 0a2934f1556e3c50f3154ec2ce3de5b79debbdcf`

## Implementation summary

Implemented:
- `.txt` file detection as `text/plain`;
- `InputSourceKind.Text` and bounded `PreparedTextInput`;
- strict UTF-8 decoding with BOM handling;
- 1 MiB text-file bound and 1,000,000-character prepared-text bound;
- source-neutral structured extraction from text using the existing structured-output provider path;
- text provenance, SHA-256 source fingerprinting, durable Management processing and restart reload;
- deterministic failure handling for empty and invalid-UTF-8 text;
- focused unit/integration coverage;
- Example Host text fixture plus continued unsupported binary failure isolation.

## Verification gate

Status: VERIFICATION PENDING

Required verification:
- focused text input selection/preparation tests;
- text extraction/structured candidate tests including malformed output and cancellation;
- Management/batch processing coverage for text;
- Example Host manual verification;
- full `Hive.Tests` suite.

The failed verification was remediated. `Submission_ContinuesAfterUnsupportedItem` now uses an actually unsupported `.bin` fixture, while the durable batch matcher now uses source fingerprint plus file name for prepared image/text inputs instead of the original submission `ItemIndex`. This preserves correct matching when one source input expands into multiple durable items.