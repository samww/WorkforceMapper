# Workforce Mapper

An ASP.NET Core + React application for workforce capacity data maintenance, with an AI layer for natural language capacity reporting.

Pre-loaded with 80 resources (employees, contractors, and vendor staff across 4 divisions), mapped to 33 activities — ready to query immediately. Map resources to project and application activities, and ask plain English questions about capacity utilisation, monthly cost, product attribution, and run/change split.

Companion project to [product-portfolio-rag](https://github.com/samww/product-portfolio-rag): both use the same fictional company (Pragmenta Insights). The workforce allocations here are the operational source behind the application cost figures in that project.

## What it does

**Pre-loaded data** — Pre-loaded with 80 resources (employees, contractors, and vendor staff across 4 divisions), mapped to 33 activities — ready to query immediately.

**Mapping UI** — per-resource cards with inline allocation editing. Division filter, unallocated filter, over-allocation highlighted in red.

**Natural language capacity and cost query** — ask questions over the mapped data. Claude calls structured tools to retrieve capacity, allocation, monthly cost, and product-level cost attribution — no freeform SQL generation.


## background
read all docs in docs/background folder for full context