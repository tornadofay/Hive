# Hive — Architecture (source of truth)



Last updated: 2026-10-10 (rev 61 — bounded Base-Agent problem-solving boundary review)



Status lives only in `Hive_Current_Status.md`. Current work slice lives only in `Hive_Active_Work.md`. The ordered implementation plan lives in `roadmap.md`.



## How this architecture is organized



`docs/architecture.md` is the architectural index and the source for global architectural intent, dependency direction, engineering invariants, and non-negotiable rules. Detailed domain architecture is split under `docs/architecture/` to keep the material usable by agents and humans without changing its authority.



All files under `docs/architecture/` are part of the same architecture source of truth. When a task crosses a domain boundary, read this file plus every relevant detailed architecture document listed below.



| Detail document | Primary scope |

|---|---|

| [`architecture/foundations.md`](architecture/foundations.md) | identity/resource foundation, persistence bootstrap, test harness, WinForms UI foundation |

| [`architecture/execution-and-persistence.md`](architecture/execution-and-persistence.md) | MAF boundary, provider platform, resources, execution planning, human intervention, events/outbox |

| [`architecture/agents-and-hives.md`](architecture/agents-and-hives.md) | Agent/Hive hierarchy, runtime mechanisms, cognitive generations and lifecycle |

| [`architecture/v1-host-and-management.md`](architecture/v1-host-and-management.md) | V1 Workspace/Management, Example Host, Settings, and host composition |
| [`architecture/v1-business-app-integration.md`](architecture/v1-business-app-integration.md) | V1 host adapters, semantic control/data surfaces, business operations, write receipts, and post-write review |

| [`architecture/cognitive-resources-and-portability.md`](architecture/cognitive-resources-and-portability.md) | cognitive resource families and configuration portability |



### Read rule



- For ordinary changes: read this file and the detailed architecture document(s) covering the affected boundary.

- For changes crossing projects, public APIs, persistence, orchestration, lifecycle, security, or durable state: read this file and all relevant detailed architecture documents before coding.

- Do not create a second architecture source. New detailed architectural material belongs in the appropriate file under `docs/architecture/`.
- Global architectural invariants in this index apply across all domains. A detail document owns only the contracts within its declared scope and must not contradict those global invariants.



## 0. Purpose & Scope

Hive is a general-purpose C# / .NET 10 platform for building, running, coordinating, observing, governing, and evolving multi-agent systems.

### V1 forcing function

V1 has a real forcing function: automate data entry from supported input sources into the user's existing business application.

That workflow is **not Hive's definition** and not a product-specific architecture. It is the first real deliverable that determines implementation order and proves that the general platform can solve a concrete problem.

A single input submission may produce one or multiple WorkItems. A WorkItem is the durable unit of user-visible work and represents one logical business operation when a business operation is required. A logical operation may contain a parent record and child-row collection and may require multiple executions or steps. Related WorkItems may be grouped operationally as a submission or batch without replacing their independent identity, lifecycle, provenance, authorization, or any applicable operation receipt or Review state.

The intended common V1 pipeline is:

```
Input submission
      ↓
input-specific preparation / interpretation
      ↓
structured candidate
      ↓
validation
      ↓
governed business-app proposal
      ↓
authorization / approval when required
      ↓
business-app write
      ↓
durable operation attempt / receipt
      ↓
policy-governed verification / review / result
```

Image input may use a vision-capable execution target. Structured input such as a spreadsheet may already contain structured values and can therefore bypass vision or other interpretation steps that are unnecessary for that source. Input preparation and interpretation are capabilities of the general platform, not Hive's permanent scope.

Longer-term capabilities such as persistent individual cognition, offline Dream processing, structured Questions, collective cognition, generic host integration, broader governance, and additional host surfaces remain part of Hive's architecture, but they must not become prerequisites for the V1 pipeline.

### Primary design goals

1. A reusable Agent/Hive platform rather than a workflow-specific application.
2. A base `Agent` and base `Hive` that are complete and useful on their own.
3. Later generations such as `CognitiveAgent : Agent` and `CognitiveHive : Hive` that add behavior without changing the base contracts.
4. Provider-neutral, capability-aware execution planning.
5. Host integration uses neutral public contracts with concrete adapters; V1 proves WinForms without coupling Hive to any private or host-specific control/data framework.
6. A reusable Workspace/control surface over authoritative Hive state, approval, and post-write review.
7. Production-oriented automated tests for normal paths, edge cases, concurrency, recovery, persistence, and security.
8. Microsoft Agent Framework (MAF) wherever MAF already owns the required mechanism.
9. CognitiveAgents learn from evaluated real outcomes and bounded simulated evidence: they can recover from mistakes, reinforce and optimize successes, stress-test apparently successful methods, and adapt future strategy without confusing simulation with experience.
10. First-class cognitive semantics require explicit contracts, ownership, provenance, lifecycle, and persistence meaning; they may share implementation mechanisms when responsibilities fit, or use separate components/subsystems when a real boundary justifies separation.
11. Normal Base Agents can use bounded reasoning, alternative attempts, progress assessment, challenge, and verification without acquiring CognitiveAgent learning semantics.

### Logical ownership hierarchy

```
Tenant
  └── Users / Principals
       └── Workspaces
            ├── Agents
            │    ├── Runtime Instances
            │    │    └── Executions
            │    └── Resources
            └── Hives
                 └── Members (Agents and/or future Hive generations)
```

Not every deployment must use every level.

### Provider model discovery architecture

Provider/model discovery is a provider-platform evidence boundary. It is provider-neutral and must describe the model information the provider actually exposes without confusing missing metadata with lack of capability.

A discovered model is represented by a normalized model metadata profile within the discovery snapshot. The profile is not a new durable `Model` resource and is not itself an execution target. The durable execution-resource graph remains:

```
Provider
   ↓
ProviderAccount
   ↓
ExecutionTarget
```

The normalized model profile covers these semantic areas when reported by the provider. The profile describes observed model/deployment metadata; it is not copied wholesale into durable ExecutionTarget configuration.

- **Identity and descriptive metadata:** model/deployment identity, ownership, family/type, version/creation metadata, and other descriptive model information that is useful for selection or presentation.
- **Input modalities:** all provider-reported model input modalities, including text, image, audio, video, and other provider-defined inputs.
- **Output modalities:** all provider-reported model output modalities, including text, image, audio, embeddings, video, and other provider-defined outputs.
- **Capabilities:** provider-reported features such as tool calling, structured output, reasoning, thinking, and other machine-readable capabilities. Reasoning and thinking remain distinct concepts; thinking may also carry provider-reported levels/options/defaults.
- **Limits:** model-scoped limits such as context window, maximum input tokens, maximum output tokens, and other provider-reported model constraints. Provider/account rate limits remain separate operational metadata.
- **Pricing/economics:** provider-reported pricing entries with their billing unit, including input/output pricing and separately reported reasoning/thinking, cached, image/audio, request, or other billable units. Free pricing evidence means the observed provider pricing for the relevant billable unit(s) is explicitly zero or otherwise explicitly identified by the provider as free. Free pricing evidence does not guarantee zero user/account cost under every provider plan, routing arrangement, quota, or policy; missing pricing is not interpreted as free.
- **Operational metadata:** availability, health, and discovery/observation timing and freshness.

The discovery boundary must ingest all useful machine-readable model metadata exposed by the provider contract that can be safely attributed to the model, subject to Hive's existing security, secret-redaction, response-size, validation, cancellation, and bounded-processing constraints. Hive normalizes common semantic fields into the provider-neutral profile and preserves additional provider-specific model metadata/evidence in a structured, bounded, extensible form rather than silently discarding fields that are not yet modeled by Hive. Provider-specific extensions are non-authoritative discovery evidence: they must not contain secrets, must not bypass normalized capability/selection/authorization policy, and must not become an implicit contract for consumers that do not explicitly understand the extension.

Absence of model metadata means **not reported / Unknown**, not zero capabilities, zero modalities, unsupported features, unlimited capacity, or free pricing. An enumerated model with no recognized normalized capability evidence remains a valid discovered model; the absence of evidence is a metadata-state distinction, not a statement that the model has no capabilities.

Discovery metadata is operational evidence and may be cached with freshness rules. It is not configuration, authorization, or a replacement for configured `ExecutionTarget` state. A matching discovered model profile may be consumed by UI, Management, selection, and automatic reconciliation, but those consumers must treat the profile as observed evidence rather than as durable target configuration. Later phases may choose to persist a reusable model catalog if a durable cross-restart catalog requirement is demonstrated; that would be a separate architecture decision and must not be introduced implicitly by provider discovery.

Within the OpenAI-compatible provider boundary, HTTP transport and model-catalog parsing are separate responsibilities. `OpenAICompatibleProviderAdapter` owns transport: request construction, bounded request/response body handling, credential presentation, timeout/cancellation, status mapping, and response reading. Model-catalog parsing is owned by `Hive.Providers.OpenAICompatible.ModelCatalog`, where each provider catalog shape has one `IModelCatalogParser` implementation and `ModelCatalogParserRegistry` resolves the parser for a format. Shared JSON-to-contract normalization for capabilities, modalities, thinking, limits, pricing, health, and bounded/redacted extension data has one owner in `ModelMetadataNormalizer`. Supporting another provider catalog shape therefore means adding a parser file and a registry entry rather than extending a dispatch switch inside the adapter. This separation is structural only: it must not change parsed catalog content, error codes, error categories, or normalization outcomes.



## Engineering Standards

Hive is built for production real-world applications. The default coding standard is clean, warning-free, lightweight, and performance-conscious without sacrificing correctness or maintainability.

### Correctness and contracts

- Nullable reference types remain enabled. Nullability mismatches are fixed at the contract boundary; they are not suppressed.
- Affected projects must compile with zero errors and zero new warnings before a slice is considered complete.
- Public APIs expose only required consumer contracts and avoid leaking framework/vendor implementation types.
- One authoritative implementation owns each validation, state transition, serialization rule, calculation, or policy decision. Use one JSON serialization stack across Hive; do not introduce parallel serializers with divergent defaults for the same contracts. Configuration must drive the behavior it claims to configure, and tests must prove that relationship.
- Failures use structured/typed classification with useful context. Boundary catches must not silently swallow the underlying cause.
- A public `Error` carries a stable Hive-authored code, category, and message. Raw exception `Message` text is diagnostic context and must not be forwarded into a public error, because Hive-authored control-flow exceptions can interpolate document, provider, or environment-supplied values. `Hive.Persistence` establishes this through `HivePersistenceError`, `Hive.Core/Input` establishes it through `InputPreparationFailureCatalog`, and an unregistered failure code resolves to a category-appropriate generic message rather than to exception detail.

### Performance

- Prefer immutable cached derived state for stable data such as theme definitions, parsed configuration, capability maps, or other repeatedly requested values.
- Avoid unnecessary allocations, repeated parsing/reflection, repeated registry/file/database/network access, and avoidable LINQ/delegate overhead in hot paths.
- Avoid speculative micro-optimization. Optimize measured or contractually important costs such as allocation rate, I/O, UI responsiveness, concurrency, and repeated lookups.
- Avoid hidden I/O or expensive work in property getters, formatting methods, or control rendering paths.
- Keep asynchronous operations cancellation-aware. Do not use sync-over-async, arbitrary sleeps, or Task.Run to mask blocking design. Use ValueTask only where the actual call pattern benefits from its lower-allocation semantics.
- Dispose owned resources deterministically, including streams, database objects, timers, GDI/images, and WinForms controls.

### WinForms

- UI handlers must remain responsive; database, network, filesystem, and other blocking work must not run synchronously on the UI thread.
- Theme and control updates should avoid unnecessary tree traversals, layout passes, repainting, and object creation.
- Every `async void` handler must contain its failures. A WinForms event handler may be `async void`, but an exception escaping it becomes an unhandled UI-thread exception and terminates the process. Route failures to `HiveUiErrorReporter` so they surface through `HiveMessageBox` and the Output panel. When an awaited call already reports through `HiveCrudPage.OperationFailed` or an equivalent operation boundary, the handler guard covers only what happens outside that boundary, and the reporter itself must remain a contained observer boundary.
- Hive-specific controls exist only for a real Hive consumer contract, behavior, or styling need. Native WinForms controls remain preferred where they already satisfy the requirement.

### Persistence

- Use parameterized SQL and explicit transaction boundaries where required.
- Repeated lookup paths require appropriate indexes.
- Avoid N+1 queries and hidden database work from property accessors or UI formatting.
- Keep connection/command/reader lifetimes bounded and disposable.

### Tests

- Test code follows the same production-quality standards as runtime code.
- Tests are deterministic, isolated, concurrency-safe, lightweight, and repeatable.
- No arbitrary sleeps, real vendor accounts, hidden environment variables, or accidental machine state.
- Test doubles remain test-only unless a production contract later requires a reusable fake.
- Tests prove normal, invalid, boundary, cancellation, concurrency, recovery, and security behavior where applicable.
- Core contract test files explicitly import Hive.Core and Xunit alongside any additional required namespaces.



## Testing & Production Readiness

Every implementation slice has a completion gate.

Coverage must include, as applicable:

- deterministic unit tests;
- invalid and boundary cases;
- persistence/MAF/HTTP/WinForms contract tests;
- concurrency and cancellation;
- stale-state and lifecycle races;
- recovery/crash behavior;
- authorization/scope/credential security;
- manual developer verification of UI behavior where applicable; no separate UI-automation framework is required by the architecture.

Network-provider tests use fakes/local infrastructure and never real vendor accounts.

No verification claim is valid unless the test or manual verification was actually performed.

"All edge cases" means all known and contract-relevant cases derived from the contract and implementation boundary; it does not claim to exhaust every conceivable future failure.

---



## Core Solution Layout

```
Hive.Core
Hive.Agents
Hive.Persistence
Hive.Coordination
Hive.Tools
Hive.Providers.OpenAICompatible
Hive.Management
Hive.Host.WinForms
Hive.Host.WinForms.UI
Hive.Example.WinForms
Hive.Tests
```

Reference direction ( `A → B` means project A references project B):

```
Hive.Agents ───────────────────────────────→ Hive.Core
Hive.Persistence ─────────────────────────→ Hive.Core
Hive.Tools ───────────────────────────────→ Hive.Core
Hive.Providers.OpenAICompatible ───────────→ Hive.Core

Hive.Coordination ────────────────────────→ Hive.Core
                  ├──────────────────────→ Hive.Agents
                  ├──────────────────────→ Hive.Persistence
                  └──────────────────────→ Hive.Providers.OpenAICompatible

Hive.Management ─────────────────────────→ Hive.Core
                 ├───────────────────────→ Hive.Agents
                 ├───────────────────────→ Hive.Persistence
                 ├───────────────────────→ Hive.Coordination
                 └───────────────────────→ Hive.Tools

Hive.Host.WinForms ──────────────────────→ Hive.Management
                   ├─────────────────────→ Hive.Persistence
                   ├─────────────────────→ Hive.Providers.OpenAICompatible
                   └─────────────────────→ Hive.Host.WinForms.UI

Hive.Host.WinForms.UI ───────────────────→ Hive.Core

Hive.Example.WinForms ───────────────────→ Hive.Host.WinForms
                      ├──────────────────→ Hive.Host.WinForms.UI
                      └──────────────────→ public platform contracts
```

`Hive.Tests` references the projects and test dependencies it exercises. The graph above summarizes production project dependencies rather than every project-file reference.

`Hive.Coordination` is the execution-composition boundary. It may reference `Hive.Agents`, `Hive.Persistence`, approved provider adapters, and Microsoft Agent Framework to assemble one execution path. It must not own SQL schema/persistence implementation, provider transport implementation, or a second orchestration engine.

`Hive.Management` may consume Coordination execution services in later application-facing slices. Host.WinForms never bypasses Hive.Management.

`Hive.Example.WinForms` is created during Phase 0 and remains a first-class developer-facing project; its test tools are not a substitute for `Hive.Tests`.

---



## Non-Negotiable Architectural Rules

This section contains only cross-cutting invariants. Detailed domain contracts belong to the owning architecture document listed below; those documents retain the full semantics, edge cases, and phase-specific boundaries. Do not create a competing second definition here.

1. **Product scope:** Hive is a general-purpose Agent/Hive platform. The V1 business-data-entry workflow determines implementation order; it is not Hive's permanent definition.
2. **MAF-first execution:** use Microsoft Agent Framework wherever it owns the required execution/orchestration mechanism. Hive must not create a competing workflow engine.
3. **Dependency and ownership direction:** Core stays dependency-light and host/provider-neutral; Persistence owns database implementation; Management owns application operations and authorization; presentation projects consume those boundaries; the Example Host is a consumer, never a platform dependency.
4. **Additive generations:** Base Agent and Base Hive remain useful independently. CognitiveAgent and CognitiveHive are optional descendants selected explicitly at creation. Descendants do not redefine ancestor contracts, and cognitive features must not become hidden prerequisites for V1 or the base generations.
5. **Host-state boundary:** the host application owns its business state and business rules. Hive's database is separate and is never a gateway to the host database. Host integration uses neutral public contracts and bounded adapters; broader cross-host generalization waits for evidence from another materially different host.
6. **Untrusted model boundary:** model output, plans, critiques, confidence, discovery metadata, and UI visibility are not authorization or proof. Authorization and consequential-operation policy are enforced by code; discovery and inspection never grant action permission.
7. **Provider evidence semantics:** capability state is `Supported` / `Unsupported` / `Unknown`; capability requirements are separate policy inputs. Capability, modality, limits, cost, quota, rate, health, availability, and capacity remain distinct. Missing evidence must not be silently treated as support, unlimited capacity, health, or free pricing.
8. **Secret handling:** credentials remain behind the Secret Store/bootstrap-secret boundaries, are protected at rest where specified, and are excluded from public contracts and diagnostics. Migration re-protects secrets for the destination rather than copying backend-specific ciphertext.
9. **Execution isolation:** running work uses immutable effective configuration snapshots where configuration can affect it. Ownership and scope are explicit, and late provider results cannot overwrite terminal execution state.
10. **Durable-state consistency:** state-changing persistence follows the owning event/snapshot/outbox contract. Events are append-oriented, snapshots support recovery, and the triggering durable change plus its outbox work commit atomically where that contract applies.
11. **Consequential host operations:** proposals, authorization/approval, host execution, durable operation receipts/reconciliation, and post-write Review are distinct boundaries. Review assesses resulting host state; it does not substitute for approval or silently turn Hive into a mirror of host data.
12. **Persistence-profile parity:** SQL Server and Embedded are deployment profiles over one logical Hive resource/ownership model. Higher-level behavior consumes shared contracts rather than duplicating features per backend. Vector indexes are derived/rebuildable retrieval data, not an alternative authoritative resource model.
13. **Cognitive evidence integrity:** technical execution status is not itself cognitive Success/Mistake. Outcome evaluations preserve success criteria, observations, provenance, and attribution; actual, simulated, predicted, counterfactual, human-corrected, and external evidence remain distinguishable.
14. **Bounded Base-Agent reasoning:** task-local retries, alternative approaches, challenge, progress assessment, and verification remain bounded by policy, budgets, cancellation, and scope. Model self-assessment is not authoritative; Base-Agent task solving does not silently persist learned strategy.
15. **Execution-target preferences:** favorites are scoped candidate preferences, not authorization, capability, lifecycle state, or ranking. They do not imply fallback to non-favorites when a non-empty favorite pool has no qualifying target.
16. **Engineering consistency:** public contracts remain neutral and minimal; errors are structured; catches do not hide causes; the same computation or policy is not independently reimplemented in multiple layers; configured behavior is real and tested; resource lifetimes and repeated lookup performance are handled deliberately.
17. **Verification and examples:** tests cover contract-relevant normal, invalid, boundary, failure, cancellation, concurrency, recovery, and security behavior. Provider tests use fakes/local infrastructure, not real vendor accounts. Meaningful public capabilities have Example Host scenarios using the public APIs and enforcement boundaries. Report only verification that actually ran.
18. **Repository authority:** `AGENTS.md` owns agent workflow and authorization rules; `Hive_Active_Work.md` owns the current implementation slice and verification gate; `Hive_Current_Status.md` owns status; `roadmap.md` defines order but never authorizes work. A future roadmap concept does not authorize implementation or become an implicit prerequisite of an earlier slice.

### Domain-owned architectural contracts

The following documents are authoritative for their domain details. This index states their boundary but does not repeat their detailed rules.

| Domain | Authoritative detail |
|---|---|
| Identity, scope, persistence bootstrap, SQL Server/Embedded foundation and parity, test harness, UI foundation | [`architecture/foundations.md`](architecture/foundations.md) |
| MAF integration, providers/discovery, capability and target selection, resource persistence, budgets, verification evidence, events/snapshots/outbox, receipts, and Review handoffs | [`architecture/execution-and-persistence.md`](architecture/execution-and-persistence.md) |
| Agent/Hive hierarchy, Base-Agent work protocols and problem-solving, cognitive lifecycle, outcomes, Dreams, Questions, and governed adaptation | [`architecture/agents-and-hives.md`](architecture/agents-and-hives.md) |
| Workspace, Management, settings, host composition, and Example Host responsibilities | [`architecture/v1-host-and-management.md`](architecture/v1-host-and-management.md) |
| Neutral host-integration contracts, semantic data surfaces, stable identity, authorized operations, receipts, reconciliation, and post-write Review | [`architecture/v1-business-app-integration.md`](architecture/v1-business-app-integration.md) |
| Cognitive resource families, actual-versus-simulated learning evidence, vector representation, and configuration portability | [`architecture/cognitive-resources-and-portability.md`](architecture/cognitive-resources-and-portability.md) |

When a rule's interpretation or implementation detail is domain-specific, update its owning detail document and keep this index to the minimum cross-cutting invariant needed to prevent architectural drift.

---



## Roadmap Summary

- **Phase 0 — Foundations**
- **Phase 1 — Base Agent, Provider Platform, Management UI, and Data-Entry Pipeline (V1)**
- **Phase 2 — Base Hive Membership & Coordination**
- **Phase 3 — Hive Governance Patterns**
- **Phase 4 — CognitiveAgent : Agent**
- **Phase 5 — Cognitive Resources**
- **Phase 6 — CognitiveHive : Hive**
- **Phase 7 — Additional Generic Host Integration**
- **Phase 8 — Multi-Tenancy, Scale, Configuration Portability & Extensibility**
- **Phase 9 — Observability, Operations & Replay**

---



## Deferred Decisions

1. Which authentication provider should Phase 8 support when real multi-user requirements arrive (for example local accounts, Microsoft/Entra, Google, or a company IdP)?
2. Whether a future automated UI-testing tool is warranted after real UI test-maintenance needs appear. This is not required for current development because the developer performs manual testing.

The V1 integration mode is not a deferred decision: Hive explicitly supports both API/service and bounded WinForms UI integration. The first V1 input type is not a deferred decision: it is an image. The V1 architecture supports both the SQL Server persistence profile and the Embedded Persistence Profile without changing the logical resource model. SQL Server remains the primary server/deployment backend; Embedded is the local/desktop deployment profile and is not a second resource model.
