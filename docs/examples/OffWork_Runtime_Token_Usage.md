# Off-Work Provider Completion — Slice 3 Runtime Token Usage Foundation

The Slice 3 Example Host scenario demonstrates the provider-reported runtime usage evidence boundary:

**Example to run:** `Providers / Runtime / Token Usage Foundation` — Hive.Example.WinForms

The deterministic loopback provider returns:

- input tokens: 120
- output tokens: 45
- total tokens: 165
- cached input tokens: 20
- reasoning tokens: 10
- additional provider-reported token count: 3

Hive preserves the reported dimensions without adding cached-input or reasoning tokens to the provider's input/output/total counts. Missing provider usage is represented as `Unknown`; this slice does not create local tokenizer estimation.

The example runs through the normal `AgentExecutionService` path and receives `ExecutionTokenUsage` on `AgentExecutionResult`. The usage observation is persisted inside the terminal execution event in the existing append-only Execution event stream.

The example is deterministic and uses a local loopback HTTP fixture. It does not require a vendor credential, external provider account, live provider network call, billing API, or reporting subsystem.

Phase 1.30 remains the later owner of usage aggregation, metrics, budgets, OpenTelemetry, quota/rate-limit handling, and reporting.
