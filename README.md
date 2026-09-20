# Hive

Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, and governing multi-agent systems.

It is built on the Microsoft Agent Framework (MAF) wherever MAF already owns the underlying behavior. Hive adds the semantics and infrastructure around that foundation: provider control, execution-target selection, persistent resources and state, agent identity and lifecycle, authorization, learning governance, Hive membership, cognitive state, observability, and other higher-level system concerns.

The document/image data-entry pipeline described below is **a V1 validation workload**, not the definition or long-term purpose of Hive.

## V1 validation workload

The first concrete workload exercises Hive as a fixed multi-agent pipeline for automating data entry from documents/images into a business application:

```text
Document / Image
      ↓
Ingestion / Parsing
      ↓
Capability-aware execution target selection
      ↓
Extraction / Validation
      ↓
Human-approved business-app write
```

This workload is intentionally narrow so the underlying agent, provider, persistence, execution, management, authorization, and recovery architecture can be built and validated without prematurely solving every possible Hive use case.

The V1 host is WinForms. Application logic is UI-agnostic; Hive is designed as a library first.

## Core design

- **C# / .NET 10 only**
- **Microsoft Agent Framework (MAF)** for agent execution and orchestration
- **SQL Server** for Hive's own database; LocalDB for development
- **SQL Server VECTOR / VECTOR_DISTANCE** behind `IVectorStore` when vector storage is needed
- **One OpenAI-compatible provider adapter** for all compatible providers and local servers
- **Execution Target** is the capability-bearing unit: Provider + ProviderAccount + Model
- Capability state is explicitly `Supported`, `Unsupported`, or `Unknown`
- Provider credentials are encrypted at rest and redacted from diagnostics
- Append-only events with snapshots and transactional outbox
- Immutable configuration snapshots per running execution
- Terminal execution outcomes are protected from late provider responses
- The LLM proposes; Hive decides

## Repository structure

```text
Hive/
├── docs/
│   ├── architecture.md
│   ├── roadmap.md
│   ├── Hive_Current_Status.md
│   └── Hive_Active_Work.md
└── README.md
```

The solution/projects will be created by Phase 0.1 according to `docs/roadmap.md`.

## Documentation rules

`docs/architecture.md` is the architectural source of truth. Update it before structural code changes.

`docs/Hive_Current_Status.md` contains the current implementation/status only.

`docs/Hive_Active_Work.md` contains only the current implementation slice and its verification notes.

`docs/roadmap.md` is the granular implementation plan. It is not a second source of status.

## Roadmap

Phase 0 establishes the solution and persistence/test foundations.

Phase 1 builds the first concrete multi-agent validation workload, provider platform, management UI, crash/resume behavior, capability-aware selection, budgets, and observability.

Later phases cover persistence hardening, death/postmortem/reincarnation, Hive membership and coordination, governance patterns, cognitive safety, multi-tenancy/scale, tooling, and operations.

See [`docs/architecture.md`](docs/architecture.md) and [`docs/roadmap.md`](docs/roadmap.md).

## Project status

**Phase 0 — not yet implemented.**

The repository is currently documentation/scaffolding only. No implementation or test claims are made until the corresponding slices are completed and recorded in `docs/Hive_Current_Status.md`.

## License

License not selected yet.
