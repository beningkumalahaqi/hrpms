# Enterprise HR & Project Management System

An internal HR and project-management application built on .NET 8 and SQL Server 2022.
It combines a server-rendered admin dashboard for HR staff with a documented REST API for
system-to-system integration, both reading and writing the same `EnterpriseHRDB` database
through one shared data-access library.

The project is being built in stages against a practical exam specification. See
[Project status](#project-status) for what exists today.

## What it does

| Module | Project | Audience | Technology |
| ------ | ------- | -------- | ---------- |
| HR / admin dashboard | `EnterpriseHR.WebApp` | HR staff and administrators in a browser | ASP.NET Core MVC, Razor Views, HR Workspace design system, Tag Helpers |
| RESTful integration API | `EnterpriseHR.Api` | External systems and integration clients | ASP.NET Core controllers, Swagger/OpenAPI |

Both modules serve from the same `EnterpriseHR.WebApp` process on one origin:
`EnterpriseHR.Api` is a class library of controllers that the host discovers, so the
dashboard lives at `/` and the API under `/api/**`. Both call the same
`EnterpriseHR.DataAccess` project, and business rules live in `EnterpriseHR.Domain`, so the
dashboard and the API apply identical validation and calculation logic. No EF Core — all
access is Dapper over hand-written, parameterized SQL.

## Structure

```
EnterpriseHRSystem.sln
├── src/EnterpriseHR.Domain        # entities, DTOs, requests, exceptions, interfaces
├── src/EnterpriseHR.DataAccess    # Dapper repositories, SQL, transactions
├── src/EnterpriseHR.WebApp        # the only executable host: MVC views + /api/**
├── src/EnterpriseHR.Api           # class library: API controllers, response envelope
├── db/                            # numbered SQL init scripts, run by db-init
├── design-system/                 # exported HR Workspace UI reference
└── docs/                          # long-form documentation
```

`EnterpriseHR.WebApp` is the single runnable application. `EnterpriseHR.Api` is a class
library with no `Program.cs`; the host references it and registers it as an MVC application
part, so its controllers are discovered and served from the same process.

## Quick start

Prerequisites: .NET 8 SDK, Docker (or Docker Engine with the Compose plugin), Git.

```bash
cp .env.example .env            # then set MSSQL_SA_PASSWORD (8+ chars, 3 of 4 character groups)

docker compose up -d            # SQL Server 2022 on localhost:1433
docker compose run --rm db-init # create + seed EnterpriseHRDB

dotnet run --project src/EnterpriseHR.WebApp
```

That is the only process to start. It serves the dashboard, the API and Swagger on one
origin:

| What | URL |
| ---- | --- |
| MVC dashboard | <http://localhost:5100> |
| REST API | <http://localhost:5100/api/**> (e.g. `/api/employees`, `/api/projects`, `/api/reports/department-summary`) |
| Swagger UI | <http://localhost:5100/swagger> |

The URL comes from `src/EnterpriseHR.WebApp/Properties/launchSettings.json`; there is no
second host and no port 5200.

`dotnet build EnterpriseHRSystem.sln` builds with no warnings. SQL Server must be up and
`EnterpriseHRDB` initialized before the host serves database-backed requests.

Full setup — `.env` rules, connection-string overrides, `db-init` verification, reset
steps, and the Apple Silicon caveats — is in
[docs/getting-started.md](docs/getting-started.md).

## Project status

| Stage | Scope | State |
| ----- | ----- | ----- |
| T01–T04 | Repository, Docker SQL Server, schema, stored procedure, seed data | done |
| T05–T06 | Solution and four projects, connection factory and DI wiring | done |
| T07–T10 | Domain entities, DTOs/requests, exceptions, interfaces | done |
| T11–T20 | Repository base, lookups, employee CRUD, salary history, project reads and assignments, department report, services and DI | done |
| T21 | API bootstrap, response envelope, validation, Swagger | done |
| T21.5 | Single host: Api becomes a class library, WebApp serves MVC + `/api/**` + Swagger | done |
| T22 | Global API exception-handling middleware | done |
| T23 | Employee endpoints: paged list and salary change | done |
| T24 | Project endpoints: details and employee assignment | done |
| T25 | Report endpoint: department summary | done |
| T26 | API request collection and scenario matrix | done |
| T27 | WebApp shell: design-system layout, top navigation, alerts, pager, home cards | done |
| T28 | Employees index: search, department filter, include-inactive, paging | done |
| T29 | Employees create and edit forms with dynamic department, position and manager selects | done |
| T30 | Employee deactivation confirmation and salary/promotion adjustment with audit history | done |
| T31 | Projects index: portfolio table with dates, status badges and active team size | done |
| T32+ | Project detail and dashboard screens | pending |

The database, domain layer, data-access foundation, services and the API infrastructure
(envelope, validation mapping, exception mapping, Swagger) are complete, and MVC plus the API
serve from one process. The employee, project and report endpoints are live, and the MVC shell
uses the HR Workspace design system with top navigation, shared alerts, a pager and a home page.
The employee directory is the first real screen: it searches by name or number, filters by
department, includes former staff on request and pages the results. Employees can be created,
edited, deactivated through a confirmation card, and given a raise or promotion that writes a
salary-history row. The project portfolio follows the same pattern: every project with its
department, dates, status badge and active team size, plus a link into its details. What is still
missing: the project detail screen and the dashboard.
The API can be exercised end to end with
[`src/EnterpriseHR.Api/requests.http`](src/EnterpriseHR.Api/requests.http), whose scenarios are
tabulated in [docs/TEST-SCENARIO-API.md](docs/TEST-SCENARIO-API.md).
Stage-by-stage detail is in [docs/development-stages.md](docs/development-stages.md).

Out of scope unless separately requested: authentication and authorization, EF Core or any
migration tool, test projects, CI pipelines, and containerization of the .NET applications.

## Documentation

| Document | Contents |
| -------- | -------- |
| [docs/index.md](docs/index.md) | Index of all documentation. |
| [docs/getting-started.md](docs/getting-started.md) | Prerequisites, local setup, configuration, running the application, troubleshooting. |
| [docs/architecture.md](docs/architecture.md) | Projects, layer responsibilities, data-access rules, design-system contract, scope. |
| [docs/database.md](docs/database.md) | Schema, constraints, stored procedure, seed fixtures. |
| [docs/TEST-SCENARIO-API.md](docs/TEST-SCENARIO-API.md) | API scenario matrix, paired with the runnable `src/EnterpriseHR.Api/requests.http`. |
| [docs/development-stages.md](docs/development-stages.md) | T01–T31 stage log and what comes next. |
| [db/README.md](db/README.md) | Rules for the numbered SQL init scripts. |
| [AGENTS.md](AGENTS.md) | Working rules for coding agents, including where each kind of knowledge belongs. |

`design-system/` holds the exported HR Workspace UI reference (tokens, screens, component
states) and is the visual source of truth for dashboard work. Start at
`design-system/index.html`, then `DESIGN-HANDOFF.md` and `design-system.html`.
