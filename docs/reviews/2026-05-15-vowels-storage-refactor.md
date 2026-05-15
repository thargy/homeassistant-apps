# Code Review: Vowels Storage Refactor (2026-05-15)

## Overview
This review covers the migration of the storage engine to the new `Vowels.FileStoreRegistry` project and the refactoring of core storage components in `Vowels.Core`.

## Participants
- **Reviewer:** USER
- **Author:** AI (based on latest commits)

## Issues & Observations

| ID | Issue | Severity | Status | Notes |
|----|-------|----------|--------|-------|
| 1 | Excessive public visibility of classes | Medium | Open | Use `internal` and `InternalsVisibleTo` where possible. |
| 2 | Rename `Vowels.Core.Common` to `Vowels.Common` | Low | Open | Shared across all components. |
| 3 | Dynamic plugin loading in `Vowels.Daemon` | High | Open | Move away from hard-linking in `.csproj`. |
| 4 | Custom plugin configuration from `config.yaml` | Medium | Open | Support specific plugin config extraction. |
| 5 | Rigid naming/sizing of `HourlyMmfFile` | Medium | Open | Remove "Hourly" and support dynamic/different sizes. |
| 6 | Lack of common base/interface for storage files | Medium | Open | Need shared abstraction with long-term storage files. |

## Holistic Requirements & Problems
*This section tracks the "problem and requirements" as they are uncovered.*

- [ ] High-performance, reactive time-indexed storage.
- [ ] Decoupled orchestration via `EntityRegistry`.
- [ ] Robust multi-file MMF management in `FileStoreManager`.

## Next Steps
1. [ ] Conduct interview/grilling session to uncover potential issues.
2. [ ] Map issues to requirements.
3. [ ] Propose a holistic fix plan.
