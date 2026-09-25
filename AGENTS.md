# Hive AI Agent Operating Rules

This file is the repository operating constitution. Keep it small. Detailed architecture, APIs, roadmap, UI usage, and implementation guidance belong in the documents named below.

## 1. Source of truth

Use this authority order:

1. `AGENTS.md` — agent workflow and repository rules.
2. `docs/architecture.md` plus the relevant `docs/architecture/*.md` detail documents — intended architecture and ownership.
3. `docs/Hive_Active_Work.md` — only authorized current implementation slice and verification gate.
4. `docs/roadmap.md` — ordered future work.
5. Source/project files — actual implementation.
6. Tests — behavior actually exercised by tests.
7. `docs/Hive_Current_Status.md` — current phase/status record only; it does not store verification history.
8. `docs/verification/` — historical verification evidence only.
9. `docs/examples/` and `docs/ui/` — practical usage guidance.
10. `README.md` — project overview.

When sources conflict:
- architecture governs intended structure;
- source governs implementation reality;
- Active Work governs current scope;
- roadmap governs order, not permission to skip work;
- status never turns an unverified claim into a fact.

Do not use previous chat history as repository authority.

## 2. Before changing code

For agents that support repository-local skills, use `.agents/skills/hive-repository-workflow/SKILL.md` as the reusable Hive workflow procedure for Hive tasks, including implementation, Revision, Maintenance, Architecture, Verification, and governance/workflow-documentation work. The skill is subordinate to this file and does not replace its rules.

Read:
1. this file;
2. `docs/Hive_Current_Status.md`;
3. `docs/Hive_Active_Work.md`;
4. the relevant `docs/roadmap.md` slice;
5. the relevant `docs/architecture.md` sections and corresponding `docs/architecture/*.md` detail documents.

For UI/Example work, also read the relevant `docs/ui/` guide.

Then inspect the affected projects, references, source, tests, examples, configuration, and package references.

Determine the current repository checkpoint from evidence.

If Active Work contains an open slice, continue that slice exactly. Do not implement later roadmap work.

If the open Active Work item says **VERIFICATION PENDING**, **verification is required**, or otherwise establishes a developer-verification gate, stop implementation at that gate. Do not perform additional implementation merely because the user says "Continue". Provide the exact required verification handoff instead.

If the active slice is closed, do not automatically advance the roadmap. Roadmap advancement requires explicit user authorization.

A maintenance, audit, polish, revision, or "again/continue" request does not authorize a later roadmap slice.

Revision means re-reviewing the immediately preceding work within its inherited mode, domain, scope, and active slice. Revision may correct concrete issues within that inherited scope but must never advance the roadmap.

## 3. Scope

Default rule: **make the smallest correct change that fully satisfies the active requirement.**

Do not:
- implement future slices;
- refactor unrelated code;
- perform drive-by cleanup;
- add speculative abstractions;
- upgrade dependencies without a concrete requirement;
- replace working technology for preference;
- silently widen scope or change behavior.

Supporting changes are allowed only when required for the active implementation.

Governance/documentation tasks explicitly requested by the user may change the affected source-of-truth files even when an unrelated implementation slice is open, but must not modify unrelated implementation, close/advance Active Work, or activate roadmap work.

## 4. Architecture

Preserve the boundaries in `docs/architecture.md`.

Key non-negotiables:
- Hive.Core remains dependency-light and host/provider neutral.
- SQL/database access stays inside `Hive.Persistence` or an explicitly authorized persistence boundary.
- `Hive.Management` owns management/application operations.
- `Hive.Host.WinForms` must not bypass `Hive.Management`.
- `Hive.Host.WinForms.UI` owns Hive WinForms presentation.
- Hive.Example.WinForms is a consumer/example host, not a platform dependency.
- Use Microsoft Agent Framework where it already owns the required mechanism; do not build a second orchestration engine for the same responsibility.
- Preserve explicit Agent/Hive generations; do not add runtime promotion/demotion.
- Authorization is enforced in code, never by prompts, UI visibility, or model output.
- Host business state remains host-owned; Hive persistence remains separate.
- Do not introduce architectural changes silently. Escalate ambiguous or breaking decisions.

Before creating a new abstraction, find the existing responsibility owner and reuse it when the contract fits.

## 5. Production implementation

New production code must be production-ready for the active contract, not a prototype or happy-path stub.

Apply the requirements that matter to the boundary:
- validation and clear errors;
- nullable/public API correctness;
- cancellation and async behavior;
- concurrency/lifecycle correctness;
- authorization/security;
- deterministic resource ownership/disposal;
- persistence consistency and constraints;
- compatibility and extensibility where required.

Prefer the simplest design that fully satisfies the contract. Do not add abstraction without a real boundary, substitution, volatility, ownership, or testability reason.

Never silently swallow failures or expose secrets.

## 6. Dependencies and persistence

Before adding a dependency, inspect existing references and versions.

Prefer:
1. existing Hive architecture;
2. .NET/framework capability;
3. MAF where applicable;
4. existing dependencies/boundaries.

Do not upgrade packages/frameworks merely because newer versions exist.

Persistence rules:
- parameterized SQL;
- appropriate indexes for repeated lookups;
- explicit transaction boundaries where required;
- database constraints for concurrency-sensitive invariants;
- explicit ordered/repeatable migrations;
- no silent durable-schema or persistence-semantic changes.

## 7. Example and test requirements

`Hive.Tests` is the authoritative automated test project.

Every new meaningful capability requires the focused automated coverage appropriate to its boundary.

Every new meaningful externally usable capability also requires a matching `Hive.Example.WinForms` scenario in the same implementation run.

Examples must:
- use supported public APIs;
- be reproducible and understandable;
- use the established Example Host discovery pattern;
- not use test-only shortcuts or private production helpers.

Use `docs/ui/examples.md` for the exact Example Host pattern and tree placement.

Final handoff for a capability requiring an Example MUST contain:

```text
Example to run: <exact Category / Subcategory / optional AdditionalNavigationPath / Example title> — Hive.Example.WinForms
Tests to run: <exact focused test class/file>; broader-suite requirement if applicable
```

When verification is pending, keep the same exact Example path and test target in `docs/Hive_Active_Work.md`.

## 8. UI rules

Use the existing Hive UI API before creating a new UI abstraction.

For UI/Example changes:
- read `docs/ui/README.md`;
- use the relevant `docs/ui/controls.md`, `forms.md`, or `examples.md`;
- use native WinForms when no Hive-specific contract is needed;
- do not create Hive wrappers merely to rename native controls;
- preserve theme, focus/selection, responsiveness, thread affinity, and resource ownership;
- user-visible UI errors/failures must be reported through `HiveMessageBox` and, when an `IHiveExampleOutput` sink is available, the active Output panel;
- unexpected exceptions should include technical details in the MessageBox details section and Output panel while never exposing secrets;
- cancellation and expected validation feedback are not treated as unexpected exceptions;
- do not put SQL, provider transport, or authorization policy into reusable UI controls.

Manual UI correctness is established only by actual developer/manual verification when required by the active slice.

## 9. Verification

Never claim verification that did not happen.

Use precise terms:
- **Inspected** — source/document inspection only.
- **Reasoned** — behavior derived without execution.
- **Compiled** — an actual successful build.
- **Automated-tested** — tests actually ran.
- **Integration-tested** — integration boundary actually ran.
- **Manually verified** — developer actually exercised the behavior.
- **Not verified** — not executed/established.

By default, do not run builds, tests, application launches, migrations, performance measurements, or external integrations unless explicitly authorized by the user or required by the repository workflow for the requested task.

Never invent results, test counts, provider responses, migration results, or performance data.

## 10. Documentation

Update only the source-of-truth document whose state changed:

- `docs/architecture.md` — intended architecture/ownership/contracts.
- `docs/Hive_Active_Work.md` — current scope/checkpoint/verification gate.
- `docs/roadmap.md` — ordered implementation plan.
- `docs/Hive_Current_Status.md` — actual phase/status only; link to historical verification records rather than copying them here.
- `docs/verification/` — historical records of verification actually performed.
- `docs/ui/` — concise UI/Example API usage.
- `docs/examples/` — public usage/reference examples.
- `.agents/skills/` — reusable agent workflow skills; these define procedure, not current Hive state or architecture.
- `README.md` — project-facing overview.

Do not document planned behavior as implemented.
Do not copy detailed architecture or API manuals into `AGENTS.md`.

## 11. Slice completion

Do not close a slice merely because code is written.

A slice closes only after:
1. required implementation exists;
2. required tests/examples/docs exist;
3. required verification has actually been performed;
4. Active Work and Status are updated from real results;
5. the next slice is authorized.

When execution is not authorized, hand off exact verification targets and leave the gate open.

## 12. Git and repository hygiene

- Normal work goes directly to `main` unless explicitly instructed otherwise.
- Do not create branches/PRs unless requested.
- Preserve unrelated developer changes.
- Never reset, clean, overwrite, or force-resolve work you did not create.
- Never rewrite published history or force-push unless explicitly instructed.
- Keep commits focused and logically scoped.
- Do not commit secrets, local environment files, build artifacts, or machine-specific output.
- If remote `main` changed concurrently, reconcile before updating it.

## 13. Final review

Before handoff:
1. review the diff and affected files;
2. check for accidental edits and dead/stale code;
3. confirm scope and architecture;
4. confirm required tests/examples/docs exist;
5. report exactly what changed;
6. report exactly what was verified;
7. explicitly state what remains unverified.

## Final rule

**Use repository evidence. Continue only the authorized slice. Make the smallest correct production change. Preserve architecture and boundaries. Add required tests/examples. Verify only what actually ran. Keep detailed knowledge in the owning docs, not here.**
