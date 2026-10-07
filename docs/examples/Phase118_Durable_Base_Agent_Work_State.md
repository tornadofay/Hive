# Phase 1.18 — Durable Base-Agent Work State Example

## Example Host surface

Persistence / Agent Work State / Durable Base-Agent Work State & Runtime Recovery

Assembly: Hive.Example.WinForms

## Scenario

The scenario creates a Base Agent, composes SQL-backed work stores, creates an Objective with a WorkItem binding, stores memory with explicit evidence classification, creates a Question and delegation request, stops the runtime, recreates a RuntimeInstance with the same RuntimeId, and reloads the persisted work state.

The delegation demonstration is request persistence only. Phase 1.18 does not schedule, execute, retry, or otherwise orchestrate delegated work.

Runtime incarnation is ephemeral. The durable work-state boundary is the explicit RuntimeId.

## Handoff

Example to run: Persistence / Agent Work State / Durable Base-Agent Work State & Runtime Recovery — Hive.Example.WinForms

Tests to run: Phase118DurableBaseAgentWorkStateTests.cs; broader Hive.Tests requirement.
