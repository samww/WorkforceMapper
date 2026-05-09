# Issue #17 — Multi-month synthetic data

> Seed data format and intentional demo scenarios in `docs/build/synthetic-data-spec.md`.
> `data/mappings.json` is wiped and re-seeded on every app startup (see `Program.cs`).

Generate plausible mapping data for Jan–Apr and Jun–Dec 2026. May 2026 stays exactly as seeded. All resources remain active all year (no mid-year joiners/leavers). At least one application must be high-risk in every non-May month.

---

## Phase A — Generation script

Create `scripts/generate-monthly-data.ts`. Run with `npx tsx scripts/generate-monthly-data.ts` (add `tsx` as a dev dependency if not present).

### Inputs

- `data/mappings.json` — May 2026 baseline (entries with `"month": "2026-05"`)
- `data/resources.json` — used to get the list of `resource_id` values
- Application support thresholds — hardcode from `WorkforceDbContext.SeedApplications`:
  - AuthService (activity_id for its Support activity): `min_support_fte = 4.0`
  - All thresholds available in `docs/build/issue-17-thresholds.json` (generate this file too, see below)

### Algorithm per non-May month

For each resource in May 2026:
1. Read its current `{ activity_id, percent }` pairs.
2. Apply noise: for each mapping, draw a delta from `[-8, +8]` uniformly, rounded to 1 decimal place.
3. Clamp each percent to a minimum of 1%.
4. Re-normalise the full set to sum to exactly 100% (scale all percents proportionally, then round, then adjust the largest to absorb rounding error).
5. Write the adjusted mappings for the target month.

### High-risk guarantee

After generating each non-May month, calculate the Support FTE coverage for each application (sum of percent/100 for resources mapped to its Support activity). If no application is below its `min_support_fte`, select the application with the smallest coverage gap (closest to going high-risk), find its Support activity mappings, and reduce one of them by enough to push coverage below `min_support_fte` (minimum reduction to cross the threshold, not a dramatic slash). Re-normalise that resource's allocations to 100%.

### Thresholds

Write `scripts/thresholds.json` alongside the script containing each application's `app_id`, `name`, `support_activity_ids`, and `min_support_fte`. Derive Support activity IDs from `data/taxonomy.json` (every application has a Support activity named `"AppName - Support"`). Application thresholds are in `WorkforceDbContext.SeedApplications`.

### Output

Overwrite `data/mappings.json` with May entries (verbatim) + all generated months.

`Program.cs` already wipes and re-seeds `Mappings` on startup — no changes needed there.

---

## Invariants the script must preserve

- Every resource's allocations sum to **exactly 100%** in every month.
- No percent value is < 1%.
- May 2026 data is **byte-for-byte unchanged**.
- At least one application has `coverage_fte < min_support_fte` in every non-May month.
- The hard 100% cap is not violated (re-normalisation ensures this).

---

## Out of scope

No changes to `data/resources.json`, `data/taxonomy.json`, or EF migrations.

**Output:** `scripts/generate-monthly-data.ts`, updated `data/mappings.json` with 12 months of data.
