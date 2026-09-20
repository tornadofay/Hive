# Hive engineering rules

These rules apply to human developers and coding agents working in this repository.

## Architecture

1. Hive is a general-purpose multi-agent platform. No single workflow defines the architecture.
2. Use Microsoft Agent Framework whenever it already owns the required behavior.
3. Do not build a second orchestration engine to duplicate MAF.
4. Keep Hive.Core dependency-light and host/provider neutral.
5. WinForms-specific behavior belongs outside Core.
6. Provider transport belongs in provider adapter assemblies.
7. Host business/domain state remains host-owned.
8. Hive's database never becomes an implicit gateway to the host application's business database.

## Runtime and cognition

9. Runtime instances isolate mutable identity, overrides, execution state, shutdown signaling, and private memory ownership.
10. Persistent cognitive state is owned by the runtime instance and updated through the cognitive runtime contracts.
11. Cognitive strategies are replaceable and provider-neutral.
12. A cognitive strategy may act deterministically and may decide not to call an LLM.
13. Reasoning Requirement and Execution Planning are separate concerns.
14. Running executions consume immutable effective configuration snapshots.
15. Terminal outcomes cannot be overwritten by late provider completion.

## Resources

16. Skills, Knowledge, Wiki, Memory, Learning Candidates, capability assignments, and runtime overrides are first-class contracts.
17. Resource scope and ownership are explicit.
18. Shared resources require explicit scope and authorization.
19. Private runtime memory must not leak across runtime instances.
20. Runtime overrides never silently mutate persistent agent configuration.
21. Unknown resource types remain representable through the generic resource inventory.

## Providers and security

22. Capability support is explicit: Supported / Unsupported / Unknown.
23. Capability requirements are explicit: Required / Preferred / Optional / Forbidden.
24. Quota, rate limits, health, capacity, cost, and capability are separate state dimensions.
25. Provider credentials are encrypted at rest.
26. Secrets and sensitive payloads are redacted from logs, traces, diagnostics, test output, and planner explanations.
27. Network-provider automated tests use fakes/local infrastructure, never real vendor accounts.
28. Authorization is enforced in code, not by prompt text.

## Host integration

29. UI Context / Control Adapters provide bounded context, not authority.
30. Prefer native/bound data sources over scraping visible UI text.
31. Support common native host data representations directly; do not force hosts through DataTable or another canonical representation.
32. DataTable/DataView/DataSet are supported representations, not the canonical Hive contract.
33. Object discovery is bounded, cycle-safe, cancellation-aware, and non-executable.
34. Discoverability never implies tool permission.
35. Host-defined semantics may enrich discovery but must not be invented by the model.

## Intervention and portability

36. Approval is one intervention type, not the intervention architecture.
36. Intervention requests capture target state/version and must reject stale application.
37. Competing intervention resolutions are serialized where required.
38. Configuration export/import uses the same authoritative Hive contracts as runtime persistence.
39. Credentials are omitted from normal exports and protected when explicitly included.
41. Import conflicts and compatibility failures are explicit, never silent overwrites.

## Code quality and testing

42. No empty catch blocks.
42. One JSON serialization stack.
43. No duplicated implementation of the same computation.
44. Prefer structured error classification over string matching.
45. Timeout and budget settings are explicit and validated.
46. Configuration must actually drive the behavior it configures.
47. Repeated lookup paths use real indexes.
48. Every production-facing feature needs normal-path and edge-case tests.
49. Concurrency, cancellation, timeout, recovery, stale-state, security, and persistence boundaries require deterministic tests.
50. Do not claim tests or verification that were not actually run.

## Documentation and workflow

51. `docs/architecture.md` is the architectural source of truth.
52. `docs/Hive_Current_Status.md` is the only status record.
53. `docs/Hive_Active_Work.md` is the only current work-slice tracker.
54. `docs/roadmap.md` is the implementation plan, not a status document.
55. Update architecture before structural code changes.
56. Complete the active slice fully without implementing future slices.
57. Example code must use the same public APIs and enforcement boundaries as real hosts.
59. Examples and automated tests are developed alongside the feature they demonstrate; they are not postponed to a final phase.
60. Every public example should have a complete copyable code snippet and expected result.
