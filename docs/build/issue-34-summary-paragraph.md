# Issue #34 — Pre-rendered `summary_paragraph` on `FewestChangesCombinationSummary`

> Spec for moving the donor-candidate headline composition from the agent prompt into the tool result.
> Read `docs/build/issue-31-rounding.md` first — this issue depends on `donable_percent` and the totals being integer-consistent.
> Read `docs/build/agent-system-prompt.md` for the current verbatim prompt.

## Problem

The agent system prompt asks the LLM to compose the response headline by stitching together several pre-computed fields:

- `fewest_changes_combination.distinct_resource_count` — "the number of distinct people involved"
- `fewest_changes_combination.row_count` — implicitly, the number of changes
- `fewest_changes_combination.total_monthly_cost` — "the total cost of the recommendation"
- `surplus_support_fte_available` — "how much surplus support capacity exists in the same division"
- A "brief explanation of the tier system" whenever the response mentions tiers

In practice the LLM has miscounted (claimed 11 distinct resources for a 9-person solution) and skipped the tier explanation (see `docs/temp/agent-response-feedback.md`, Issues 1 and 4). Multi-field composition into a single coherent paragraph is unreliable.

## Fix

Move composition into the tool. Add a new field on `FewestChangesCombinationSummary`:

```csharp
public record FewestChangesCombinationSummary(
    int RowCount,
    int DistinctResourceCount,
    double TotalFte,
    decimal TotalMonthlyCost,
    double GapCoverageReachedPct,
    List<string> MappingChanges,       // from #33
    string SummaryParagraph             // NEW
);
```

`SummaryParagraph` is a single short paragraph ready to display verbatim. The agent's response leads with this paragraph; the `MappingChanges` bullets (from #33) follow.

### Composition

Assemble in this order:

1. **Change shape.** `"{RowCount} changes across {DistinctResourceCount} {person|people}..."`
2. **Tier scope.** If every fewest-changes row is in the same tier, name it: `"...(all Tier {N}: {tier description})"`. Otherwise: `"...(spanning Tiers {min}–{max})"`. Tier descriptions:
   - Tier 1 / 4: `"surplus Support capacity"` + `"in the same division"` (1) or `"from other divisions"` (4)
   - Tier 2 / 5: `"Minor Changes capacity"` + ditto
   - Tier 3 / 6: `"Project capacity"` + ditto
3. **Totals.** `"Total: {TotalFte:F1} FTE, ${TotalMonthlyCost rounded to nearest $1,000 with k suffix}/month."` Round per the prompt's general rule.
4. **Surplus exhaustion note** (only when applicable — the response includes at least one tier-1 row and the sum of tier-1 donable FTE equals `SurplusSupportFteAvailable` to within 0.01 FTE):
   `"This exhausts the {SurplusSupportFteAvailable:F1} FTE of same-division surplus Support available."`
5. **Tier-system explanation** (always, while the schema returns tiered candidates):
   `"Tier system: tiers 1–3 draw from the same division as {TargetApplicationName} (Tier 1 surplus Support, Tier 2 Minor Changes, Tier 3 Project); tiers 4–6 are the same source types from other divisions and require cross-divisional alignment."`

When there is no fewest-changes solution (gap cannot be closed), `SummaryParagraph` should be `null` — the agent prompt continues to handle this case via fallback text. Alternatively, populate it with a short explanation; document whichever path is chosen and update tests accordingly.

## Prompt update

In `AgentService.BuildSystemPrompt`, remove these instructions (which are now redundant):

- *"Use `fewest_changes_combination.distinct_resource_count` ... do not sum or count the candidates list yourself."*
- *"Use `surplus_support_fte_available` (pre-computed) when stating how much surplus support capacity..."*
- *"When your response mentions tiers, briefly explain the tier system to the user..."* and the four lines describing it.

Replace with:

```
Render fewest_changes_combination.summary_paragraph verbatim as the response headline,
followed by the fewest_changes_combination.mapping_changes bulleted list. Do not recompute
counts, totals, or the tier-system description — they are baked into the paragraph.
```

(This complements the #33 prompt update; both edits can land in the same prompt-update commit.)

Copy the full updated prompt verbatim into `docs/build/agent-system-prompt.md` per the existing convention.

## Where to change

- `WorkforceMapper.Server/Models/ServiceResults.cs` — add `SummaryParagraph` to `FewestChangesCombinationSummary`.
- `WorkforceMapper.Server/Services/DataService.cs` — compose `SummaryParagraph` after `MappingChanges` (from #33) is built. The target application name is already in scope (`application.Name`); `SurplusSupportFteAvailable` is computed earlier in the method.
- `WorkforceMapper.Server/Services/AgentService.cs` — update the system prompt and the `get_donor_candidates` tool description.
- `docs/build/agent-system-prompt.md` — replace the donor-candidate paragraph to match.

## Tests — `WorkforceMapper.Tests/DataServiceTests.cs`

Required cases:

1. **Headline counts.** `SummaryParagraph` contains `RowCount.ToString()` and `DistinctResourceCount.ToString()`.
2. **FTE precision.** Paragraph contains `TotalFte` rounded to one decimal place.
3. **Cost rounding.** Paragraph contains the monthly cost rounded to the nearest $1,000 with a `k` suffix (e.g. `$10k/month`).
4. **Single-tier label.** Seed a fewest-changes set that lies entirely in Tier 1; paragraph contains `"Tier 1"` and not `"Tiers 1–"`.
5. **Multi-tier label.** Seed a fewest-changes set spanning tiers 1 and 2; paragraph contains `"Tiers 1–2"`.
6. **Surplus exhaustion clause.** Seed a tier-1-only solution that fully consumes available surplus; paragraph contains `"exhausts"`.
7. **No exhaustion clause** when surplus is not fully consumed.
8. **Tier explanation always present** while a fewest-changes solution exists. Paragraph names the target application's division (or names the target app — implementer's choice, document it).

Add `WorkforceMapper.Tests/AgentServiceTests.cs` coverage if it currently exercises this prompt path.

## Verification

Run the backend and issue the AuthService query in May 2026. The first paragraph of the chat response should match `SummaryParagraph` verbatim and contain the correct distinct-resource count (9, not 11) and the tier-system explanation.

## Acceptance criteria

Mirror GitHub issue #34 — see `gh issue view 34` for the canonical checklist.
