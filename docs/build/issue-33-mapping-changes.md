# Issue #33 — Pre-rendered `mapping_changes` list on `FewestChangesCombinationSummary`

> Spec for moving per-row mapping-change composition from the agent prompt into the tool result.
> Read `docs/build/issue-31-rounding.md` first — this issue depends on `donable_percent` being integer-consistent.
> Read `docs/build/agent-system-prompt.md` for the current verbatim prompt.

## Problem

The agent system prompt currently instructs the LLM to compose, for every row in the fewest-changes combination, an explicit mapping-change sentence:

> *"For every row where `in_fewest_changes_combination` is true, state the explicit mapping change required: `'[ResourceName]: add [donable_percent]% to [ApplicationName] Support'`. For tier 1 rows (activity_type Support), also note the source mapping must be reduced by the same amount. For tier 2/3 rows (activity_type Minor Changes or Project), the source mapping is fully removed."*

In practice the LLM collapses this to a single example plus a table (see `docs/temp/agent-response-feedback.md`, Issue 3). Composing N sentences from a JSON list while keeping each one accurate is exactly the kind of work LLMs are unreliable at.

## Fix

Move composition into the tool. Add a new field on `FewestChangesCombinationSummary`:

```csharp
public record FewestChangesCombinationSummary(
    int RowCount,
    int DistinctResourceCount,
    double TotalFte,
    decimal TotalMonthlyCost,
    double GapCoverageReachedPct,
    List<string> MappingChanges  // NEW
);
```

`MappingChanges` contains one entry per row in the fewest-changes combination, in the same order as the `candidates` list. Each entry is ready to display verbatim.

### Sentence format

- **Tier 1 (surplus Support, same-division):**
  `"<ResourceName>: add <donable_percent>% to <TargetApplicationName> Support (reduce <SourceActivityName> from <CurrentPercent>% to <CurrentPercent - donable_percent>%)"`

  Example: `"Daniel Evans: add 13% to AuthService Support (reduce CoreDataWarehouse - Support from 55% to 42%)"`

- **Tier 2 (Minor Changes, same-division):**
  `"<ResourceName>: add <donable_percent>% to <TargetApplicationName> Support (remove <SourceActivityName> mapping, currently <CurrentPercent>%)"`

- **Tier 3 (Project, same-division):**
  Same as tier 2 but the source activity will have a `Project` type — `SourceActivityName` already includes the project prefix (e.g. `"P03 DataMigration - Project"`).

- **Tiers 4–6 (cross-divisional):**
  Same formats as tiers 1/2/3 respectively, with an appended cross-divisional warning:
  `"<base sentence> — requires cross-divisional sign-off (<ResourceDivision> → <TargetDivision>)"`

  The base format is identical to the same-division case; only the suffix differs. The `requires_cross_divisional_alignment` flag remains on the row for any consumer that wants to render it differently.

### Source activity name

`DonorCandidateRow.ActivityName` already carries the full source activity name (e.g. `"CoreDataWarehouse - Support"`). Use that directly — do not re-derive.

### Resource division for cross-divisional rows

The current `DonorCandidateRow` does not include the resource's division. Either:
- Add `ResourceDivision: string` to `DonorCandidateRow` (preferred — keeps the data structured for other consumers), or
- Look it up at sentence-composition time from the active mapping (the resource is already loaded via `m.Resource`).

Prefer adding the field — it's cheap and avoids re-querying.

## Prompt update

In `AgentService.BuildSystemPrompt`, replace the entire paragraph starting *"Each row carries `donable_percent`..."* and ending *"...do not convert from FTE yourself."* with:

```
Render fewest_changes_combination.mapping_changes verbatim as a bulleted list — one bullet
per entry, in the order returned. Do not paraphrase, re-order, or omit entries. The sentences
already contain the tier-specific source-change clauses (reduce / remove) and the
cross-divisional warning where applicable.
```

Also remove the corresponding clause about "For tier 1 rows... For tier 2/3 rows..." — that logic now lives in the tool.

After updating `BuildSystemPrompt` in code, copy the full updated prompt verbatim into `docs/build/agent-system-prompt.md` per the existing convention (see issue #30 spec, section "docs/build/agent-system-prompt.md").

## Where to change

- `WorkforceMapper.Server/Models/ServiceResults.cs` — add `MappingChanges` to `FewestChangesCombinationSummary`; add `ResourceDivision` to `DonorCandidateRow`.
- `WorkforceMapper.Server/Services/DataService.cs` — populate `ResourceDivision` in `BuildSurplusSupportTier` and `BuildNonSupportTier` (~lines 242–308). Compose `MappingChanges` immediately after the fewest-changes set is fixed and the largest-remainders pass (#31) has run.
- `WorkforceMapper.Server/Services/AgentService.cs` — update the system prompt and the `get_donor_candidates` tool description.
- `docs/build/agent-system-prompt.md` — replace the donor-candidate paragraph to match.

## Tests — `WorkforceMapper.Tests/DataServiceTests.cs`

Required cases:

1. **Count matches.** `mapping_changes.Count == fewest_changes_combination.row_count`.
2. **Order matches.** For each index `i`, `mapping_changes[i]` contains `candidates[fewest_changes_indices[i]].ResourceName`.
3. **Tier 1 sentence has reduce clause.** A seeded tier-1 row produces a sentence containing `"reduce"` and the arithmetic `"<from>%" → "<from − donable>%"`.
4. **Tier 2 sentence has remove clause.** A seeded tier-2 row produces a sentence containing `"remove"` and the source activity name.
5. **Cross-divisional warning appended.** A seeded tier-4 (or 5/6) row in the fewest-changes set has the sign-off suffix and names both the source and target divisions.
6. **Donable percent is integer.** Sentences contain integer percentages only (no decimal points in the `%` tokens).

Add `WorkforceMapper.Tests/AgentServiceTests.cs` coverage if it currently exercises the donor-candidate prompt path — verify the prompt update is reflected.

## Verification

Run the backend and issue the AuthService query in May 2026. The chat response should contain a bulleted list — one bullet per fewest-changes row — and the LLM should add no per-row commentary beyond the pre-rendered sentences.

## Acceptance criteria

Mirror GitHub issue #33 — see `gh issue view 33` for the canonical checklist.
