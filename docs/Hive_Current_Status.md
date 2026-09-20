# Hive — Current Status

Last updated: 2026-09-20

## Repository state

Architecture/documentation baseline only. No implementation has started.

## Current phase

Phase 0 — Foundations.

## Architecture decisions now locked

- Hive is general-purpose; the real V1 forcing function is automating data entry from documents/images into the existing business application.
- The V1 pipeline is not Hive's permanent definition; it determines implementation order.
- `Agent` and `Hive` are stable base types.
- `CognitiveAgent : Agent` and `CognitiveHive : Hive` are later additive generations.
- Concrete type is selected at creation; there is no runtime promotion/demotion.
- Different Agent/Hive generations can coexist without ancestor contract changes.
- Persistent cognition belongs to the CognitiveAgent generation rather than being a prerequisite of the base Agent.
- Hive uses MAF for execution/orchestration mechanisms MAF already provides.
- One shared OpenAI-compatible provider adapter serves compatible providers/local servers through configuration.
- V1 provider configurations currently targeted: Groq, OpenRouter, Cloudflare, Cerebras, NVIDIA, Google, and local OpenAI-compatible servers.
- The business-app integration boundary must be decided before the write-tool slice: API versus narrowly scoped UI integration.
- CognitiveAgent persists its cognition independently of any one runtime incarnation; death ends the incarnation, not the Agent or its durable state.
- Dreams are bounded offline cognitive simulations/analysis that can run with no live Agent runtime, including during host-application downtime; Dream output remains distinct from actual experience.
- Questions are first-class, specialty-aware cognitive objects that can be owned by individual CognitiveAgents and coordinated collectively by CognitiveHive.
- CognitiveHive adds collective cognition without moving or replacing member-level cognition.
- Generic host integration is deferred until a second real host proves the need.
- V1 human intervention is Approve/Reject at the business-app write; the broader intervention taxonomy is later.
- Tests and examples are developed with each implementation slice; no unperformed verification is claimed.

## Not started

- Phase 0.1 solution/project scaffolding.
- Implementation code.
- Database schema.
- Automated tests.
- WinForms management host.
- Example application.

No implementation or test-coverage claims are made beyond this documentation baseline.
