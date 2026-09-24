# Hive — Architecture (source of truth)



Last updated: 2026-09-24 (rev 32 — V1 host integration, business write receipt, and review architecture)



Status lives only in `Hive_Current_Status.md`. Current work slice lives only in `Hive_Active_Work.md`. The ordered implementation plan lives in `roadmap.md`.



## How this architecture is organized



`docs/architecture.md` is the architectural index and the source for global architectural intent, dependency direction, engineering invariants, and non-negotiable rules. Detailed domain architecture is split under `docs/architecture/` to keep the material usable by agents and humans without changing its authority.



All files under `docs/architecture/` are part of the same architecture source of truth. When a task crosses a domain boundary, read this file plus every relevant detailed architecture document listed below.



| Detail document | Primary scope |

|---|---|

| [`architecture/foundations.md`](architecture/foundations.md) | identity/resource foundation, SQL persistence bootstrap, test harness, WinForms UI foundation |

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

V1 has a real forcing function: automate data entry from documents and images into the user's existing business application.

That workflow is **not Hive's definition** and not a product-specific architecture. It is the first real deliverable that determines implementation order and proves that the general platform can solve a concrete problem.

The V1 pipeline is:

```
Document / image
      ↓
parse / rasterize
      ↓
text / vision capability
      ↓
structured extraction
      ↓
validation
      ↓
governed business-app proposal
      ↓
authorization / approval when required
      ↓
business-app write
      ↓
operation receipt
      ↓
policy-governed verification / review / result
```

Vision and image/document extraction are therefore capabilities that emerge from the general platform. They are not Hive's permanent scope.

Longer-term capabilities such as persistent individual cognition, offline Dream processing, structured Questions, collective cognition, generic host integration, broader governance, and additional host surfaces remain part of Hive's architecture, but they must not become prerequisites for the V1 pipeline.

### Primary design goals

1. A reusable Agent/Hive platform rather than a workflow-specific application.
2. A base `Agent` and base `Hive` that are complete and useful on their own.
3. Later generations such as `CognitiveAgent : Agent` and `CognitiveHive : Hive` that add behavior without changing the base contracts.
4. Provider-neutral, capability-aware execution planning.
5. Host integration uses neutral public contracts with concrete adapters; V1 proves WinForms without coupling Hive to HForms, HControls, or another host library.
6. A reusable Workspace/control surface over authoritative Hive state, approval, and post-write review.
7. Production-oriented automated tests for normal paths, edge cases, concurrency, recovery, persistence, and security.
8. Microsoft Agent Framework (MAF) wherever MAF already owns the required mechanism.

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



## Engineering Standards

Hive is built for production real-world applications. The default coding standard is clean, warning-free, lightweight, and performance-conscious without sacrificing correctness or maintainability.

### Correctness and contracts

- Nullable reference types remain enabled. Nullability mismatches are fixed at the contract boundary; they are not suppressed.
- Affected projects must compile with zero errors and zero new warnings before a slice is considered complete.
- Public APIs expose only required consumer contracts and avoid leaking framework/vendor implementation types.
- One authoritative implementation owns each validation, state transition, serialization rule, calculation, or policy decision.
- Failures use structured/typed classification with useful context. Boundary catches must not silently swallow the underlying cause.

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

Reference direction:

```
Hive.Core
   ↑
Agents / Persistence / Tools / Providers
   ↑
Coordination
   ↑
Management
   ↑
Host.WinForms
   ↑
Host.WinForms.UI

Example.WinForms → Host.WinForms + Host.WinForms.UI + public platform contracts
```

`Hive.Coordination` is the execution-composition boundary. It may reference `Hive.Agents`, `Hive.Persistence`, approved provider adapters, and Microsoft Agent Framework to assemble one execution path. It must not own SQL schema/persistence implementation, provider transport implementation, or a second orchestration engine.

`Hive.Management` may consume Coordination execution services in later application-facing slices. Host.WinForms never bypasses Hive.Management.

`Hive.Example.WinForms` is created during Phase 0 and remains a first-class developer-facing project; its test tools are not a substitute for `Hive.Tests`.

---



## Non-Negotiable Architectural Rules

1. Hive is general-purpose, but V1 build order is determined by the real data-entry forcing function.
2. Use MAF where MAF already owns the required mechanism.
3. Never build a second orchestration engine to replace MAF.
4. `Agent` and `Hive` are complete useful base types.
5. `CognitiveAgent` and `CognitiveHive` are optional additive descendants.
6. Concrete type is selected at creation; there is no runtime promotion/demotion.
7. Different generations may coexist without requiring ancestor changes.
8. Host business/domain state remains host-owned.
9. Hive's database is isolated from host business data.
10. Capability state is Supported / Unsupported / Unknown.
11. Capability requirements are Required / Preferred / Optional / Forbidden.
12. Quota, rate, health, capacity, cost, and capability remain separate concerns.
13. Secrets are encrypted at rest and redacted from diagnostics.
14. The LLM is a reasoning/request component, never an authorization authority.
15. Authorization is enforced in code.
16. Running executions use immutable effective configuration snapshots.
17. Terminal execution state is protected from late results.
18. Private runtime state is isolated by explicit ownership.
19. Generic cross-host integration is built only when a second real host requires it; V1 uses neutral host-integration contracts with a concrete WinForms adapter, while broader host technology generalization remains later.
20. Host discovery never grants tool permission.
21. Approval is one intervention action; V1 uses Approve/Reject at the business-app write, while post-write correctness is represented separately through a durable business-operation receipt and Review.
22. State-changing persistence is append-oriented; snapshots are recovery aids.
23. Outbox work is transactional with its triggering durable state change.
24. No empty catch blocks.
25. Use one JSON serialization stack.
26. Do not duplicate the same computation in multiple layers.
27. Prefer structured error classification over string matching.
28. Timeout and budget limits are explicit and validated.
29. Configuration must actually drive the behavior it configures and have tests.
30. Repeated lookup paths use real indexes.
31. Network-provider tests never use real vendor accounts.
32. Every implementation slice has the required unit/edge/integration/recovery/security coverage for its boundary, plus manual developer verification of user-facing UI behavior where applicable.
33. Do not claim verification that was not actually performed.
34. Update architecture before structural code changes.
35. Complete the active slice before starting future slices.
36. Do not implement future cognitive generations as hidden prerequisites of the base Agent/Hive.
37. Do not silently broaden the V1 host integration boundary.
38. Do not make document/image extraction the permanent definition of Hive.
39. Do not make persistent cognition a prerequisite for the V1 Agent.
40. Do not force every Agent into a Hive.
41. Do not force every Hive member to use the same Agent generation.
42. Descendant-owned state must not alter the semantics of ancestor-owned state.
43. New generations must remain replaceable/coexistable through stable base contracts.
44. The example host uses the same public APIs and enforcement boundaries as real hosts.
45. Status lives only in `Hive_Current_Status.md`.
46. Current work lives only in `Hive_Active_Work.md`.
47. `roadmap.md` is the ordered implementation plan and must match this architecture's phase order.
48. Phase and slice numbers are ordinal, not version numbers.
49. Do not create documentation that contradicts these source-of-truth boundaries.
50. When adding or removing rules in `AGENTS.md`, renumber the whole list and verify that there are no duplicates or gaps.
51. An Agent's persistent cognitive identity/state may outlive every individual runtime incarnation.
52. Death ends the current Agent runtime/incarnation; it does not delete the Agent or its persistent cognitive state.
53. Dream processing may operate while no Agent runtime is active, and may continue across host-application shutdown/restart through durable state.
54. Dream outputs are simulated/predicted/hypothetical evidence and must never be recorded as actual experience.
55. Human edits made while an Agent is inactive are part of versioned persistent state and must be incorporated by the next valid wake/reincarnation path.
56. Questions are first-class, provenance-bearing, specialization-aware cognitive objects; semantically duplicate questions should be avoided when existing evidence is sufficient.
57. CognitiveAgents remain complete autonomous cognitive entities; CognitiveHive adds collective cognition without owning or replacing member cognition.
58. New lifecycle, Dream, Question, or collective-cognition behavior must remain additive to the generation that owns it and must not become an implicit prerequisite of the base Agent/Hive.
59. Pure V1 host-integration semantic contracts belong in Hive.Core; host-specific adapters belong outside Core.
60. Host adapters translate or execute authorized capabilities; they never become the host application's database, business-logic, or authorization owner.
61. Consequential host row operations require stable row identity; row position is never authoritative identity.
62. A business-operation receipt records the disposition and affected host identities of a consequential host operation; it does not make Hive a mirror of host business state.
63. Approval and post-write Review are distinct lifecycle boundaries; Review uses authoritative host state and policy-governed verification.

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

The V1 integration mode is not a deferred decision: Hive explicitly supports both API/service and bounded WinForms UI integration. The first V1 input type is not a deferred decision: it is an image.