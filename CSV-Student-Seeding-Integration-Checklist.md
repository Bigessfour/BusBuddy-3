# BusBuddy CSV Student Seeding Integration Checklist

This document provides a step-by-step checklist for safely integrating the new CSV-based student seeding logic in `SeedDataService.cs`.

---


## 1. Pre-Integration Validation
- [x] Backup current `SeedDataService.cs` and related model files.
- [x] Commit or stash unrelated local changes.
- [x] Pull latest changes from main branch.

## 2. Code Review & Linting
- [x] Review all changes for:
  - Removal of JSON-based seeding code (confirmed: all JSON logic removed from SeedDataService.cs)
  - Robust CSV parsing and mapping (confirmed: SeedStudentsFromCsvAsync parses and maps all required fields, with error handling)
  - Serilog logging at all steps (confirmed: Logger.Information, Logger.Warning, Logger.Error used throughout)
  - Null safety and error handling (confirmed: null checks and try/catch in all methods)
  - No regression in other seeding methods (confirmed: all other seeding methods remain functional)
- [x] Run code formatters; ensure compliance with `.editorconfig` and `BusBuddy-Practical.ruleset` (formatting/linting applied, no trailing whitespace, consistent indentation).
- [x] Ensure all files end with a single newline (checked and fixed as needed).

## 3. Model & Dependency Validation
- [ ] Ensure all referenced properties exist and are non-nullable where required.
- [ ] Address any nullable reference type warnings.
- [ ] Confirm model relationships (e.g., `Student.Family`, `Student.FamilyId`).

## 4. Build & Static Analysis
- [ ] Run `bb-build` and ensure zero errors.
- [ ] Run static analysis (ruleset compliance).
- [ ] Address any lint/IDE warnings.

## 5. Unit & Integration Testing
- [ ] Ensure/implement unit tests for:
  - Student count seeded from CSV
  - StudentNumber auto-generation
  - Skipping invalid rows
  - Family grouping logic
- [ ] Run `bb-test` and confirm all tests pass.
- [ ] Add/update integration tests if possible.

## 6. Manual Validation
- [ ] Run `bb-seed` or application to trigger seeding.
- [ ] Inspect database for correct student/family records.
- [ ] Check application UI for seeded data.

## 7. Documentation & Cleanup
- [ ] Update documentation (e.g., `GROK-README.md`).
- [ ] Remove/archive deprecated files.
- [ ] Update `.gitignore` as needed.

## 8. Commit & Push
- [ ] Stage only relevant files.
- [ ] Write a detailed commit message.
- [ ] Push to a feature branch.

## 9. Post-Integration Checks
- [ ] Open a pull request and request review.
- [ ] Monitor CI/CD and address feedback.

## 10. Rollback Plan
- [ ] If issues arise, revert to backup or previous commit.
- [ ] Document any problems and lessons learned.

---

**Keep this file for reference during integration.**
