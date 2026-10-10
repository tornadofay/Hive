# Deferred Capability — Vector Retrieval

**Status:** Deferred. This document is a design/reassessment note, not an implementation authorization.

**Former roadmap identity:** Phase 1.19 — V1 Vector Retrieval Infrastructure  
**Review point:** Phase 5.1 — Memory Resource Families

## Purpose

Retain the original vector-retrieval requirements without placing an infrastructure-only slice ahead of an approved V1 consumer. Phase 1 already provides Base-Agent work-state and basic memory mechanisms, but that alone does not establish a requirement for vector similarity retrieval. Phase 5.1 should identify concrete memory use cases and their retrieval needs before selecting or implementing vector infrastructure.

## Decision boundary

During Phase 5.1 planning, determine whether Working, Episodic, Semantic, or Procedural memory needs vector-based similarity retrieval and whether structured, exact, lexical, or metadata-based retrieval is sufficient for the intended scenarios.

Do not treat the following as preselected implementation decisions:
- a public `IVectorStore` contract;
- SQL Server native `VECTOR` storage;
- a particular embedded vector index/library;
- a common storage implementation or index format;
- a fixed similarity metric, embedding model, or vector dimensionality.

If vector retrieval is justified, Phase 5.1 should either incorporate the minimum necessary contract and implementation into the memory work or explicitly propose a separate follow-up slice. That decision must be documented before implementation is authorized.

## Candidate requirements retained from the former Phase 1.19 proposal

If vector retrieval is approved, evaluate:
- a replaceable storage/search boundary where it meaningfully preserves portability;
- consistent behavior across supported SQL Server and Embedded persistence profiles without duplicating higher-level feature logic;
- bounded insertion and similarity search;
- explicit vector validity, dimensions, normalization, and similarity semantics;
- deterministic ordering and tie handling;
- deterministic limits, cancellation, and resource bounds;
- ownership, tenant/scope isolation, authorization, and provenance;
- distinction between Actual and Simulated evidence where vectorized records represent evidence;
- whether indexes are durable or derived/rebuildable, and how they recover after restart or profile migration;
- isolation between resources and runtimes;
- regression tests for normal, invalid, boundary, cancellation, persistence/rebuild, and cross-profile behavior, if both profiles are in scope.

The authoritative Hive resource/state records must remain authoritative. A vector index, if introduced, is derived retrieval data and must not become an alternate resource model or authorization source.

## Explicit non-goals for this deferred note

- No Phase 1 implementation or V1 prerequisite.
- No assumption that CognitiveAgent in Phase 4 requires vector retrieval.
- No assumption that the Phase 5 memory system necessarily requires a vector database.
- No automatic activation of implementation work when Phase 5.1 begins.
- No preselection of an external vector database or a third Hive persistence backend.

## Reassessment outcome

Phase 5.1 planning records one of these outcomes: (1) vector retrieval is not needed for the initial memory capabilities; (2) the minimum justified vector support belongs inside the Phase 5.1 memory implementation; or (3) a separately scoped follow-up slice is justified. Each outcome must be evidence/requirement driven. Any implementation remains subject to explicit authorization and the repository's active-work boundary.
