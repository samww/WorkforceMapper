# Issue #31 — Largest-remainders rounding for `donable_percent`

> Spec for the `donable_percent` rounding inside `DataService.GetDonorCandidatesAsync`.
> Read `docs/build/risk-model.md` (donor candidate tier section) for context.
> Root cause: the AuthService chat response on 2026-05-13 displayed an internally inconsistent set of `donable_percent` values whose sum (97 pp) did not match the pre-computed `gap_coverage_reached_pct` (103.7 pp). Per-row rounding errors cannot account for the gap (max 5.5 pp for 11 rows), so the displayed integers don't represent a feasible mapping that closes the gap.

## Problem

`donable_percent` is computed independently per row:

```csharp
// DataService.cs BuildSurplusSupportTier (~line 272)
var donablePct = (int)Math.Round(donatableFte * 100.0);
```

`cumulative_gap_coverage_pct` is then computed from the unrounded `AllocationFte`:

```csharp
// DataService.cs (~line 210)
var cumCoverage = gap > 0 ? runningFte / gap * 100.0 : 0.0;
```

So `sum(donable_percent for fewest-changes rows)` can drift from `round(gap_coverage_reached_pct)`. ADR 8 requires integer percents at write time (`PUT /api/mappings`), so the recommendation as displayed is not directly actionable.

## Fix

Apply largest-remainders allocation to the rows in the fewest-changes combination:

1. Compute unrounded `donatable_pct_unrounded` = `AllocationFte * 100` for each fewest-changes row.
2. Take the integer floor for each row's `donable_percent`.
3. Distribute the residual `(target_integer_total - sum_of_floors)` one percentage point at a time to the rows with the largest fractional parts, breaking ties by `AllocationFte` descending (matches existing within-tier ordering).
4. `target_integer_total = round(gap_coverage_reached_pct)` clamped to `[0, 100 * row_count]`.
5. No row's adjusted `donable_percent` may exceed the resource's `Percent` on the source mapping (cannot donate more than they have). If the cap binds, redistribute the leftover to the next-highest fractional row that isn't capped.

For rows **outside** the fewest-changes combination, retain the existing independent rounding — they are informational only.

## Where to change

- `WorkforceMapper.Server/Services/DataService.cs` — `GetDonorCandidatesAsync` (lines ~141–240). The current logic builds `withCumulative`, identifies `stoppingIdx`, then sets `InFewestChangesCombination`. Apply the largest-remainders pass after the fewest-changes set is fixed and before `FewestChangesCombinationSummary` is built.
- No DTO changes required.

## Tests — `WorkforceMapper.Tests/DataServiceTests.cs`

Follow the existing in-memory pattern. Required cases:

1. **Invariant — sum matches headline.** For a seeded scenario with a non-zero gap, `sum(row.DonablePercent where InFewestChangesCombination)` equals `Math.Round(FewestChangesCombination.GapCoverageReachedPct)`.
2. **Cap respected.** No row's `DonablePercent` exceeds its source mapping's `Percent`.
3. **Largest-remainders tie-break.** When two rows have equal fractional parts, the one with larger `AllocationFte` gets the extra percentage point.
4. **No fewest-changes set.** When the gap cannot be closed, no rows have `InFewestChangesCombination = true` and the largest-remainders pass is a no-op (outer rounding remains independent).
5. Existing tier-ordering and exclusion tests continue to pass; any test that asserted a specific `DonablePercent` is reviewed and either updated or expanded with a tolerance note.

## Verification

Run the backend and issue the AuthService query against May 2026:

> *"Why is AuthService at risk this month and where could we find the capacity to fix it?"*

The per-row `donable_percent` values in the response, summed, must equal the cumulative coverage figure cited in the headline (modulo any rendering rounding by the LLM, which should now be zero because the inputs reconcile).

## Acceptance criteria

Mirror GitHub issue #31 — see `gh issue view 31` for the canonical checklist.
