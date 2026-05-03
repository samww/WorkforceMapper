# Issue #7 — Chat UI: SSE chat panel

> Domain context and glossary are in `CONTEXT.md`.
> Chat API contract (`POST /api/chat`, SSE format) is in `CONTEXT.md`.

## Phase 5b — Frontend: chat panel

**Stack:** React 19, TypeScript, Vite, Tailwind CSS v4, Vitest, React Testing Library. Dark mode.

### Components

`ChatPanel.tsx`:
- Query input textarea with submit button
- Four starter question chips (empty state)
- SSE consumer: POST via fetch with streaming to `/api/chat`
- Tokens appended to response display as they arrive

`ToolCallIndicator.tsx`:
- Rendered while tools are executing
- Text: `"Checking capacity by division…"`

### Tests (`ChatPanel.test.tsx`)

- Chips call submit with correct query string
- Response area renders streamed text

Run `npx vitest run` — all tests green.

**Output:** Working chat panel with SSE streaming
