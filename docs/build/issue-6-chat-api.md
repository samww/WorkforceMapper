# Issue #6 — Chat API: NL query tool-use loop

> Domain context, glossary, tool signatures, and API contracts are in `CONTEXT.md`.
> System prompt verbatim text is in `docs/build/agent-system-prompt.md`.

## Phase 4 — AgentService + SSE endpoint

**Goal:** Tool-use NL query agent using the Anthropic C# SDK with SSE streaming.

### Tests (`WorkforceMapper.Tests/AgentServiceTests.cs`)

- `RunAsync_CallsCorrectTool_WhenCapacityQueryReceived` — mock Claude; verify `get_capacity_by_division` called
- `RunAsync_CallsProductCostTool_WhenProductQueryReceived` — verify `get_cost_by_product` called for product spend questions
- `RunAsync_StreamsTokens_ToCallback` — verify callback invoked with non-empty tokens
- `RunAsync_HandlesMultipleToolCalls_InSingleConversation`

### AgentService (`WorkforceMapper.Server/Services/AgentService.cs`)

Five tools defined using Anthropic C# SDK tool definition format:
- `get_capacity_by_division(division?: string, month?: string)`
- `get_activity_allocations(activity_id?: int, division?: string, activity_type?: string, month?: string)`
- `get_unallocated_resources(division?: string, threshold_pct?: number, month?: string)`
- `get_overallocated_resources(division?: string, threshold_pct?: number, month?: string)`
- `get_cost_by_product(month?: string)`

Tool-use loop: send message → if response contains tool calls, execute via `DataService`, send results back → repeat until no tool calls → stream final text response via `Action<string> onToken` callback.

Use `Anthropic.SDK` — no other AI SDK.

### SSE endpoint

```
POST /api/chat
Body: { query: string, month: string }
Content-Type: text/event-stream
Streams: data: {token}\n\n per token
Final:   data: [DONE]\n\n
```

Use async write patterns throughout — do not require `AllowSynchronousIO`.

Default month: `"2026-05"`.

Run `dotnet test` — all tests green.

**Output:** AgentService, `/api/chat` SSE endpoint, passing tests
