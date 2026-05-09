# Issue #20 — Demo-aligned query chips

> Demo scenarios in `docs/background/DEMO_SCRIPT.md`.

Replace the four query chips in `ChatPanel.tsx` with prompts that match the demo narrative.

---

## Change

In `ChatPanel.tsx`, replace the `CHIPS` constant (around line 6):

```ts
const CHIPS = [
  'Which divisions have unallocated monthly cost this month?',
  'How much are we spending on the AuthService Replacement project per month?',
  'Which resources are over-allocated this month?',
  'What is the monthly cost attribution by product?',
];
```

With:

```ts
const CHIPS = [
  'Why is AuthService at risk this month and where could we find the capacity to fix it?',
  'What unallocated resource cost does Data Collection carry this month?',
  'What is the monthly cost attribution by product?',
  'Which division is most constrained on capacity this month?',
];
```

No other changes to `ChatPanel.tsx`.

---

## Out of scope

- No changes to the streaming logic, markdown renderer, or tool-call indicator.
- No changes to the chip rendering UI — same button style as before.

**Output:** Four updated query chips aligned with the demo script scenarios.
