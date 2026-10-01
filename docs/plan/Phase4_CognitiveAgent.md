# Phase 4 — CognitiveAgent : Agent

This document contains the detailed ordered plan for this phase. It does not authorize implementation; authorization remains in `docs/Hive_Active_Work.md`.

The base Agent and V1 pipeline continue working unchanged throughout this phase. The base Agent may already provide Objectives, Question transport, patience/understanding gates, memory infrastructure, simulations, delegation, and Hive creation as reusable mechanisms. CognitiveAgent is created explicitly and adds adaptive cognition over those mechanisms; no runtime type promotion or demotion is introduced.

## 4.1 — Cognitive Kernel
Persistent cognitive identity binding, lifecycle, state versioning, recovery, and per-runtime concurrency ownership.

## 4.2 — Cognitive Strategy
Replaceable strategy contract capable of deterministic decisions and explicit no-model paths, including adaptive interpretation of evaluated outcomes, contextual Risk/Fear/Confidence, reconsideration, and selection among direct execution, Questions, Hive assistance, Dreams, decomposition, and previously governed strategy/resource adaptations. This slice defines the extension point for learned deterministic shortcuts but does not implement Learning Candidate promotion from later Phase 5 work.

Verify: strategy decisions can consume cognitive evidence and Risk/Fear/Confidence without bypassing authorization, capability, scope, budget, or execution planning; any promoted adaptation is consumed only through its owning governed contract.

## 4.3 — Reasoning Requirement
Provider-neutral reasoning requirements kept separate from concrete Execution Target planning.

## 4.4 — Persistent Cognitive State
Beliefs, bounded workspace/attention, goals, intentions, plans, methods, self-model, contextual Risk/Fear/Confidence state, impasses, and revision-safe transitions. Persistent state is independent of whether a runtime incarnation is currently active.

Risk, Fear, and Confidence are evidence-backed cognitive state rather than authorization or policy state. They may alter strategy and escalation behavior but never override authoritative enforcement.

Verify: versioned cognitive-state transitions preserve context/provenance for Risk/Fear/Confidence and do not permit cognitive state to bypass deterministic safety, authorization, capability, scope, or budget checks.

## 4.5 — Experience, Outcome Evaluation & Cognitive Event History
Bounded experience capture, provenance, expected-versus-observed results, outcome evaluation, attribution/credit context, and replayable supported transitions. OutcomeEvaluation is a first-class cognitive contract/process that establishes a provenance-bearing outcome classification from the observed evidence and applicable success criteria. Success, Mistake, Partial, and Unknown are first-class cognitive outcome concepts associated with that evaluation; specialized processing may consume them without making them aliases for execution states. Outcome correctness remains distinct from method/strategy quality and causal attribution. Actual observations/experiences remain distinguishable from simulated, predicted, counterfactual, human-corrected, and external evidence.

Define mutually exclusive outcome semantics for one evaluation:
- Success = all applicable success criteria were actually satisfied;
- Partial = some but not all applicable criteria were satisfied and the result is incomplete rather than wholly incorrect;
- Mistake = the result is known to be wrong relative to the intended objective or success criteria and is not better classified as Partial;
- Unknown = available evidence cannot establish the substantive result.

Technical execution failure is not automatically a Mistake. Technical execution success is not automatically a cognitive Success. Attribution of the failure or success remains a separate evidence problem and may involve the Agent, tools, specialists, the environment, or other factors.

Verify: outcome evaluation preserves evidence, attribution, and the distinction between outcome correctness and method/strategy quality; actual/simulated evidence remain distinguishable; technical failure/success cannot be silently mapped to cognitive learning labels; partial and unresolved outcomes remain representable.

## 4.6 — Death / Wake / Reincarnation Lifecycle
Define death as complete termination of the current runtime/incarnation, preserve Agent identity and cognitive state, support inactive periods with no live runtime, and explicitly reconstruct a new runtime from durable state when the Agent wakes.

## 4.7 — Postmortem & Dream Processing
Define bounded postmortem processing plus a first-class Dream subsystem that can inspect history, generate hypothetical alternatives, run multiple simulations in parallel, compare predicted outcomes, and produce proposed cognitive updates without requiring the Agent runtime to remain alive. Dream purposes have explicit semantics and provenance; purpose-specific processors may share the core Dream contract or be separately replaceable when scheduling, lifecycle, or resource boundaries justify that split. Proposed changes are not authoritative state transitions; reconciliation and the owning resource/governance boundary decide whether they are accepted.

Dream purposes include:
- Recovery — explore alternatives after a Mistake or unresolved outcome;
- Optimization — search for cheaper, faster, safer, simpler, or more deterministic ways to reproduce a Success;
- Nightmare / Stress-Test — actively search for plausible conditions under which an apparently successful method, plan, assumption, or strategy would fail;
- Reconsideration — revisit prior decisions in light of later evidence;
- Preparation — rehearse plausible future scenarios.

Dream processing is governed by applicable authorization, provider/model quota, token/cost budget, time budget, concurrency/parallelism limits, retrieval/work limits, and cancellation.

Dream evidence remains simulated/predicted evidence and cannot become actual experience. Counterfactual conclusions such as Regret must remain distinguishable from information actually available at the time of the original decision.

After a Mistake, Cognitive Strategy may retry with a revised method directly or may first use Questions, Hive assistance, or a Recovery Dream when the expected benefit justifies the additional work. After a Success, it may use Optimization and Nightmare/Stress-Test Dreams before adopting a broader lesson.

Verify: evaluated Mistake → Recovery proposal or bounded revised retry; evaluated Success → Optimization proposal; evaluated Success → Nightmare/Stress-Test proposal; Dream results remain simulated; Dream processing works while the Agent runtime is inactive; an inactive-runtime Dream requires an already authorized request or durable policy trigger; budgets/cancellation/concurrency are enforced.

## 4.8 — Questions
Define first-class Questions with structured context, specialty, provenance, answer type, evidence requirements, status, and confidence/uncertainty where applicable. Support specialty-specific questions so different Agents can investigate different aspects of the same user objective.

Questions may be selected or prioritized when outcome attribution is uncertain, risk remains high, evidence conflicts, or a missing fact materially changes the choice among competing strategies.

Verify: unresolved Mistake/Success attribution can result in an evidence-seeking Question; redundant Questions remain avoidable when sufficient evidence already exists.

## 4.9 — Cognitive State Reconciliation
Integrate human edits, actual experience, evaluated outcomes, Mistake/Success interpretations, Dream results, Question answers, beliefs, goals, plans, Risk/Fear/Confidence state, and other candidate updates through versioning, provenance, authorization, validation, and concurrency boundaries before the next wake/reincarnation.

Conflicting evidence must remain attributable. Reconciliation may retain multiple hypotheses, uncertainty, or an unresolved Question instead of inventing a single authoritative explanation.

Verify: concurrent human/Dream updates do not lose evidence; actual experience cannot be overwritten by simulated evidence; stale candidate updates are rejected or reconciled explicitly.

---
