# Issue #29 — Six-tier donor candidate pipeline

> Spec for `DataService.GetDonorCandidatesAsync` and related model changes.
> Read `docs/build/risk-model.md` for the tier table and surplus-FTE formula before implementing.

## Model changes

### `DonorCandidateRow` — add one field
```
RequiresCrossDivisionalAlignment : bool
```
False for tiers 1–3 (same division). True for tiers 4–6 (other division).

### `DonorCandidatesResult` — add one field
```
SurplusSupportFteAvailable : double
```
Pre-computed sum of surplus support FTE across all same-division applications (not just the target). Surplus per app = `max(0, coverage - recommended_support_fte)`.

## `GetDonorCandidatesAsync` logic

Replace the flat Project+MinorChanges same-division query with the six-tier pipeline described in `docs/build/risk-model.md`.

### Tier 1 / Tier 4 — surplus support
- Load all Support mappings for the queried month across all applications in the relevant division(s).
- Group by application. Compute `coverage = sum(percent/100)` for active resources.
- Surplus = `max(0, coverage - recommended_support_fte)`.
- For each resource mapped to a surplus application's Support activity, the donatable FTE is capped at the application's surplus (do not offer more FTE than the surplus allows across all donors on that application).
- Exclude any resource already mapped to the **target** application's Support activity.

### Tier 2 / Tier 5 — Minor Changes
- All Minor Changes mappings for active resources in the relevant division(s).
- Full `percent` is donatable.
- Exclude resources already on the target application's Support activity.

### Tier 3 / Tier 6 — Project
- All Project mappings for active resources in the relevant division(s).
- Full `percent` is donatable.
- Exclude resources already on the target application's Support activity.

### Sort and cumulative fields
- Sort within each tier by `cost_per_fte` ascending (resource monthly rate).
- Concatenate tiers 1–6 in order.
- Compute `CumulativeFte` and `CumulativeGapCoveragePct` as a running total across the full concatenated list.

## CONTEXT.md update
Add `surplus support capacity` and `requires_cross_divisional_alignment` to the glossary (already done in preparation — verify the entries are present before closing the issue).

## Tests — `DataServiceTests.cs`

Follow the existing in-memory context pattern. Seed minimal resources, activities, and mappings. Assert on the `Candidates` list fields — not on EF query internals.

Required cases:
1. Tier 1 candidate appears before tier 2 candidate in `Candidates`.
2. Tier 2 candidate appears before tier 3 candidate.
3. Tiers 1–3 all appear before tiers 4–6.
4. All tier 4–6 rows have `RequiresCrossDivisionalAlignment = true`; tiers 1–3 have `false`.
5. Application at exactly `recommended_support_fte` yields no tier-1 donor; application one percent-point above yields a tier-1 donor with the surplus FTE only.
6. Resource already on the target application's Support activity does not appear in any tier.
7. `CumulativeFte` accumulates correctly across a tier boundary (sum of tier-1 rows + tier-2 rows is correct at the first tier-2 row).
8. `SurplusSupportFteAvailable` equals the sum of surplus FTE across same-division applications.
