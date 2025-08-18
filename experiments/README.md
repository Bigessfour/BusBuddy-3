# Experiments (Archived / Deferred Features)

This directory holds non-MVP or temporarily disabled features migrated from scattered `.disabled` source files.

Contents:

- `xai/AIEnhancedRouteService.cs` (from Services/AIEnhancedRouteService.cs.disabled)
- `xai/OptimizedXAIService.cs` (from Services/OptimizedXAIService.cs.disabled)
- `xai/XAIService.cs` (from Services/XAIService.cs.disabled)

Rationale:

- Keeps core `BusBuddy.Core/Services` folder lean for MVP
- Clear separation of experimental or postponed code
- Eliminates `.disabled` suffix (improves tooling / analyzer behavior)

Re-enable Process:

1. Move file back into appropriate project folder (e.g., `BusBuddy.Core/Services`).
2. Ensure namespace still matches target folder structure.
3. Add to `.csproj` if not using wildcard includes (current project uses SDK-style implicit includes; normally no change needed).
4. Resolve any dependency / interface references that may have drifted.
5. Run `bb-build` then `bb-mvp-check` to confirm no regressions.

Notes:

- These stubs intentionally keep only skeletal class shells (original logic was deferred) to avoid accidental dependency drag.
- Keep experimental logic isolated until post-MVP prioritization review.
