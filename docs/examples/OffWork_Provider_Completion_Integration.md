# Off-Work Provider Completion — Slice 4 Example

The Slice 4 Example Host scenario demonstrates the final provider completion integration boundary:

**Example to run:** `Providers / Runtime / Provider Completion Integration & Hardening` — Hive.Example.WinForms

The deterministic scenario:

- populates the existing Management discovery cache with a model profile containing normalized USD token pricing and a pricing variant;
- invokes the real configured-Agent execution path through `Hive.Management`;
- consumes only fresh cached pricing evidence for the exact Provider → ProviderAccount → ExecutionTarget → model context;
- returns provider-reported runtime token usage from a deterministic loopback HTTP fixture;
- proves execution does not invoke provider discovery a second time;
- preserves pricing evidence and runtime usage together in the existing terminal execution event;
- uses no provider credentials, live vendor endpoint, tokenizer, billing API, or reporting subsystem.

The example intentionally does not read the internal event store directly. The public execution result exposes the usage and pricing evidence, while the durable terminal-event persistence remains owned by the existing Hive event boundary.

This example is the handoff demonstration for the completed off-work provider foundation. Phase 1.30 remains responsible for downstream metrics, budgets, OpenTelemetry, quotas, aggregation, reporting, and cost accounting.
