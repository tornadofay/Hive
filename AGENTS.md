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
30. Base Agent mechanisms may include Objectives, memory infrastructure, Question/Answer transport, Patience / Understanding Gate, bounded Simulation infrastructure, delegation, and Hive sponsorship without making the Agent cognitively adaptive.
31. An Objective is an explicit work target; a Cognitive Goal is an adaptive cognitive construct that may be formed, revised, prioritized, or abandoned.
32. Base Simulation infrastructure must distinguish predicted/hypothetical results from actual experience; Cognitive Dreams build adaptive selection and interpretation on top of it.
33. The base Question protocol may transport and await required information; CognitiveAgents add autonomous question generation, selection, specialization, and interpretation.
34. Base Agent Patience / Understanding Gates must enforce required information/confirmation before consequential work and must not claim that all possible context must be understood.
35. A base Agent may explicitly sponsor or create a Hive without changing its own type. A member Agent inside a Hive normally requests new specialists through the parent Hive.
36. Hive population authority controls creation/reuse of member Agents; recursive child-Hive creation is not the default behavior of Hive members.
37. A Swarm is a temporary active work session over a persistent Hive; ending a Swarm may return the Hive to Dormant without deleting its members or state.
38. Agent identity and persistent cognitive state are distinct from Runtime/RuntimeInstance/Incarnation lifetime.
39. Agent death means complete termination of the current runtime/incarnation; it does not delete the Agent or its persistent cognitive state.
40. Dream processing may operate against persisted cognitive state while no Agent runtime is active, including while the host application is shut down.
41. Dream outputs are simulations, predictions, hypotheses, or candidate analyses and must never be recorded as actual experience or observation.
42. Human edits made while an Agent is inactive are durable cognitive-state changes and must be versioned, authorized, and incorporated by the next valid wake/reincarnation path.
43. Questions are first-class, provenance-bearing, specialty-aware cognitive objects; semantically duplicate questions should be avoided when existing evidence is sufficient.
44. Cognitive strategies are replaceable and provider-neutral.
45. A cognitive strategy may act deterministically and may decide not to call an LLM.
46. Reasoning Requirement and concrete Execution Planning are separate concerns.
47. Running executions consume immutable effective-configuration snapshots.
48. Terminal execution outcomes cannot be overwritten by late provider completion.
49. Runtime mutable state is isolated by explicit ownership.
50. Cognitive work must remain bounded by applicable time, work, recursion, retrieval, and model-usage limits.
51. Model output, retrieved content, memory, and observations are evidence/input, never authorization.

## Resources, providers, and security

52. Resource scope and ownership are explicit.
53. Shared resources require explicit scope and authorization.
54. Private runtime memory must not leak across runtime instances.
55. Unknown future resource types remain representable through the generic resource inventory.
56. Capability support is explicitly Supported / Unsupported / Unknown.
57. Capability requirements are explicitly Required / Preferred / Optional / Forbidden.
58. Quota, rate limits, health, capacity, cost, and capability are separate state dimensions.
59. Provider credentials are encrypted at rest and redacted everywhere else.
60. Network-provider automated tests use fakes/local infrastructure, never real vendor accounts.
61. Authorization is enforced in code, not by prompt text.
62. Configuration must actually drive the behavior it configures.

## Host integration and intervention

63. V1 host integration is limited to the actual business application boundary proved by the integration decision gate.
64. Do not build a generic UI/object-discovery framework before a second differently-shaped real host requires it.
65. Prefer native/bound host data sources over visible-text scraping.
66. Host discovery is bounded, cancellation-aware, read-oriented, and never grants tool permission.
67. Approval is one intervention type; V1 only needs Approve/Reject for the business-app write.
68. Intervention requests capture target state/version and reject stale application.

## Code quality, testing, and workflow

69. No empty catch blocks.
70. Use one JSON serialization stack.
71. Do not duplicate the same computation in multiple layers.
72. Prefer structured error classification over string matching.
73. Timeout and budget settings are explicit and validated.
74. Repeated lookup paths use real indexes.
75. Every implementation slice requires the relevant unit, boundary/edge, integration, recovery/concurrency, security, UI, and public-example verification before completion.
76. System/end-to-end tests are required where unit tests cannot prove an important cross-boundary contract.
77. Edge-case coverage means all known and contract-relevant cases; do not claim exhaustive coverage of every conceivable future failure.
78. Do not claim tests, builds, or verification that were not actually run.
79. Update `docs/architecture.md` before structural code changes.
80. Complete the active slice fully before implementing future slices or future generations.

## Documentation source of truth

81. `docs/architecture.md` is the architectural source of truth.
82. `docs/Hive_Current_Status.md` is the only status record.
83. `docs/Hive_Active_Work.md` is the only current implementation-slice tracker.
84. `docs/roadmap.md` is the ordered implementation plan and must match the architecture's phase order.
85. Keep these source-of-truth files synchronized.
86. The README is explanatory and must not introduce architecture that conflicts with the source-of-truth files.
87. Examples and tests are developed alongside the feature they demonstrate, not postponed to a final phase.
88. Complete public examples should include copyable API usage and expected result where meaningful.
89. Do not silently broaden a slice because a later phase is mentioned in the architecture.
90. When adding or removing an AGENTS rule, renumber the whole list and verify that there are no duplicate or skipped numbers.
