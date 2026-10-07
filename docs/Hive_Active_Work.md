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

## Verification gate

Status: VERIFICATION PENDING

Required verification:
- focused text input selection/preparation tests;
- text extraction/structured candidate tests including malformed output and cancellation;
- Management/batch processing coverage for text;
- Example Host manual verification;
- full `Hive.Tests` suite.

No verification result is claimed until the developer runs the required checks.