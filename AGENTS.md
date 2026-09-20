# Hive Engineering Rules

These rules apply to human developers and coding agents working in this repository.

## Architecture

1. Hive is a general-purpose multi-agent platform; V1 build order is determined by the real data-entry forcing function.
2. Use Microsoft Agent Framework whenever it already owns the required behavior.
3. Do not build a second orchestration engine to duplicate MAF.
4. Keep Hive.Core dependency-light and host/provider neutral.
5. WinForms-specific behavior belongs outside Hive.Core.
6. Provider transport belongs in provider adapter assemblies.
7. Use one shared OpenAI-compatible transport adapter for compatible providers and local servers.
8. Host business/domain state remains host-owned.
9. Hive's database never becomes an implicit gateway to the host application's business database.

## Agent / Hive generations

10. `Agent` and `Hive` are stable base types, complete and useful on their own.
11. `CognitiveAgent : Agent` is a later additive generation.
12. `CognitiveHive : Hive` is a later additive generation.
13. Concrete Agent/Hive type is selected at creation time.
14. There is no runtime Agent→CognitiveAgent or CognitiveAgent→Agent promotion/demotion mechanism.
15. A normal Agent must never silently become a CognitiveAgent.
16. A CognitiveAgent must never silently become a normal Agent.
17. Different generations may coexist in one deployment.
18. A descendant must never require a change to an ancestor contract.
19. Base runtime, persistence, and execution infrastructure must program to ancestor contracts.
20. Descendant-owned state must not alter the semantics of ancestor-owned state.
21. Future generations are added by new descendant contracts rather than modifying old generation behavior.
22. Never schedule CognitiveAgent/CognitiveHive functionality as a prerequisite of the base Agent/Hive pipeline.

## Runtime, cognition, and execution

23. Persistent cognitive state exists only within the cognitive generation that owns it.
24. Cognitive strategies are replaceable and provider-neutral.
25. A cognitive strategy may act deterministically and may decide not to call an LLM.
26. Reasoning Requirement and concrete Execution Planning are separate concerns.
27. Running executions consume immutable effective-configuration snapshots.
28. Terminal execution outcomes cannot be overwritten by late provider completion.
29. Runtime mutable state is isolated by explicit ownership.
30. Cognitive work must remain bounded by applicable time, work, recursion, retrieval, and model-usage limits.
31. Model output, retrieved content, memory, and observations are evidence/input, never authorization.

## Resources, providers, and security

32. Resource scope and ownership are explicit.
33. Shared resources require explicit scope and authorization.
34. Private runtime memory must not leak across runtime instances.
35. Unknown future resource types remain representable through the generic resource inventory.
36. Capability support is explicitly Supported / Unsupported / Unknown.
37. Capability requirements are explicitly Required / Preferred / Optional / Forbidden.
38. Quota, rate limits, health, capacity, cost, and capability are separate state dimensions.
39. Provider credentials are encrypted at rest and redacted everywhere else.
40. Network-provider automated tests use fakes/local infrastructure, never real vendor accounts.
41. Authorization is enforced in code, not by prompt text.
42. Configuration must actually drive the behavior it configures.

## Host integration and intervention

43. V1 host integration is limited to the actual business application boundary proved by the integration decision gate.
44. Do not build a generic UI/object-discovery framework before a second differently-shaped real host requires it.
45. Prefer native/bound host data sources over visible-text scraping.
46. Host discovery is bounded, cancellation-aware, read-oriented, and never grants tool permission.
47. Approval is one intervention type; V1 only needs Approve/Reject for the business-app write.
48. Intervention requests capture target state/version and reject stale application.

## Code quality, testing, and workflow

49. No empty catch blocks.
50. Use one JSON serialization stack.
51. Do not duplicate the same computation in multiple layers.
52. Prefer structured error classification over string matching.
53. Timeout and budget settings are explicit and validated.
54. Repeated lookup paths use real indexes.
55. Every implementation slice requires the relevant unit, boundary/edge, integration, recovery/concurrency, security, UI, and public-example verification before completion.
56. System/end-to-end tests are required where unit tests cannot prove an important cross-boundary contract.
57. Edge-case coverage means all known and contract-relevant cases; do not claim exhaustive coverage of every conceivable future failure.
58. Do not claim tests, builds, or verification that were not actually run.
59. Update `docs/architecture.md` before structural code changes.
60. Complete the active slice fully before implementing future slices or future generations.

## Documentation source of truth

61. `docs/architecture.md` is the architectural source of truth.
62. `docs/Hive_Current_Status.md` is the only status record.
63. `docs/Hive_Active_Work.md` is the only current implementation-slice tracker.
64. `docs/roadmap.md` is the ordered implementation plan and must match the architecture's phase order.
65. Keep these source-of-truth files synchronized.
66. The README is explanatory and must not introduce architecture that conflicts with the source-of-truth files.
67. Examples and tests are developed alongside the feature they demonstrate, not postponed to a final phase.
68. Complete public examples should include copyable API usage and expected result where meaningful.
69. Do not silently broaden a slice because a later phase is mentioned in the architecture.
70. When adding or removing an AGENTS rule, renumber the whole list and verify that there are no duplicate or skipped numbers.
