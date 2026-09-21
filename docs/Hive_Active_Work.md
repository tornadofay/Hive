# Hive — Active Work

Last updated: 2026-09-21

## Active slice

None. The architecture baseline is finalized before implementation begins.

## Next planned slice

**0.1 — Solution & project scaffolding**

See `docs/roadmap.md` for the objective and verification criteria.

## Architecture baseline constraints for Phase 0.1

- Do not implement CognitiveAgent/CognitiveHive.
- Do not implement generic cross-host UI discovery/generalization.
- Treat the concrete V1 WinForms host-context discovery boundary as part of the planned V1 integration slices.
- Do not implement future governance/cognition/resource phases early.
- Preserve the base Agent/Hive dependency direction.
- Keep the V1 forcing function visible in subsequent slice decisions.
- Keep V1 Workspace scope limited to WorkItem/image processing and business-app approval operations.
- Treat the WinForms UI foundation and first-class Example host as Phase 0 infrastructure used by later slices.
- Keep ReaLTaiizor behind `Hive.Host.WinForms.UI`; consuming forms must not reference the third-party UI library directly.
- Keep Example test execution as external developer tooling over `dotnet test`; `Hive.Tests` remains authoritative.
- Do not pull Agent/Hive topology, Swarm views, general LLM mode, or Agentic mode into Phase 1; their Workspace extensions require the owning later capabilities.
