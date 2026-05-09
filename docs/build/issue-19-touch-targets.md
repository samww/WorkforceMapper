# Issue #19 — Mobile touch targets on mapping cards

> No domain knowledge required. Purely a CSS change in `MappingView.tsx`.

The edit (✎) and delete (✕) icon buttons in `ResourceCard` have no padding, making them too small to tap on an iPhone 16 Pro Max. Apple HIG specifies 44×44pt as the minimum touch target.

---

## Change

In `MappingView.tsx`, find the two icon buttons inside the non-editing mapping row (around lines 320–333):

```tsx
<button
  onClick={() => startEdit(m)}
  className="text-blue-400 hover:text-blue-300 mr-1"
  aria-label={`Edit ${m.activity_name}`}
>
  ✎
</button>
<button
  onClick={() => handleDelete(m.activity_id)}
  className="text-red-400 hover:text-red-300"
  aria-label={`Remove ${m.activity_name}`}
>
  ✕
</button>
```

Replace the `className` on both buttons to add padding and enforce a minimum touch target:

```tsx
className="text-blue-400 hover:text-blue-300 p-2 -m-2 mr-1"
// and
className="text-red-400 hover:text-red-300 p-2 -m-2"
```

`p-2` adds 8px of padding on all sides (total 32px tap area with the icon). The negative margin `-m-2` compensates so the visual layout of the row is unchanged. If 32px is still marginal, use `p-2.5` / `-m-2.5` for a 40px target, or `p-3` / `-m-3` for 44px.

---

## Out of scope

- No changes to the Save/Cancel buttons in the editing state — those are text buttons with adequate tap area already.
- No changes to other views.

**Output:** Edit and delete buttons on mapping rows have comfortable tap targets on mobile.
