# Issue #55 - iPhone 16 Pro Max mobile navigation and Applications table

> GitHub issue: https://github.com/samww/WorkforceMapper/issues/55
> Frontend-only, no domain-model changes.

## What to build

Make the application shell usable on iPhone 16 Pro Max by replacing the top tab navbar with a mobile-only hamburger drawer and by tightening the Applications table so it fits without horizontal pressure.

On mobile widths below `640px`, the top-level section tabs should collapse into a top-left hamburger button. Opening it shows a left slide-in drawer with the four top-level destinations: Mappings, Applications, Products, Projects. Selecting a destination closes the drawer and resets detail-screen state exactly like the current desktop tab buttons.

At `640px` and above, keep the existing desktop tab bar. Do not move the global Month and Division filters into the drawer. Do not move `Ask AI` into the drawer.

On the Applications screen below `640px`, hide the `Division` table header and cells and reduce table cell padding. Keep the existing global Division filter as the mobile way to work by division. There should be no visible mobile sort control for the hidden Division column.

## Decisions already made

- Scope is the tab navbar only. `Ask AI`, Month, and Division remain visible global controls.
- The hamburger pattern is mobile-only below Tailwind's `sm` breakpoint (`640px`).
- The mobile menu is a slide-in drawer from the left, not a dropdown.
- Drawer width should be `min(20rem, 85vw)`.
- Drawer closes when the user taps a destination, taps the backdrop, presses Escape, or taps the close button inside the drawer.
- Destination taps reset `selectedApp`, `selectedProduct`, `productBackApp`, and `appBackProduct`, matching current desktop tab-switch behaviour.
- The Applications table remains a table on mobile. Hide `Division`; do not convert rows into cards.
- Hide both the `Division` header and cells below `640px`. Do not expose a hidden Division sort control on mobile.
- Reduce mobile table cell padding below `640px`; preserve desktop spacing.

## Acceptance criteria

- [x] At viewport widths below `640px`, the tab buttons are not visible and a top-left hamburger button is visible.
- [x] At viewport widths `640px` and above, the existing tab bar remains visible.
- [x] The hamburger button opens a left slide-in drawer with Mappings, Applications, Products, and Projects.
- [x] The drawer uses a width equivalent to `min(20rem, 85vw)`.
- [x] Tapping a drawer destination switches sections, closes the drawer, and clears detail-screen back-link state in the same way as the current tab buttons.
- [x] The drawer closes via backdrop click, Escape, and an in-drawer close button.
- [x] Drawer controls have touch targets suitable for mobile use, at least 44px high.
- [x] `Ask AI`, Month, and Division remain outside the drawer and visible as global controls.
- [x] On the Applications screen below `640px`, the `Division` column header and cells are hidden.
- [x] On the Applications screen below `640px`, there is no visible Division sort button.
- [x] The Applications table uses reduced horizontal cell padding below `640px`.
- [x] On desktop widths, the Applications `Division` column and existing sort behaviour remain unchanged.
- [x] Tests cover mobile drawer open/close behaviour, section switching through the drawer, desktop tab visibility, and responsive `Division` column classes or behaviour.
- [x] A manual check at an iPhone 16 Pro Max-sized viewport confirms the Applications table fits without horizontal scrolling caused by the hidden `Division` column.

## Implementation summary

- `App.tsx` now uses `switchTopLevelSection` for desktop tabs and mobile drawer destinations so top-level navigation always clears detail-screen and back-link state.
- The mobile drawer is `sm:hidden`, labelled as `Section navigation`, and closes by destination tap, backdrop click, Escape, or the in-drawer close button.
- The desktop tab bar remains `hidden sm:flex`; the hamburger is `sm:hidden`.
- Header controls use balanced vertical padding and 44px minimum touch targets so the hamburger, tabs, and `Ask AI` align vertically.
- `ApplicationsView.tsx` keeps the table layout, hides the `Division` column with `hidden sm:table-cell`, and uses mobile-only reduced horizontal cell padding.
- `App.test.tsx` and `ApplicationsView.test.tsx` cover drawer behavior, section switching/reset, desktop/mobile visibility classes, global controls staying outside the drawer, and responsive Applications table classes.

## Suggested implementation notes

- Keep the top-level tab union and section-switching logic in `App.tsx`.
- Extract the repeated "switch top-level section and clear detail state" action into a shared handler before wiring both desktop tabs and mobile drawer destinations.
- Use responsive classes for visibility, for example desktop tabs at `hidden sm:flex` and mobile controls at `sm:hidden`.
- Preserve the existing current-section styling for desktop tabs.
- For accessibility, expose the hamburger as `aria-label="Open navigation"` and the drawer as a labelled navigation/dialog region.
- Apply mobile-only `hidden sm:table-cell` style to the Applications `Division` header and cells.
- Prefer responsive table cell padding classes over global table CSS changes so unrelated tables are not affected.

## Not in scope

- Reworking Month or Division filters into mobile-specific controls.
- Moving or restyling the chat panel beyond keeping `Ask AI` available.
- Converting Applications rows into cards.
- Changing Product, Project, Mapping, Application detail, or Product detail table layouts.
- Changing any API, backend, or domain logic.
