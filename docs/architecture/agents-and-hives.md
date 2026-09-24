# Hive Architecture — Agents, Hives, and Cognitive Generations



This document is part of the authoritative architecture defined by `docs/architecture.md`. It contains the Agent/Hive type rules, runtime mechanisms, cognitive generations, and lifecycle boundaries.



## 3. Agent / Hive Type Hierarchy

The hierarchy is a **creation-time type choice**, not a runtime promotion system.

```
Agent (base)                              Hive (base)
  instructions, tools, context,           membership, roles, communication,
  conversation, execution,                 delegation, authorization,
  resources, objectives,                   population, lifecycle,
  questions, patience,                     coordination via MAF,
  memory/simulation mechanisms             complete and useful alone
  complete and useful alone
       │                                        │
       ▼                                        ▼
CognitiveAgent : Agent                  CognitiveHive : Hive
  adds: Cognitive Kernel,                 adds: collective/shared cognition
  Cognitive Strategy,                     while each member keeps its own
  Reasoning Requirement,                  Agent/CognitiveAgent identity,
  persistent goals/beliefs/               cognition, and state
  intentions/plans,
  experience, self-model,
  death/wake/reincarnation,
  Dreams, and Questions
       │                                        │
       ▼                                        ▼
Future generations remain open-ended and may coexist with older generations.
```

### Type rules

1. A concrete Agent/Hive type is selected when that resource/runtime is created.
2. A normal `Agent` never silently becomes a `CognitiveAgent`.
3. A `CognitiveAgent` never silently becomes a normal `Agent`.
4. There is no runtime promotion/demotion feature in the base architecture.
5. Projects that need a different generation create the desired subtype explicitly.
6. Older and newer generations may coexist in the same deployment.
7. A subtype is strictly additive. It must not redefine, remove, or require changes to the ancestor contract.
8. Base runtime/persistence/execution infrastructure is written against the base contracts and does not need to know which descendant type is being used.
9. A subtype may use additional state and events owned by that subtype, but ancestor-owned state semantics remain stable.
10. A future generation may be introduced without forcing existing Agent/Hive implementations to change.
11. Agent generation is selected explicitly when the Agent is created; it never changes automatically during runtime or reincarnation.
12. A creator may request any supported Agent generation, including a CognitiveAgent, only when explicit authorization/policy permits that generation; generation is never inferred automatically from task complexity.
13. Agent generation and Hive membership are independent. A base Hive may contain CognitiveAgents and a base Agent may create or join Hives without changing type.
14. The Agent that sponsors a Hive is not the Hive's lifecycle owner. Sponsorship is a relationship; sponsor retirement, runtime death, or deletion does not automatically delete or retire the Hive or its members.

This gives Hive long-term flexibility without making type mutation a correctness problem.

### Base Agent mechanisms vs CognitiveAgent semantics

A base `Agent` may be substantially capable without being a `CognitiveAgent`. The boundary is **not** "simple features versus smart features"; it is whether the Agent owns adaptive cognitive interpretation and revision.

Base Agent mechanisms may include:

- **Objectives** = explicit work targets with lifecycle/status, completion criteria, priority, deadline, and dependency metadata. An Objective can make a base Agent objective-driven without giving it autonomous goal formation or reconsideration.
- **Memory infrastructure** = durable storage/retrieval for conversation, execution history, artifacts, tool results, checkpoints, configuration, and other explicitly addressed state. Cognitive memory semantics belong to `CognitiveAgent`.
- **Question protocol** = first-class question/answer transport, provenance, ownership, status, timeout, answer type, and waiting. The base Agent may know what required information is missing without dynamically generating cognitive inquiry strategies.
- **Patience / Understanding Gate** = a deterministic or policy-driven gate that prevents consequential work until required information or explicit confirmation is available. The principle is **understand the minimum required information before trying to solve**; it does not require the Agent to understand everything.
- **Simulation infrastructure** = generic bounded simulation/job execution, scenario inputs, predicted outputs, parallel simulation execution, and result storage. Cognitive `Dreams` are a higher-level use of this infrastructure.
- **Delegation and coordination interfaces** = request work from another Agent/Hive, await answers, and preserve provenance without requiring autonomous cognitive delegation decisions.
- **Lifecycle and persistence** = creation, suspension, death of an execution/incarnation, wake/recreation, durable state, and human management of inactive state.

These mechanisms are intentionally reusable by later CognitiveAgent implementations.

A mechanism becomes cognitive when the Agent can autonomously interpret and revise it as part of its own ongoing cognition—for example: forming/revising Goals, revising Beliefs, selecting and reconsidering Intentions, generating/choosing Questions, deciding what to Dream and why, interpreting simulation results, revising its self-model, or changing strategy from experience.

### Base Agent creation/runtime contract

Phase 1.5 establishes the first executable base Agent boundary without persistence or model execution:

- `AgentDefinition` is immutable configuration containing the stable definition key/display name and explicitly selected `AgentGeneration`.
- `AgentFactory` is the creation boundary. Creation requires a `ResourceAccessContext` with deployment and principal identity and passes through `IAgentCreationAuthorizer`.
- The factory currently supports only `AgentGeneration.Base`. `AgentGeneration.Cognitive` is represented as a reserved later generation and is rejected until its owning phase supplies the concrete implementation.
- An `Agent` receives one immutable generation at creation. There is no runtime promotion or demotion.
- A base Agent creates independent `RuntimeInstance` incarnations. Each runtime receives a distinct `RuntimeId` while retaining the owning Agent identity and generation.
- A RuntimeInstance may create `Execution` objects. Each Execution has its own `ExecutionId`, retains its Runtime/Agent ownership, and has an independent immutable lifecycle.
- Runtime and Execution objects in this slice are in-memory runtime contracts. They do not add persistence, provider transport, MAF execution, cognitive state, or Management operations.
- Runtime/Execution state transitions are fail-closed typed results; a stopped RuntimeInstance cannot start new Execution objects, and terminal Execution objects cannot transition again.
- The factory does not contain cognitive-generation logic and does not infer generation from task complexity.

### Base Agent Work Protocol boundary

Phase 1.6 makes the base Agent's work mechanisms explicit without introducing cognitive strategy or a workflow engine.

The runtime-owned protocol surface is RuntimeWorkProtocols. Each RuntimeInstance receives one independent protocol bundle, and stopping a runtime preserves that runtime's protocol state for inspection/recovery; a different RuntimeInstance receives a different bundle.

The bundle provides:

- **Objectives** — explicit work targets with immutable identity, Runtime ownership, lifecycle (Active, Completed, Cancelled), updateable completion criteria/priority/deadline/dependencies, and optional WorkItemBinding. Completion and cancellation are explicit state transitions; the protocol never infers or revises objectives autonomously.
- **WorkItem binding** — binds a base Agent runtime to the existing Core WorkItem identity and captures the WorkItem resource version plus a provenance source reference. Binding requires the caller's Agent/Runtime access context to match the runtime boundary and the WorkItem owner/scope; binding does not grant additional authorization.
- **Memory** — IAgentMemoryStore is a replaceable storage boundary. Phase 1.6 supplies an in-memory implementation only. Entries are Runtime-scoped ResourceEnvelope<MemoryId> records and are append-oriented; retrieval is explicitly scoped to the owning RuntimeInstance and access context, so one runtime cannot silently retrieve another runtime's entries.
- **Questions** — IQuestionTransport provides first-class Runtime-scoped Questions with ownership, timeout deadline, Answer/Cancel/Timeout transitions, and asynchronous waiting. Timeout is deterministic through the existing IClock plus explicit ExpireDue() processing; no second scheduler/workflow engine is introduced. The Answer records responder identity, but Phase 1.6 requires the responder to match the owning RuntimeInstance; only that RuntimeInstance may wait, answer, or cancel the Question.
- **Patience / Understanding Gate** — IUnderstandingGate evaluates explicit required-information keys and optional confirmation requirements. It returns a deterministic Satisfied or Blocked result with missing information; it does not generate questions, infer missing facts, or make cognitive decisions.
- **Delegation** — IDelegationChannel carries explicit work requests between Agent runtimes with requester/delegate identity, optional WorkItem source, and ResourceProvenance. The Phase 1.6 in-memory implementation stores requests but does not schedule, execute, retry, or otherwise orchestrate delegated work.

Phase 1.6 adds typed protocol identities for ObjectiveId, MemoryId, QuestionId, and transient DelegationId. Objective, Memory, and Question use the existing Core resource envelope/scope/provenance model and remain in-memory in this slice. Their durable event/snapshot/outbox representation belongs to later persistence slices.

The base work-protocol APIs are additive to Agent and RuntimeInstance; they do not alter the Agent generation contract and do not create CognitiveAgent behavior. Cognitive generations may later build adaptive interpretation and revision over these same stable mechanisms.

### Cognitive Kernel vs Cognitive Strategy

Inside `CognitiveAgent` only:

- **Cognitive Kernel** = durable cognitive substrate: identity binding, lifecycle, cognitive-state versioning, event history, recovery, concurrency ownership, and intervention boundaries.
- **Cognitive Strategy** = replaceable adaptive reasoning process: belief revision, attention, goal formation/reconsideration, intention selection, planning, impasse handling, reflection, outcome interpretation, Mistake/Success attribution, Risk/Fear/Confidence revision, learning, Dream selection/interpretation, Question generation/selection, and deterministic-vs-reasoning routing. Strategy may consume a previously promoted deterministic shortcut, but the promotion and governance of that shortcut belong to the Learning/Resource boundary rather than being implicitly created by strategy execution.
- **Reasoning Requirement** = what reasoning capability is required.
- **Execution Planning** = where/how the requirement executes.

A cognitive strategy may decide that no model call is necessary.

### Cognitive outcomes: Mistake, Success, and Regret

CognitiveAgent learning begins with a first-class outcome evaluation, not with the raw transport result of an execution. Outcome evaluation is an explicit cognitive contract/process: it owns the comparison between intended success criteria and observed evidence and produces a provenance-bearing outcome interpretation. Mistake and Success are first-class interpretations produced by that boundary; they are not merely renamed execution states. A dedicated outcome component, evaluator, event family, projection, or other separate implementation boundary is valid when its lifecycle or replacement needs justify it.

An outcome evaluation compares the intended objective/success criteria with the observed result and the evidence available to establish whether the objective was actually achieved.

The minimum semantic distinction is:

```
Execution
    ↓
technical result
    ↓
Cognitive outcome evaluation
    ├── Success
    ├── Mistake
    ├── Partial
    └── Unknown / unresolved
```

**Success** is an evaluated outcome in which the applicable success criteria were actually satisfied.

**Mistake** is an evaluated outcome in which the applicable success criteria were not satisfied. Whether the Agent, a tool, a specialist, the environment, or another factor caused the failure is a separate attribution question and may remain uncertain.

A technical failure such as a timeout, unavailable provider, or cancelled transport is therefore not automatically a Mistake. Likewise, a technically successful execution is not automatically a cognitive Success: the produced result may still be wrong, incomplete, unsafe, or otherwise inconsistent with the objective.

Outcome evidence should preserve, as applicable:

- expected result/success criteria;
- observed result;
- evaluation basis/evidence;
- Objective/Goal/Intention/Plan/Method/Decision references;
- Questions and answers that affected the decision;
- tool and specialist contributions;
- relevant environmental/external factors;
- attribution/credit hypotheses;
- evaluation confidence and unresolved uncertainty.

**Regret** is a counterfactual interpretation made after an outcome: given later knowledge, another available action appears preferable. Regret must not rewrite what the Agent actually knew at the original decision point.

### Relationship to V1 Review

Cognitive outcome evaluation and V1 post-write Review answer different questions.

```
V1 Review
    ↓
Was the resulting host/application state correct?
```

```
Cognitive OutcomeEvaluation
    ↓
Did the CognitiveAgent satisfy its objective/success criteria,
and what does that outcome mean for learning?
```

A Review may supply authoritative host-state evidence to OutcomeEvaluation, including human-corrected evidence, but the Review lifecycle/status remains a V1 work-operation concern. A Cognitive Success or Mistake must not be inferred merely from `VerifiedCorrect` or `VerifiedIncorrect` without evaluating the CognitiveAgent's own objective and evidence.

### Risk, Fear, and Confidence

Risk, Fear, and Confidence are cognitive state used by Cognitive Strategy rather than authorization state.

**Risk** represents the Agent's contextual estimate of potential adverse consequence and/or uncertainty associated with an objective, plan, method, or decision.

**Fear** represents the Agent's strategy-level response to perceived risk, consequence, and adverse experience. It is allowed to change how cautiously the Agent approaches a problem. For example, increasing Fear may cause the strategy to decompose a difficult objective, obtain more evidence, ask a Question, invoke a specialist through Hive, or run a Dream before acting. Repeated successful evidence under comparable conditions may reduce Fear when it lowers estimated risk, but success does not automatically erase known risk.

**Confidence** represents evidence-backed support for a belief, method, plan, or strategy under stated conditions. Confidence is contextual rather than global. Repeated success can increase confidence in a method without establishing that it is universally reliable.

Risk/Fear/Confidence may influence:

- direct versus decomposed problem solving;
- deterministic versus model-assisted execution;
- verification strength;
- Question generation;
- Hive/specialist escalation;
- Dream selection;
- willingness to optimize an already successful method.

They never bypass capability checks, authorization, safety rules, budgets, host validation, or other authoritative enforcement.

### Dream modes and Nightmare

Dream is a first-class cognitive subsystem/contract for bounded simulation and hypothetical analysis. Its purpose is part of the Dream's semantics and provenance, and Dream processing may run independently of an active runtime.

Dream implementations may share common simulation infrastructure across purposes or separate recovery, optimization, and stress-test processing where their lifecycle, scheduling, or replacement boundary requires it; the architecture does not require them to be one implementation unit.

Supported purposes include:

- **Recovery** — explore alternatives after a Mistake or unresolved outcome;
- **Optimization** — search for cheaper, faster, safer, simpler, or more deterministic ways to reproduce a Success;
- **Nightmare / Stress-Test** — actively search for plausible conditions that would break an apparently successful plan, method, assumption, or strategy;
- **Reconsideration** — revisit goals, beliefs, plans, or decisions in light of new evidence;
- **Preparation** — rehearse plausible future scenarios before wake/runtime execution.

A Nightmare is a first-class Dream purpose with its own semantic objective: actively search for failure boundaries and hidden weaknesses in something the Agent currently considers successful or safe. Whether Nightmare processing is implemented inside the general Dream component or as a replaceable specialized processor is an implementation decision governed by lifecycle, scheduling, resource, and replacement boundaries.

A Recovery Dream can turn:

```
Mistake
  ↓
counterfactual alternatives
  ↓
predicted outcomes
  ↓
candidate recovery strategy
```

An Optimization Dream can turn:

```
Success
  ↓
alternative methods
  ↓
predicted cost/risk/quality
  ↓
candidate optimization
```

A Nightmare can turn:

```
Success
  ↓
adverse scenario generation
  ↓
predicted failures
  ↓
applicability boundary / new safeguard candidate
```

Dream outputs remain simulated evidence. They never become actual experience merely because the simulation predicts success or failure.

### Adaptive learning loop

Learning is a first-class governed cognitive process. It consumes evaluated evidence and produces candidate adaptations through an explicit validation/reconciliation boundary; it does not directly mutate durable strategy from raw model output.

The CognitiveAgent learning loop is:

```
actual attempt
    ↓
experience
    ↓
outcome evaluation
    ↓
Mistake / Success / Partial / Unknown
    ↓
interpretation, attribution, risk/confidence revision
    ↓
retry with revised strategy
OR
Dream / Question / Hive assistance
    ↓
Learning Candidate
    ↓
validation / reconciliation
    ↓
persistent cognitive adaptation
    ↓
future strategy
```

Learning may reinforce a successful method, reduce confidence in a failed method, narrow a method's applicability, add a safeguard, change decomposition behavior, or learn that a deterministic procedure can replace a model call for a known class of situations. A Mistake may therefore lead to another real attempt with a revised method, while a Dream may be used first when the strategy judges that simulation is safer or more informative. These are governed adaptations, not direct model-output mutations.

A candidate lesson must retain whether its evidence came from actual experience, human correction, Question evidence, or simulation/Dream. Simulated evidence can support a candidate without being promoted into an actual event.

---

### Dynamic Hives, population, and Swarms

A base Agent may sponsor one or more persistent Hives as a normal delegation/coordination capability. The Agent does not become a Hive and does not need to be cognitively upgraded to create one.

A Hive may:

- add existing Agents of any supported generation when authorized;
- create or reuse Agent instances/definitions for missing specialties when authorized;
- manage membership and role assignment;
- coordinate member Agents;
- become Dormant when no active work requires it;
- later reactivate with its persistent membership and member state intact.

A Hive is a persistent resource. Its sponsor is a relationship, not an implicit lifecycle owner. Sponsor retirement, runtime death, or deletion does not automatically delete the Hive or its independent members.

A **Swarm is not a persistent resource or another hierarchy layer.** It is the temporary set of selected Hive members actively collaborating on a WorkItem, Question, or other bounded problem. It has no separate durable identity, repository, or independent lifecycle. When collaboration ends, the Swarm simply ceases to be active; the Hive and its Agents remain.

The active set may contain one member, several members, or all members of the Hive. A Hive does not require a Swarm to perform ordinary work.

For a solo Agent that is not currently inside a parent Hive:

```
Agent
  ↓
requires multiple specialties
  ↓
creates/sponsors persistent Hive
  ↓
Hive creates/reuses required membership
  ↓
selected members form an active Swarm for the problem
  ↓
work complete
  ↓
selected members leave the active set
  ↓
Hive may become Dormant
```

For an Agent that is already a member of a Hive, the default rule is different:

> A member Agent does not independently create a child Hive during normal Hive-managed work. It requests missing capabilities/specialists from the parent Hive, and the parent Hive remains the population authority.

A Hive may create or reuse a specialist Agent when a required specialty is missing. The created Agent remains an independent Agent entity with its own generation, identity, state, memory, and later lifecycle. A Hive may create a CognitiveAgent specialist when explicitly authorized by its population policy; the generation is not inferred automatically.

The term **Herd** may be used informally for a group's members, but it is not an architectural resource. **Hive** is the persistent collective; **Swarm** describes the currently collaborating subset.



## 10. Cognitive Generations

### CognitiveAgent

`CognitiveAgent : Agent` is the first cognitive generation.

It adds:

- Cognitive Kernel;
- replaceable Cognitive Strategy;
- Reasoning Requirement;
- persistent cognitive state;
- observations and beliefs;
- bounded attention/workspace;
- goals;
- intentions;
- plans and methods;
- deterministic decision path;
- bounded probabilistic escalation;
- typed impasses;
- reconsideration;
- experience/history;
- evaluated outcomes including Mistake/Success interpretations;
- contextual Risk/Fear/Confidence state;
- governed death/postmortem/reincarnation.

It remains compatible with the ordinary Agent execution boundary and MAF.

### CognitiveHive

`CognitiveHive : Hive` is a later generation that adds collective/shared cognition while preserving each member's own Agent/CognitiveAgent identity, cognition, and state.

Collective cognition is additive. A CognitiveAgent remains a complete autonomous cognitive entity when outside a Hive, and a Hive does not become the owner of the member's individual goals, beliefs, plans, memory, self-model, or lifecycle.

It must not turn the base Hive into a requirement for ordinary Agents.

### Cognitive lifecycle

An Agent's persistent identity and cognitive state are distinct from any particular runtime incarnation:

```
Agent identity + persistent cognitive state
                  │
                  ▼
          Incarnation / runtime
                  │
               operates
                  │
                  ▼
                Death
                  │
        runtime no longer exists
                  │
                  ├───────────────┐
                  ▼               ▼
             Dream/analysis   human review/edit
                  │               │
                  └───────┬───────┘
                          ▼
                 persistent state
                          │
                          ▼
                     Wake / Reincarnation
                          │
                          ▼
                  new runtime incarnation
```

**Death** is the complete end of the current runtime/incarnation. It is not deletion of the Agent, its identity, or its persistent cognitive state.

A CognitiveAgent may remain fully usable as persistent state while no Agent runtime is active. The host application may also be completely shut down during this period. A later wake reconstructs a new runtime from the durable state plus any valid human or cognitive updates made while the Agent was inactive.

### Dreams

**Dreams** are bounded cognitive simulations/analyses performed against persistent Agent state without requiring the normal Agent runtime to be active.

Dreams may:

- replay or analyze historical experience;
- generate hypothetical alternatives;
- run multiple bounded simulations in parallel;
- compare predicted outcomes;
- explore plans or strategies before the next wake;
- identify unresolved questions or candidate state changes.

Dream output is never silently treated as an event that actually happened. Persistent records distinguish at least actual observations/experiences from simulations, hypotheses, predictions, counterfactuals, stress-test results, and other non-observed evidence.
A Dream records its purpose so recovery, optimization, and Nightmare/Stress-Test reasoning remain distinguishable.

A Dream may produce candidate changes to goals, beliefs, plans, memories, self-model, skills, or other cognitive resources, but authoritative state changes remain subject to the same validation, ownership, authorization, provenance, and concurrency rules as other Hive-owned state.

Dream processing is subject to applicable authorization, model/provider quota, token/cost budget, time budget, concurrency/parallelism limits, retrieval/work limits, and other resource-governance rules. Being offline or asleep never bypasses those limits.

### Questions

**Questions** are first-class cognitive objects representing an information gap, uncertainty, decision point, or request for evidence.

A Question carries structured context, specialty/role requirements, provenance, answer state, and any required evidence or answer type. Questions are not merely strings appended to a prompt.

Questions are specialty-aware. In a Hive, different Agents may receive different questions about the same user objective based on their specialization. The system should recognize semantically duplicate questions and avoid redundant work when existing evidence is sufficient.

Answers remain attributable to the Agent, Dream/simulation, source, or other evidence that produced them. Individual Agents may use their own Questions independently; CognitiveHive can coordinate Question assignment, cross-agent evidence, synthesis, conflict handling, and collective reasoning.

### Future generations

Future generations may add different cognitive strategies or different Hive coordination models. They must consume stable base contracts and remain coexistable with earlier generations.

---