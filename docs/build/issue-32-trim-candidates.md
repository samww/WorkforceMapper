# Issue #32 — Trim `get_donor_candidates` response

> Spec for trimming the `candidates` list returned by `DataService.GetDonorCandidatesAsync`.
> Read `docs/build/risk-model.md` (donor candidate tier section) for context.
> Independent of #31, #33, #34 — can be implemented in parallel.

## Problem

`GetDonorCandidatesAsync` returns every active resource that qualifies for any of tiers 1–6. For AuthService in May 2026 this is dozens of rows; for any application with a wide donor pool it can exceed 100. The agent (Haiku) only consumes the fewest-changes rows plus a small amount of context, but every other row inflates the tool-result token count and tempts the LLM to re-aggregate the list.

## Fix

Trim the returned `Candidates` list before serialization:

- Always include every row where `InFewestChangesCombination` is `true`.
- Plus a fixed buffer of the next `BufferSize` rows beyond the fewest-changes cutoff (in the existing tier-then-FTE order), to give the agent visibility into runner-up options.
- If no fewest-changes solution exists (gap cannot be closed by any combination), return the top `BufferSize * 2` rows across all tiers so the agent can still narrate.

Add a new top-level field on `DonorCandidatesResult`:

```csharp
int TotalAvailableCandidatesCount
```

= the unbuffered count: every active resource that would have qualified for any tier before trimming. The agent can use this to say *"11 changes chosen from 87 candidates considered"*.

Buffer size lives in a single named constant in `DataService`:

```csharp
private const int DonorCandidateBufferRows = 5;
```

## Where to change

- `WorkforceMapper.Server/Services/DataService.cs` — `GetDonorCandidatesAsync`. The trimming happens after `candidates` is fully populated and `InFewestChangesCombination` is set, just before constructing `DonorCandidatesResult`.
- `WorkforceMapper.Server/Models/ServiceResults.cs` — add `TotalAvailableCandidatesCount` to `DonorCandidatesResult`.
- `WorkforceMapper.Server/Services/AgentService.cs` — extend the `get_donor_candidates` tool description in `BuildTools` to mention `total_available_candidates_count`. Prompt changes are otherwise minimal; the existing instructions to "do not count the list yourself" still apply (and now there are two counts: the trimmed list, and the pre-computed total).

## Tests — `WorkforceMapper.Tests/DataServiceTests.cs`

Required cases:

1. **Trimming happens.** Seeded scenario with more candidates than `fewest_changes_combination.row_count + DonorCandidateBufferRows`. Assert `candidates.Count == fewest_changes_combination.row_count + DonorCandidateBufferRows`.
2. **Fewest-changes rows preserved.** Every row that would have been `InFewestChangesCombination = true` in the untrimmed result is still present.
3. **Total reflects untrimmed count.** `TotalAvailableCandidatesCount` equals the count returned by the pre-trim implementation on the same seed.
4. **No fewest-changes path.** Seed a scenario where the gap exceeds total available capacity. Assert that the response still includes the top `DonorCandidateBufferRows * 2` rows and no row has `InFewestChangesCombination = true`.
5. **Small donor pool.** When the total candidate count is below the buffer threshold, no trimming occurs; `candidates.Count == TotalAvailableCandidatesCount`.

## Verification

Run the backend and issue the AuthService query in May 2026. Inspect the raw `get_donor_candidates` tool result (logged on the server or via a probe in `AgentService.DispatchAsync`). The candidates array should be no longer than `fewest_changes_combination.row_count + 5`, and `total_available_candidates_count` should be larger than the array length.

## Acceptance criteria

Mirror GitHub issue #32 — see `gh issue view 32` for the canonical checklist.
