# Issue #15 — Doc updates: supersede ADR 5, add ADR 6 + ADR 7, refresh CONTEXT.md

> Domain context in `CONTEXT.md`.
> Risk model rules in `docs/build/risk-model.md`.
> Existing ADRs in `docs/adr/decisions.md`.

Bring the canonical architecture record and shared domain glossary up to date with the implemented capacity-driven risk model and hard allocation cap. Pure documentation slice — no code changes.

## Phase A — ADR 5: mark superseded

In `docs/adr/decisions.md`, prepend the ADR 5 block with a status banner:

```
**Status:** Superseded by ADR 6 (issue #13). The 100% allocation rule is now hard, API-enforced, with structured rejection codes for cap and inactive-month violations.
```

Keep the existing ADR 5 body intact below the banner so the historical reasoning stays readable.

## Phase B — ADR 6: Hard 100% allocation cap, API-enforced

Append after ADR 5. Outline:

- **Decision:** `PUT /api/mappings` and `DELETE /api/mappings` enforce a hard cap. Writes that push a resource above 100% in a given month are rejected with 400 + `would_exceed_cap`. Writes against a month in which the resource is inactive are rejected with 400 + `resource_inactive_in_month`.
- **Why:** Over-allocation is a symptom rather than a tracked operational state once the tool diagnoses risk. Surfacing impossible states weakens trust in the diagnostic frame.
- **Alternatives considered and rejected:**
  - SQLite cross-row CHECK constraints — would require triggers; the seed pipeline is the only other writer, so API enforcement is sufficient.
  - Soft warning UI — keeps the door open for symptom-tracking and contradicts the new premise.
- **Key constraints:**
  - The cap rule and the inactive-month rule are owned by `AllocationValidator` (single source of truth).
  - Seed data is held to the same invariant: every active resource's monthly total is `<= 100%`.

## Phase C — ADR 7: Capacity-driven application risk model

Append after ADR 6. Outline:

- **Decision:** Each application carries `min_support_fte` and `recommended_support_fte` thresholds. Coverage is the sum of `(mapping.percent / 100)` across active resources mapped to the application's Support activity for the month. Risk classification: `coverage < min` -> high; `min <= coverage < recommended` -> medium; `coverage >= recommended` -> low. Product risk = max risk over the transitive closure of (direct apps + their `application_dependencies`). Aggregation rule is max-wins.
- **Why:** Over-allocation as a primary insight is a symptom. The diagnostic question is whether each application has enough support to keep running safely. Thresholds + coverage make that question answerable.
- **Alternatives considered and rejected:**
  - Weighted or count-based product aggregation. Max-wins is unambiguous and matches business framing ("if any critical app is at risk, the product is at risk").
  - Cost-pro-rating across the application dependency graph. Cost stays attributed to direct `product_applications` only; the Unattributed line absorbs foundational infrastructure spend.
  - Skill modelling on resources. Out of scope; demo narrative still references skill match informally.
- **Key constraints:**
  - Risk classification is owned by `RiskCalculator` (single source of truth).
  - Dependency graph traversal is owned by `DependencyGraph`.
  - Cost attribution is unchanged. The Unattributed line behaviour is preserved.

## Phase D — CONTEXT.md glossary

In `CONTEXT.md`, glossary table:

### Add

| Term | Definition |
|---|---|
| **support level** | Per-application thresholds `min_support_fte` and `recommended_support_fte` describing the demand on the engineering team to keep the application running. |
| **capacity coverage** | Sum of `(mapping.percent / 100)` across active resources whose mapping targets an application's Support activity for a month. |
| **application risk** | High / medium / low classification of an application based on capacity coverage relative to its `min_support_fte` and `recommended_support_fte`. |
| **product risk** | Max-wins aggregation of application risk over the transitive closure of (direct apps + their `application_dependencies`). |
| **application dependency** | Directed `(application_id, depends_on_application_id)` edge in `application_dependencies`. Used for risk reach; not used for cost attribution. |
| **donor candidate** | An active resource currently mapped to a Project or Minor Changes activity, surfaced as potential capacity to shift onto a Support activity. Composed by the agent from `get_unallocated_resources` and `get_activity_allocations`. |

### Remove

- The **advisory constraint** row.

## Phase E — CONTEXT.md Key Constraints

Replace the advisory-only bullet:

```
- Advisory-only 100% rule — API returns 200 for any percent value (ADR 5)
```

with:

```
- Hard 100% allocation cap — `PUT /api/mappings` and `DELETE /api/mappings` reject writes that exceed the cap or target a month in which the resource is inactive (ADR 6)
```

Active-flag and tool-use bullets stay unchanged.

## Phase F — CONTEXT.md Data Model + API surface

- Data Model § list: add `application_dependencies` to the entity diagram and bullet list. Add `min_support_fte` and `recommended_support_fte` to the `applications` description. Update the entity counts header from "8 entities" to "9 entities".
- API Surface table: add row `GET /api/applications?month=` and (if introduced in #12) `GET /api/products?month=`. Note rejection codes on the mapping rows.

## Phase G — CLAUDE.md and DEMO_SCRIPT.md

- `CLAUDE.md` issue index: mark #11–#15 as Done after this slice merges; add row for issue #10 (parent PRD) if not already present.
- `DEMO_SCRIPT.md` (if present): rewrite to the new arc — spot it (Applications list -> AuthService red), quantify it (NL ask + dependent product ARR), fix it (move two senior PE resources from P05 ML Forecasting onto AuthService Support, watch the badge flip; demonstrate the cap by attempting to push past 100%). The "Platform Engineering at 112%" and "$18k Data Collection idle bench" lines lose their punchline status — the 112% line dies entirely; the idle bench survives as colour.

## Phase H — Verify

- `grep -rn "advisory" docs/ CONTEXT.md` returns no live references (only the ADR 5 historical body).
- `grep -rn "get_overallocated_resources" docs/ CONTEXT.md` returns no live references.

## Out of scope

- Code changes of any kind.
- Generating new ADRs beyond ADR 6 and ADR 7.
- Editing portfolio-RAG inputs.

**Output:** ADR 5 superseded; ADR 6 + ADR 7 added; CONTEXT.md glossary, key constraints, data model, and API surface refreshed; CLAUDE.md issue index updated.
