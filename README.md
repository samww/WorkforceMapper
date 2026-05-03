# Workforce Mapper

An ASP.NET Core + React application for workforce capacity data maintenance, with an AI layer for mapping suggestions and natural language capacity reporting.

Upload a headcount extract (employees, contractors, and vendor resources with monthly rates), map resources to project and application activities, and ask plain English questions about capacity utilisation, monthly cost, product attribution, and run/change split. Claude handles both — suggesting mappings on upload and answering capacity and cost questions via tool-use.

Companion project to [product-portfolio-rag](https://github.com/samww/product-portfolio-rag): both use the same fictional company (Pragmenta Insights). The workforce allocations here are the operational source behind the application cost figures in that project.

## What it does

**Excel upload + ETL** — drag-and-drop a `.xlsx` headcount file covering employees, contractors, and vendor resources (with monthly rates and month flags); preview new/updated/unchanged counts before committing.

**AI mapping suggestions** — after upload, Claude reviews each resource's role type, seniority, resource type, and division and suggests which activities to map them to, with confidence scores and reasoning. Suggestions never auto-apply — the user accepts, rejects, or edits.

**Mapping UI** — per-resource cards with inline allocation editing. Division filter, unallocated filter, over-allocation highlighted in red.

**Natural language capacity and cost query** — ask questions over the mapped data. Claude calls structured tools to retrieve capacity, allocation, monthly cost, and product-level cost attribution — no freeform SQL generation.


## background
read all docs in docs/background folder for full context