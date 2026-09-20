# Hive

Hive is a C# / .NET 10 multi-agent automation platform for turning documents and images into governed writes to a user's business application.

Hive is built on the Microsoft Agent Framework (MAF) where MAF already owns the underlying behavior. Hive adds the provider control plane, document ingestion, resource ownership, management surface, agent identity, authorization, learning governance, and later cognitive/governance semantics.

## V1

The first target is a fixed multi-agent pipeline:

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

The V1 host is WinForms. The application logic is UI-agnostic; Hive is designed as a library first.

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

Phase 1 builds the first real document-to-business-app multi-agent pipeline, provider platform, management UI, crash/resume behavior, capability-aware selection, budgets, and observability.

Later phases cover persistence hardening, death/postmortem/reincarnation, Hive membership and coordination, governance patterns, cognitive safety, multi-tenancy/scale, tooling, and operations.

See [`docs/architecture.md`](docs/architecture.md) and [`docs/roadmap.md`](docs/roadmap.md).

## Project status

**Phase 0 — not yet implemented.**

The repository is currently documentation/scaffolding only. No implementation or test claims are made until the corresponding slices are completed and recorded in `docs/Hive_Current_Status.md`.

## License

License not selected yet.
