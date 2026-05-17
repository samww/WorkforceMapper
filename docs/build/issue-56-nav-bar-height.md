# Issue #56 — Match nav bar height to filter bar height

> GitHub issue: https://github.com/samww/WorkforceMapper/issues/56
> Frontend-only, no domain-model changes.

## What was built

The nav bar (`<header>`) had a forced `min-h-16` (64px) and buttons with `min-h-11` / `py-2` padding, making it visually taller than the month/division filter bar below it (~44px natural height). Both bars now share the same height on desktop and mobile.

## Decisions made

- The filter bar's natural height (`py-2` outer padding + select `py-1 text-sm` content) is the target — approximately 44px.
- `min-h-16` removed from `<header>`.
- Nav tab buttons, hamburger, and Ask AI button: `py-2` → `py-1`, `min-h-11` removed. Height is now driven by padding + text, matching the selects.
- Filter bar gains `role="region" aria-label="Filter bar"` for test queryability; no `min-h-*` class needed.
- No change to drawer controls inside the mobile nav overlay — those are separate from the header bar.

## Acceptance criteria

- [x] Nav bar and filter bar have the same vertical height on desktop and mobile.
- [x] Neither bar uses `min-h-16`.
- [x] All existing App tests pass (10/10).

## Implementation summary

- `App.tsx`: removed `min-h-16` from `<header>`; changed nav tab buttons, hamburger, and Ask AI to `py-1` without `min-h-11`; added `role="region" aria-label="Filter bar"` to the filter bar div.
- `App.test.tsx`: updated "same height" test to assert neither bar has `min-h-16`; removed `min-h-11` class assertions from the responsive nav test (behaviour unchanged, only sizing reduced).
- `docs/build/frontend-components.md`: updated Responsive shell navigation section to document equal-height bars and the `py-1` button convention.
