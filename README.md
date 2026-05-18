# Workforce Mapper

An ASP.NET Core + React application for workforce capacity data maintenance, with an AI layer for natural language capacity reporting.

Pre-loaded with 80 resources (employees, contractors, and vendor staff across 4 divisions), mapped to 33 activities — ready to query immediately. Map resources to project and application activities, and ask plain English questions about capacity utilisation, monthly cost, product attribution, and run/change split.

Companion project to [product-portfolio-rag](https://github.com/samww/product-portfolio-rag): both use the same fictional company (Pragmenta Insights). The workforce allocations here are the operational source behind the application cost figures in that project.

## What it does

**Pre-loaded data** — Pre-loaded with 80 resources (employees, contractors, and vendor staff across 4 divisions), mapped to 33 activities — ready to query immediately.

**Mapping UI** — per-resource cards with inline allocation editing. Division filter, unallocated filter, over-allocation highlighted in red.

**Natural language capacity and cost query** — ask questions over the mapped data. Claude calls structured tools to retrieve capacity, allocation, monthly cost, and product-level cost attribution — no freeform SQL generation.


## Demo auth toggle

Two `workflow_dispatch` workflows in the Actions tab let the maintainer flip Easy Auth on and off during live demos:

- **Demo - disable auth (allow anonymous)** — sets the unauthenticated-client action to `AllowAnonymous` so attendees can open the app URL without an Entra sign-in prompt.
- **Demo - enable auth (require sign-in)** — restores `RedirectToLoginPage`.

Note: "off" is `AllowAnonymous`, not a full disable — Easy Auth and the Entra app registration remain configured throughout. Signed-in users continue to receive `X-MS-CLIENT-PRINCIPAL` headers.

One-time setup: run `scripts/setup_demo_auth_toggle.ps1` to create the OIDC service principal and add the required GitHub Secrets (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`).

## background
read all docs in docs/background folder for full context