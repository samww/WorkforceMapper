# Architecture Decision Records

Design decisions for `workforce-mapper`. Intended for:
- Interview conversations where an interviewer asks "why did you choose X?"
- README reference to show architectural thinking, not just implementation
- The `docs/decisions/` directory in the repo

---

## ADR 1: SQLite with EF Core over Azure SQL or PostgreSQL

**Status:** Accepted

**Context:**  
The application needs a relational database to store people, activities, and mappings. The obvious production choices would be Azure SQL (managed, scalable, integrated with the Azure stack on the CV) or PostgreSQL. Both would be more credible from a production standpoint.

**Decision:** Use SQLite as the database, with Entity Framework Core as the data access layer.

**Reasons — SQLite:**
- SQLite runs inside the container with zero additional Azure resources. Azure SQL Basic tier costs ~$5/month minimum; Azure SQL S0 (the first tier with meaningful performance) is ~$15/month. For a demo that may sit idle between interviews, this is unnecessary spend.
- The repo is immediately runnable with `docker compose up` and a single API key — no Azure subscription required for a reviewer to run the project themselves.
- The data model is small (eight entities, simple joins) and the dataset is modest (80 resources, 33 activities, 18 applications, 7 products). SQLite handles this without any meaningful limitation.

**Reasons — Entity Framework Core:**
- EF Core is the idiomatic data access layer for ASP.NET Core applications. Using it is the natural choice for a C# developer and matches what a hiring team would expect to see in a well-structured .NET project.
- Migrations are a first-class feature: the schema history is committed to the repo as migration files, making the data model visible and reviewable without reading the application code. `context.Database.Migrate()` at startup keeps the deployed instance in sync automatically.
- LINQ queries are type-safe: refactoring entity property names produces a compiler error rather than a runtime SQL failure. This matters for a project where future changes (e.g. adding a `skills` field to `Person`) should be caught at build time.
- EF Core's navigation properties (`.Include(p => p.Mappings).ThenInclude(m => m.Activity)`) express joins in a way that is readable and avoids hand-written SQL for straightforward relationship traversal.
- The InMemory provider (`Microsoft.EntityFrameworkCore.InMemory`) enables fast, isolated unit tests without spinning up a SQLite file — the same DbContext configured differently in test vs production.

**Mitigation — Azure SQL:** Switching the database would mean replacing the SQLite connection string and EF Core provider package with the Azure SQL equivalents. The entity classes, migrations, and LINQ queries are otherwise database-agnostic — this is one of EF Core's primary advantages. The ADR documents this boundary explicitly.

**Trade-offs accepted:**
- SQLite has no concurrent write support — acceptable for a single-user demo.
- Data is stored in a file on the container filesystem. The `docker-compose.yml` mounts a named volume to persist the SQLite file between restarts, but data is lost if the volume is deleted. For a demo this is acceptable; for production, persistent block storage or a managed database would be required.
- EF Core adds some query overhead vs raw SQL for complex aggregations (e.g. the capacity utilisation calculation). For this dataset size the difference is immaterial. Where EF Core's LINQ translation is awkward, the queries can be expressed as raw SQL via `context.Database.SqlQuery<T>()` while keeping the rest of the data layer idiomatic.
- Would not use SQLite for a multi-user production system.

---

## ADR 2: AI suggestions require human confirmation — no auto-apply

**Status:** Accepted

**Context:**  
After a headcount upload, Claude generates mapping suggestions: which activities each person should be mapped to, with confidence scores and reasoning. A simpler implementation would automatically apply high-confidence suggestions (e.g. confidence ≥ 0.9) without user review.

**Decision:** Suggestions are always presented for review. The user accepts, rejects, or edits each one. No suggestion is ever written to the `mappings` table without an explicit user action.

**Reasons:**
- The consequence of an incorrect mapping is significant. Allocation percentages feed into capitalisation calculations (which activities are capitalised vs expensed affects EBITDA), workforce cost reporting, and headcount-to-budget reconciliation. An incorrect mapping silently propagates into financial reporting.
- The model's confidence score is not a guarantee of correctness. A resource with "Senior Java Developer" in their role type and a division of "Platform Engineering" may have a legitimate reason to be mapped to a Client Services project — context that Claude cannot infer from the upload data alone.
- The mapping process is the point where human domain knowledge adds value. A reviewer knows that Sarah Mitchell is on secondment to the Data Collection programme, even though her division field says Platform Engineering. Auto-apply removes that correction opportunity.
- This matches real-world practice: recommendation systems in consequential domains (credit, medical, financial) present recommendations for review rather than acting autonomously.
- In interview, the "why not auto-apply?" question tests whether you understand the trust model of AI in enterprise workflows. This ADR documents the reasoning explicitly.

**Trade-offs accepted:**
- The upload flow requires an extra confirmation step for each suggestion. For 80 people uploading monthly, this is a few minutes of review — acceptable given the consequence of errors.
- "Accept all high-confidence" button (confidence ≥ 0.8) provides efficiency for bulk acceptance when the user has reviewed enough to trust the model's suggestions for a given upload.
- The suggestion quality improves over time as users build familiarity with the model's reasoning patterns — but that improvement is achieved through trust, not by removing oversight.

---

## ADR 3: Structured tool-use over freeform NL-to-SQL

**Status:** Accepted

**Context:**  
The natural language query feature needs to translate user questions into database queries. Two approaches were considered:

**Option A — Freeform NL-to-SQL (not chosen):** Give Claude the database schema and ask it to generate SQL, execute the query, and format the result. This is the pattern used by many "chat with your data" products.

**Option B — Structured tool-use (chosen):** Define five specific tools with typed parameters (`get_capacity_by_division`, `get_activity_allocations`, `get_unallocated_resources`, `get_overallocated_resources`, `get_cost_by_product`). Claude calls tools to retrieve data; the tool implementations are EF Core queries in `DataService.cs`.

**Decision:** Option B — structured tool-use.

**Reasons:**
- **Security:** NL-to-SQL exposes the entire database schema to the model and relies on prompt constraints to prevent destructive or exfiltrating queries. Tool-use exposes only the four read operations defined; Claude cannot execute arbitrary SQL.
- **Predictability:** The tool response schema is typed and deterministic. A `get_capacity_by_division` call always returns the same structure: `division`, `resource_count`, `resource_type_split`, `monthly_cost`, `allocated_monthly_cost`, `unallocated_monthly_cost`, `allocation_pct`. `get_overallocated_resources` always returns a typed list with `total_allocation_pct` and `overallocation_pct`. NL-to-SQL output shape varies with query complexity, making the frontend rendering logic fragile.
- **Testability:** Each tool is a C# method with a defined signature. Tests can verify that specific questions trigger specific tool calls with specific parameters. NL-to-SQL is harder to test because the intermediate SQL is model-generated and varies.
- **Reliability:** Freeform SQL generation fails on aggregations, multi-table joins, and ambiguous column names. The four tools encapsulate the meaningful aggregations that the UI needs — the underlying SQL is written once by a developer, not regenerated per query.
- **Interview clarity:** Being able to say "I defined five tools with typed parameters, and Claude calls them" is a cleaner explanation than "Claude generates SQL and I execute it." The tool-use pattern also evidences understanding of the Anthropic tool-use API specifically.

**Trade-offs accepted:**
- The tool surface area is fixed at five operations. A user asking something outside those five operations (e.g. "which resources have been at Pragmenta for more than two years?") cannot be answered — the `resources` table has no tenure field, so this is a data limitation rather than a tool limitation.
- `get_cost_by_product` includes an explicit `"Unattributed"` line for mapped cost that cannot be traced to any product (e.g. DevPortal Rebuild — internal developer tooling with no `project_products` entries). This ensures the product view sums to total workforce spend; without it, infrastructure overhead would be silently excluded.
- Adding a new analytical dimension requires adding a new tool and its underlying EF Core query, then redeploying. This is more structured than NL-to-SQL but less flexible.
- All five tools accept an optional `month` parameter defaulting to the currently selected month. This enables cross-month comparisons ("what was the cost in April?") without exposing arbitrary SQL — Claude passes the month string and the tool handles the query.

---

## ADR 5: Advisory-only 100% allocation constraint — no API enforcement

**Status:** Accepted

**Context:**  
Each resource should have mappings that sum to exactly 100% per month for a fully-allocated resource. The constraint could be enforced at three levels:

- **Database** — a CHECK constraint on the `mappings` table
- **API** — `PUT /api/mappings` rejects a save that would push a resource's total above 100% or below the intended floor
- **UI only** — the API accepts any value; the UI surfaces violations visually (red over-allocation indicator, unallocated filter)

**Decision:** Enforce at the UI layer only. The API accepts any `percent` value without validation against the resource's current total allocation.

**Reasons:**
- Over-allocation is a valid operational state, not an error. Platform Engineering runs at ~112% — resources are legitimately mapped to more work than their available capacity because the business has prioritised delivery over headcount. Rejecting saves that exceed 100% would prevent users from recording this reality.
- The mapping editor is an incremental workflow. A user building up a resource's allocations across several activities will pass through intermediate states (e.g. 60%, then 80%, then 100%) where the total is not yet 100%. API-level enforcement would require all mappings to be submitted atomically, which conflicts with the inline single-mapping save pattern.
- The seeded `data/mappings.json` is the authoritative clean-state reference — every active resource in `2026-05` sums to exactly 100%. The constraint is maintained by design of the seed data, not by runtime enforcement.
- Database-level CHECK constraints on aggregate values (sum across rows) are not supported in SQLite without triggers, which add complexity for no meaningful benefit given the UI already surfaces the condition.

**Where violations surface:**
- `AllocationBar` renders over-allocation in red when `total_allocation_pct > 100`
- Unallocated filter shows resources with `total_allocation_pct < 100`
- `get_unallocated_resources` and `get_overallocated_resources` NL query tools expose both conditions to Claude

**Trade-offs accepted:**
- The API will accept logically inconsistent data (e.g. a resource mapped to 200% across activities). The UI is the only guard. For a single-user demo this is acceptable; a multi-user production system would add API-layer validation.
- Partial saves during an editing session produce transient under-allocation that is visible in the UI until the user completes their edits. This is preferable to blocking saves mid-flow.
- For this dataset and use case, the four tools cover the meaningful capacity questions. If the data model grew significantly (e.g. adding skills, tenure, salary bands), additional tools would be added.

---

## ADR 4: ASP.NET Core and C# over Python or Node.js

**Status:** Accepted

**Context:**  
The companion project `product-portfolio-rag` uses a Python FastAPI backend. Using the same backend stack for `workforce-mapper` would be faster to build and more consistent across the portfolio. Both Python and Node.js are well-supported for the AI SDK integrations required.

**Decision:** Use ASP.NET Core 8 Minimal API with C#.

**Reasons:**
- The CV explicitly lists C# as a backend skill. A Python-only portfolio would not evidence this claim; a second Python project would add quantity but not breadth.
- The two projects are designed to demonstrate different patterns: `product-portfolio-rag` is Python + RAG + vector search; `workforce-mapper` is C# + CRUD + tool-use. A technical interviewer reviewing both projects sees a wider skill set than two instances of the same stack.
- ASP.NET Core Minimal API is deliberately chosen over the full MVC pattern: it is idiomatic for small-to-medium APIs, and the endpoint definitions in `Program.cs` are readable without framework knowledge.
- The Anthropic C# SDK (`Anthropic.SDK`) is a production-quality client library with full support for tool-use and streaming. There is no meaningful technical reason to prefer Python for the AI integration.
- The choice is itself an interview talking point: "I deliberately used a different stack for each project to demonstrate breadth — Python/FastAPI for the RAG project, C#/ASP.NET Core for the data pipeline and CRUD application. The architectural decisions in each project reflect the stack conventions of that ecosystem."

**Trade-offs accepted:**
- The build pipeline is more complex: the Dockerfile stages a Node build for the React frontend alongside the .NET publish step, rather than the simpler Python/Node pattern used in `product-portfolio-rag`.
- C# verbosity means slightly more code than an equivalent Python implementation, though Minimal API keeps this manageable.
- The Anthropic C# SDK has a smaller community and fewer examples than the Python SDK. The tool-use implementation required closer reading of the SDK documentation and source.
