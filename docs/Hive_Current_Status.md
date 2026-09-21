# Hive — Current Status

Last updated: 2026-09-21

## Repository state

The initial Phase 0.1 .NET 10 solution/project scaffold is implemented, merged into main, and locally verified by the developer. The current implementation slice is Phase 0.2 common infrastructure, with its first implementation set committed to main and local verification pending.

## Current phase

Phase 0 — Foundations.

**Active slice: 0.2 — Common infrastructure.**

## Architecture decisions now locked

- Phase 0 establishes a shared WinForms UI foundation using ReaLTaiizor as the selected third-party rendering layer behind Hive.Host.WinForms.UI; consuming forms do not reference ReaLTaiizor directly.
- Hive owns its UI contract, theme modes, semantic design tokens, and Hive-specific controls where additional behavior/styling is needed; it does not wrap every WinForms control merely to rename it.
- Hive.Example.WinForms is a first-class permanent developer-facing project from Phase 0, with scalable Category → Subcategory → Example navigation and external dotnet test developer tooling.
- Hive is general-purpose; the real V1 forcing function is automating data entry from documents/images into the existing business application.
- The V1 pipeline is not Hive's permanent definition; it determines implementation order.
- Agent and Hive are stable base types.
- The base Agent may provide reusable Objectives, memory infrastructure, Question/Answer transport, Patience / Understanding Gate, Simulation infrastructure, delegation, and Hive sponsorship without becoming a CognitiveAgent.
- Agent generation is fixed at creation. Authorized creators may request any supported generation, including CognitiveAgent; generation is never inferred or promoted automatically.
- Generation and Hive membership are independent; base Hives may contain CognitiveAgents and base Agents may sponsor Hives.
- Hive sponsorship is not lifecycle ownership. A Hive and its independent members survive sponsor runtime death/retirement/deletion unless explicitly retired.
- Swarm is a non-persistent active subset of Hive members collaborating on a bounded problem; it is not another architectural resource/lifecycle layer.
- Workspace is the human-facing operational surface over Hive.Management. V1 is limited to image submission, WorkItem status/activity, relevant execution/provider status, notifications, and business-app write Approve/Reject. Agent/Hive topology, Swarm views, general LLM mode, and Agentic mode are later phase-gated Workspace extensions.
- V1 WorkItem semantics are fixed: one submitted document is one WorkItem; batches are multiple WorkItems.
- Durable events carry event type and payload schema version with an upcasting compatibility boundary separate from database schema versioning.
- Dream processing is bounded by applicable authorization, quota, cost/token, time, concurrency, retrieval/work, and cancellation policies.
- A base Agent may sponsor a persistent Hive for multi-specialty work; a Swarm is the active work session and a Hive may become Dormant afterward.
- Hive population authority belongs to the Hive during Hive-managed work; member Agents normally request missing specialties rather than recursively creating child Hives.
- CognitiveAgent : Agent and CognitiveHive : Hive are later additive generations.
- Concrete type is selected at creation; there is no runtime promotion/demotion.
- Different Agent/Hive generations can coexist without ancestor contract changes.
- Persistent cognition belongs to the CognitiveAgent generation rather than being a prerequisite of the base Agent.
- Hive uses MAF for execution/orchestration mechanisms MAF already provides.
- One shared OpenAI-compatible provider adapter serves compatible providers/local servers through configuration.
- V1 provider configurations currently targeted: Groq, OpenRouter, Cloudflare, Cerebras, NVIDIA, Google, and local OpenAI-compatible servers.
- V1 business-app integration explicitly supports both API/service and bounded UI integration; they are not mutually exclusive and may be used together per WorkItem or operation.
- The first V1 document/input type is an image.
- V1 WinForms host discovery covers the relevant Form/control hierarchy, including Forms, UserControls, custom/inherited controls, Panels, GroupBoxes, other containers, nested controls, and relevant runtime/data-source context; discovery never grants action authority.
- CognitiveAgent persists its cognition independently of any one runtime incarnation; death ends the incarnation, not the Agent or its durable state.
- Dreams are bounded offline cognitive simulations/analysis that can run with no live Agent runtime, including during host-application downtime; Dream output remains distinct from actual experience.
- Questions are first-class, specialty-aware cognitive objects that can be owned by individual CognitiveAgents and coordinated collectively by CognitiveHive.
- CognitiveHive adds collective cognition without moving or replacing member-level cognition.
- Generic cross-host integration is deferred until a second real host proves the need to generalize V1 patterns.
- V1 human intervention is Approve/Reject at the business-app write; the broader intervention taxonomy is later.
- Developer manual testing is the current UI/application verification approach; no UI-automation framework is required by the architecture.
- Authentication-provider selection is deferred until real multi-user requirements reach Phase 8.
- Tests and examples are developed with each implementation slice; no unperformed verification is claimed.

## Current implementation progress

### Phase 0.2 — Common infrastructure

The first 0.2 implementation set is committed to `Hive.Core`: common technical IDs, typed errors/results, `IClock`, durable event envelope contracts, System.Text.Json serialization, and sequential event-payload upcasting infrastructure. No local 0.2 build/test verification is claimed yet.

## Completed

### Phase 0.1 — Solution & project scaffolding

- Eleven-project .NET 10 solution scaffold created and merged into main.
- Hive.Example.WinForms is the current developer startup project.
- Developer reports a successful full-solution rebuild.
- Developer reports the example application launches successfully with the current placeholder form.
- No automated tests exist in this slice; xUnit/test harness is intentionally deferred to Phase 0.5.

## Not started

- Phase 0.3 Identity, WorkItem & Resource foundation.
- Phase 0.4 Persistence bootstrap.
- Phase 0.5 Test harness.
- Phase 0.6 WinForms UI/UX Foundation.
- Phase 0.7 Example Host Shell.
- Phase 0.8 Example Developer Test Tools.
- Later implementation slices.
- Database schema.
- Automated tests beyond the later test-harness slice.
- WinForms management host.
- Example application features.

The 0.1 scaffold is locally verified; no verification is claimed here beyond the developer-reported result.