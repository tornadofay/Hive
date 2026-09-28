# Revision — Maintenance — Backend: Management Lifetime, Provider Dependency, and Forced Refresh Coalescing

Date: 2026-09-28

## Status

Complete and verified.

Revision checkpoint: `46bf631b41ac7142926036ed2e64a36ff7ed5efa`

## Finding corrected

The preceding maintenance implementation could allow a forced discovery request waiting behind an overlapping non-forced discovery to reuse that non-forced result. That did not satisfy the intended `forceRefresh: true` contract.

## Scope completed

1. Preserved the previously corrected Management facade disposal/lifetime boundary.
2. Preserved the previously corrected `Hive.Management` provider dependency boundary.
3. Corrected provider-discovery cache metadata so each cached observation records whether it came from a forced refresh and a monotonic completion sequence.
4. Changed forced-refresh coalescing so only a successful forced refresh completed after the waiting request began can satisfy that forced caller.
5. Added regression coverage proving an overlapping non-forced discovery does not satisfy a forced refresh while retaining the existing forced-success and failed-refresh coverage.
6. Aligned the Phase 1.16 architecture wording with the corrected forced/non-forced behavior.

## Developer verification

Developer reported:

- Full `Hive.Tests`: **455/455 passed**
- 0 failed
- 0 skipped
- 49.1 seconds
- .NET 10.0.1
- **Treat Warnings as Errors / zero-warning confirmed**

No manual UI verification was required because this revision did not change user-visible behavior.

## Review outcome

The concrete revision finding is corrected and verified. No roadmap advancement or unrelated capability was introduced.
