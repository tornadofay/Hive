# Hive — Current Status

Last updated: 2026-09-21

## Repository state

Architecture/documentation baseline only. No implementation has started.

## Current phase

Phase 0 — Foundations.

## Architecture decisions now locked

- Hive is general-purpose; the real V1 forcing function is automating data entry from documents/images into the existing business application.
- The V1 pipeline is not Hive's permanent definition; it determines implementation order.
- `Agent` and `Hive` are stable base types.
- The base Agent may provide reusable Objectives, memory infrastructure, Question/Answer transport, Patience / Understanding Gate, Simulation infrastructure, delegation, and Hive sponsorship without becoming a CognitiveAgent.
- Agent generation is fixed at creation. Authorized creators may request any supported generation, including CognitiveAgent; generation is never inferred or promoted automatically.
- Generation and Hive membership are independent; base Hives may contain CognitiveAgents and base Agents may sponsor Hives.
- Hive sponsorship is not lifecycle ownership. A Hive and its independent members survive sponsor runtime death/retirement/deletion unless explicitly retired.
- Swarm is a non-persistent active subset of Hive members collaborating on a bounded problem; it is not another architectural resource/lifecycle layer.
- Workspace is the human-facing operational surface with LLM mode, Agentic mode, topology, activity, Questions, notifications, and approvals. Host form registration is bounded and does not imply automatic Hive creation.
- V1 WorkItem semantics are fixed: one submitted document is one WorkItem; batches are multiple WorkItems.
- Durable events carry event type and payload schema version with an upcasting compatibility boundary separate from database schema versioning.
- Dream processing is bounded by applicable authorization, quota, cost/token, time, concurrency, retrieval/work, and cancellation policies.
- A base Agent may sponsor a persistent Hive for multi-specialty work; a Swarm is the active work session and a Hive may become Dormant afterward.
- Hive population authority belongs to the Hive during Hive-managed work; member Agents normally request missing specialties rather than recursively creating child Hives.
- `CognitiveAgent : Agent` and `CognitiveHive : Hive` are later additive generations.
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
- Developer manual testing is the current UI/application verification approach; no smoke-test or UI-automation framework is required by the architecture.
- Authentication-provider selection is deferred until real multi-user requirements reach Phase 8.
- Tests and examples are developed with each implementation slice; no unperformed verification is claimed.

## Not started

- Phase 0.1 solution/project scaffolding.
- Implementation code.
- Database schema.
- Automated tests.
- WinForms management host.
- Example application.

No implementation or test-coverage claims are made beyond this documentation baseline.
