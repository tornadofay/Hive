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

23. Persistent cognitive state belongs to the cognitive generation that owns it and survives the lifetime of any individual runtime incarnation.
24. Agent identity and persistent cognitive state are distinct from Runtime/RuntimeInstance/Incarnation lifetime.
25. Agent death means complete termination of the current runtime/incarnation; it does not delete the Agent or its persistent cognitive state.
26. Dream processing may operate against persisted cognitive state while no Agent runtime is active, including while the host application is shut down.
27. Dream outputs are simulations, predictions, hypotheses, or candidate analyses and must never be recorded as actual experience or observation.
28. Human edits made while an Agent is inactive are durable cognitive-state changes and must be versioned, authorized, and incorporated by the next valid wake/reincarnation path.
29. Questions are first-class, provenance-bearing, specialty-aware cognitive objects; semantically duplicate questions should be avoided when existing evidence is sufficient.
30. Cognitive strategies are replaceable and provider-neutral.
31. A cognitive strategy may act deterministically and may decide not to call an LLM.
32. Reasoning Requirement and concrete Execution Planning are separate concerns.
33. Running executions consume immutable effective-configuration snapshots.
34. Terminal execution outcomes cannot be overwritten by late provider completion.
35. Runtime mutable state is isolated by explicit ownership.
36. Cognitive work must remain bounded by applicable time, work, recursion, retrieval, and model-usage limits.
37. Model output, retrieved content, memory, and observations are evidence/input, never authorization.

## Resources, providers, and security

38. Resource scope and ownership are explicit.
39. Shared resources require explicit scope and authorization.
40. Private runtime memory must not leak across runtime instances.
41. Unknown future resource types remain representable through the generic resource inventory.
42. Capability support is explicitly Supported / Unsupported / Unknown.
43. Capability requirements are explicitly Required / Preferred / Optional / Forbidden.
44. Quota, rate limits, health, capacity, cost, and capability are separate state dimensions.
45. Provider credentials are encrypted at rest and redacted everywhere else.
46. Network-provider automated tests use fakes/local infrastructure, never real vendor accounts.
47. Authorization is enforced in code, not by prompt text.
48. Configuration must actually drive the behavior it configures.

## Host integration and intervention

49. V1 host integration is limited to the actual business application boundary proved by the integration decision gate.
50. Do not build a generic UI/object-discovery framework before a second differently-shaped real host requires it.
51. Prefer native/bound host data sources over visible-text scraping.
52. Host discovery is bounded, cancellation-aware, read-oriented, and never grants tool permission.
53. Approval is one intervention type; V1 only needs Approve/Reject for the business-app write.
54. Intervention requests capture target state/version and reject stale application.

## Code quality, testing, and workflow

55. No empty catch blocks.
56. Use one JSON serialization stack.
57. Do not duplicate the same computation in multiple layers.
58. Prefer structured error classification over string matching.
59. Timeout and budget settings are explicit and validated.
60. Repeated lookup paths use real indexes.
61. Every implementation slice requires the relevant unit, boundary/edge, integration, recovery/concurrency, security, UI, and public-example verification before completion.
62. System/end-to-end tests are required where unit tests cannot prove an important cross-boundary contract.
63. Edge-case coverage means all known and contract-relevant cases; do not claim exhaustive coverage of every conceivable future failure.
64. Do not claim tests, builds, or verification that were not actually run.
65. Update `docs/architecture.md` before structural code changes.
66. Complete the active slice fully before implementing future slices or future generations.

## Documentation source of truth

67. `docs/architecture.md` is the architectural source of truth.
68. `docs/Hive_Current_Status.md` is the only status record.
69. `docs/Hive_Active_Work.md` is the only current implementation-slice tracker.
70. `docs/roadmap.md` is the ordered implementation plan and must match the architecture's phase order.
71. Keep these source-of-truth files synchronized.
72. The README is explanatory and must not introduce architecture that conflicts with the source-of-truth files.
73. Examples and tests are developed alongside the feature they demonstrate, not postponed to a final phase.
74. Complete public examples should include copyable API usage and expected result where meaningful.
75. Do not silently broaden a slice because a later phase is mentioned in the architecture.
76. When adding or removing an AGENTS rule, renumber the whole list and verify that there are no duplicate or skipped numbers.
