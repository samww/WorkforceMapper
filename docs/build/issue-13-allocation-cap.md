# Issue #13 — Hard 100% allocation cap

> Domain glossary, data model, API surface in `CONTEXT.md`.
> Existing mapping CRUD pattern in `docs/build/issue-4-mappings-api.md`.
> Schema constraints table in `docs/build/db-schema.md`.

Reverse ADR 5: the 100% allocation rule becomes a hard, API-enforced cap. Mapping writes against an inactive month are rejected. This slice owns the validator deep module, the controller rejection paths, and the UI feedback.

Prerequisite (already landed via issue #11): `data/mappings.json` has been rewritten so every active resource's `2026-05` total is `<= 100%`. Without that, the cap turning on still loads the seed (cap is enforced at write time, not seed time) but any subsequent edit on Platform Engineering would be impossible.

## Phase A — AllocationValidator (pure module)

`WorkforceMapper.Server/Services/AllocationValidator.cs`. Stateless; no DI dependencies; no I/O.

```csharp
public enum RejectionReason { None, WouldExceedCap, ResourceInactiveInMonth }

public readonly record struct ValidationResult(bool IsValid, RejectionReason Reason);

public static class AllocationValidator
{
    public static ValidationResult ValidateUpsert(
        Resource resource,
        string month,
        IReadOnlyCollection<Mapping> currentMappingsForResourceMonth,
        int activityId,
        double proposedPercent);

    public static ValidationResult ValidateDelete(
        Resource resource,
        string month);
}
```

Cap rule: for the `(resource, month)` snapshot, replace any existing mapping for `activityId` with `proposedPercent`, sum, compare to 100. `> 100` -> `WouldExceedCap`.

Inactive-month rule: applies on both upsert and delete. If `resource.IsActiveIn(month) == false` -> `ResourceInactiveInMonth`.

`ValidateDelete` only checks the inactive-month rule. A delete cannot violate the cap.

### Unit tests (`WorkforceMapper.Tests/AllocationValidatorTests.cs`)

- proposed change brings total to exactly 100% -> valid
- proposed change pushes from 100% to 101% -> `WouldExceedCap`
- existing 50% mapping replaced with 60%, total ends at 110% -> `WouldExceedCap`
- existing 50% mapping replaced with 30%, total ends at 80% -> valid (reduction allowed)
- proposed percent < 0 -> `WouldExceedCap` (or dedicated reason — pick one and stick to it)
- inactive-month upsert rejected with `ResourceInactiveInMonth`
- inactive-month delete rejected with `ResourceInactiveInMonth`
- no-change idempotency: replacing a 70% mapping with 70% leaves total unchanged -> valid

## Phase B — Controller rejection paths

### `PUT /api/mappings`

```
On rejection: 400 Bad Request
Body: { "error": "would_exceed_cap" } or { "error": "resource_inactive_in_month" }
```

Wire validator in `MappingController.Upsert`:
1. Load resource and current mappings for `(resource_id, month)`.
2. Call `AllocationValidator.ValidateUpsert`.
3. On invalid: return 400 with the error code.
4. On valid: persist via existing `DataService.UpsertMappingAsync` and return 200.

### `DELETE /api/mappings`

```
On rejection: 400 Bad Request with { "error": "resource_inactive_in_month" }
On not found: 404
On success: 204
```

Wire validator in `MappingController.Delete`:
1. Load resource.
2. Call `AllocationValidator.ValidateDelete`.
3. On invalid: 400 with the error code.
4. Otherwise: existing 204 / 404 behaviour.

### Existing tests

`MappingApiTests.PutMapping_Returns200_WhenAllocationExceeds100Pct` (from issue #4) is now invalid behaviour. Replace it with the new rejection test below — do not keep the advisory-cap behaviour as a separate path.

### Tests (`WorkforceMapper.Tests/MappingApiTests.cs` extension)

- `PutMapping_Returns400_WithReason_WhenCapExceeded`
- `PutMapping_Returns400_WithReason_WhenResourceInactiveInMonth`
- `PutMapping_Returns200_WhenBringsResourceTo_Exactly_100Pct`
- `PutMapping_Returns200_WhenReducingExistingMapping_BelowCap`
- `DeleteMapping_Returns400_WithReason_WhenResourceInactiveInMonth`
- `DeleteMapping_Returns204_WhenResourceActive_AndMappingExists`

## Phase C — Frontend feedback

### `MappingView.tsx` updates

- Surfaces 400 responses as inline toasts with reason-specific copy:
  - `would_exceed_cap` -> `"Cannot allocate more than 100% to <resource name> in <month>"`
  - `resource_inactive_in_month` -> `"<resource name> is not active in <month>"`
- Client-side guard: prevent submission of an edit/add against an inactive month before sending the request. The API remains the source of truth.
- Existing submit/edit flows stay otherwise unchanged.

### `AllocationBar.tsx` repurpose

- Remove the red over-allocation styling and any branches that handle `> 100%`. The state is no longer reachable.
- Component becomes a neutral "capacity used" bar with green fill from 0–100%.
- Update existing component tests: replace any "renders red when over 100" assertion with "no red branch present".

## Phase D — Inline doc updates

- `docs/build/db-schema.md` — replace the `mappings.percent` row in the constraint table:
  - From: `Advisory only — no DB CHECK; UI shows red over-allocation indicator. API accepts any value.`
  - To: `Hard cap enforced at API layer. PUT/DELETE reject with 400 + structured reason on cap or inactive-month violations (issue #13). No DB CHECK constraint.`
- `docs/build/api-contracts.md` — note the rejection codes on the `PUT /api/mappings` and `DELETE /api/mappings` rows.
- Do not touch `CONTEXT.md` glossary or ADR 5 — those are the doc-updates slice (#15).

## Out of scope

- Per-division allocation caps. Cap is per-resource per-month only.
- Editable thresholds via UI or API.
- Skill-based allocation rules.
- Reframing this in NL through chat — that is part of the chat slice (#14) only insofar as `get_overallocated_resources` is removed.

**Output:** AllocationValidator + 400 rejection paths on PUT and DELETE + MappingView toasts + AllocationBar repurpose.
