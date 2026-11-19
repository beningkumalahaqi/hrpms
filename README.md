# Enterprise HR & Project Management System

An internal HR and project-management application built on .NET 8 and SQL Server 2022. It
combines a server-rendered admin dashboard for HR staff with a documented REST API for
system-to-system integration, both reading and writing the same `EnterpriseHRDB` database
through one shared data-access library.

The application is feature-complete: employees, projects, project assignments, salary history
and the department budget report all work through the MVC screens and the API. See
[Project status](#project-status) for what each stage delivered.

## What it does

| Module | Project | Audience | Technology |
| ------ | ------- | -------- | ---------- |
| HR / admin dashboard | `EnterpriseHR.WebApp` | HR staff and administrators in a browser | ASP.NET Core MVC, Razor Views, HR Workspace design system, Tag Helpers |
| RESTful integration API | `EnterpriseHR.Api` | External systems and integration clients | ASP.NET Core controllers, Swagger/OpenAPI |

Both modules serve from one `EnterpriseHR.WebApp` process on one origin: `EnterpriseHR.Api` is
a class library of controllers that the host discovers, so the dashboard lives at `/` and the
API under `/api/**`. Both call the same `EnterpriseHR.DataAccess` project, and the business
rules live in `EnterpriseHR.Domain`, so the dashboard and the API apply identical validation
and calculation logic.

## Technology stack

| Area | Technology |
| ---- | ---------- |
| Language | C# 12 |
| Runtime / framework | .NET 8 (`net8.0`) |
| Web application | ASP.NET Core MVC with Razor Views and Tag Helpers, styled by the HR Workspace design system |
| REST API | ASP.NET Core Web API, controller-based endpoints, Swagger/OpenAPI, global exception-handling middleware |
| Hosting | One process, `EnterpriseHR.WebApp`, serving MVC at `/` and the API at `/api/**`; `EnterpriseHR.Api` is a class library |
| Database | Microsoft SQL Server 2022, database `EnterpriseHRDB`, run locally with Docker Compose |
| Data access | Dapper (asynchronous methods only) with `Microsoft.Data.SqlClient` |
| Database programming | Parameterized SQL, `IDbTransaction`, stored procedures, Dapper multi-mapping, `QueryMultipleAsync` |
| Architecture | One solution, `EnterpriseHRSystem.sln`, with exactly four projects |
| Frontend | Server-rendered Razor Views styled from the exported HR Workspace design system in `design-system/` (custom CSS tokens, IBM Plex Sans). No Bootstrap, no SPA framework, no separate frontend app |
| Local infrastructure | Docker Compose |
| Documentation | Markdown README and `docs/` |

Entity Framework Core is deliberately **not** used. All database access goes through Dapper
with hand-written, parameterized SQL.

## Projects

`EnterpriseHRSystem.sln` contains exactly four projects. There is no fifth project, no
separate services project and no reverse proxy: the orchestration services live in
`EnterpriseHR.DataAccess/Services/`, next to the repositories they call.

| Project | Responsibility | Depends on |
| ------- | -------------- | ---------- |
| `EnterpriseHR.Domain` | Entities, enums, DTOs, request types, domain validation and calculation rules, exception hierarchy, and the repository/service interfaces. No database or web dependencies. | none |
| `EnterpriseHR.DataAccess` | Dapper repositories, thin orchestration services, parameterized SQL, stored-procedure calls, mapping, transaction handling, and DI registration. Owns the `Microsoft.Data.SqlClient` connection factory and connection-string configuration. | Domain |
| `EnterpriseHR.WebApp` | The only executable application: ASP.NET Core MVC host with Razor Views, view models, controllers, Tag Helpers, the design-system layout, Swagger, and the JSON/validation configuration. Registers `EnterpriseHR.Api` as an MVC application part so its controllers are served from this process. | Domain, DataAccess, Api |
| `EnterpriseHR.Api` | Class library, not a host: API controllers deriving from `ApiControllerBase`, the `ApiResponse<T>` envelope, and API-specific middleware (`ExceptionHandlingMiddleware`). No `Program.cs`, no `appsettings`, no launch profile. | Domain, DataAccess |

`EnterpriseHR.DataAccess` is the single place where SQL is written. Neither host opens a
`SqlConnection` directly, which is what keeps the MVC dashboard and the API consistent as the
schema evolves. Multi-table writes (salary adjustment with its history row, employee
assignment, deactivation) run inside a transaction owned by DataAccess, so a rejected
operation leaves no partial write.

## Repository structure

```
.
├── docker-compose.yml          # SQL Server 2022 + db-init
├── .env.example                # template for local secrets
├── EnterpriseHRSystem.sln      # solution, four projects
├── db/                         # numbered SQL init scripts, run by db-init
│   ├── 01_schema.sql           #   EnterpriseHRDB, six tables, keys, indexes
│   ├── 02_sp_GetDepartmentSummary.sql
│   └── 03_seed.sql             #   fixture: 4 departments, 10 employees, 6 projects
├── design-system/              # exported HR Workspace UI reference
├── docs/                       # long-form documentation
├── src/
│   ├── EnterpriseHR.Domain/
│   ├── EnterpriseHR.DataAccess/
│   ├── EnterpriseHR.WebApp/
│   └── EnterpriseHR.Api/
└── README.md
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet --version` should report `8.x`)
- Docker Desktop, or Docker Engine with the Compose plugin (`docker compose version`)
- Git
- A SQL client for inspecting the database (optional): `sqlcmd`, Azure Data Studio, or DBeaver

On Apple Silicon, read [Troubleshooting](#troubleshooting) before starting the containers; the
SQL Server image is published for `linux/amd64` only.

## Getting started

```bash
git clone <repository-url> EnterpriseHRSystem
cd EnterpriseHRSystem
cp .env.example .env
```

Open `.env` and set `MSSQL_SA_PASSWORD` to a password that satisfies SQL Server complexity
rules: at least 8 characters, using three of uppercase, lowercase, digits, and symbols.
`.env` is ignored by Git; never commit it and never put a real password in a tracked file.

Start SQL Server:

```bash
docker compose up -d
```

The first run pulls `mcr.microsoft.com/mssql/server:2022-latest` and takes a few minutes. Wait
until Docker reports the container as healthy:

```bash
docker compose ps
```

SQL Server listens on `localhost:1433` with the `sa` login and the password from `.env`.

### Initialize the database

The database itself is created by the scripts in `db/`, not by the container image. The
`db-init` service waits for `sqlserver` to become healthy, then runs every file matching
`db/0*.sql` in filename order using `sqlcmd -b`, so the first error stops the run. Scripts are
mounted read-only at `/scripts`.

```bash
# Run, or re-run after editing a script
docker compose run --rm db-init

# Follow the init log
docker compose logs -f db-init
```

Scripts are idempotent, which is why re-running the command is safe. Confirm the result:

```bash
docker exec enterprisehr-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<your-password>' -C -d EnterpriseHRDB \
  -Q "SELECT name FROM sys.tables ORDER BY name"
```

A completed run lists six tables: `Departments`, `Employees`, `Positions`,
`ProjectAssignments`, `Projects`, `SalaryHistories`.

### Connection string

The application reads one connection string, under the key `ConnectionStrings:EnterpriseHR`:

```json
{
  "ConnectionStrings": {
    "EnterpriseHR": "Server=localhost,1433;Database=EnterpriseHRDB;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=True"
  }
}
```

`src/EnterpriseHR.WebApp/appsettings.Development.json` ships this string built from the
`.env.example` placeholder password. If your `.env` uses a different `MSSQL_SA_PASSWORD`,
update that file to match it. `appsettings*.json` is tracked, so keep only the placeholder
there.

`AddEnterpriseHrDataAccess` reads the key once and throws `InvalidOperationException` at
startup when it is missing. Because the key only exists in `appsettings.Development.json`, a
non-Development environment must supply it. The standard ASP.NET Core configuration override
does that without touching a file:

```bash
export ConnectionStrings__EnterpriseHR="Server=localhost,1433;Database=EnterpriseHRDB;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=True"
```

The double underscore maps to the `:` hierarchy, and environment variables win over
`appsettings*.json`.

### Run

There is one runnable project. It serves the MVC screens, the REST API and Swagger on one
origin:

```bash
dotnet run --project src/EnterpriseHR.WebApp
```

| What | URL |
| ---- | --- |
| MVC screens | <http://localhost:5100> (dashboard at `/`), `/Employees`, `/Projects`, and `/Dashboard` for the same dashboard |
| REST API | <http://localhost:5100/api/**> (e.g. `/api/employees`, `/api/projects/1/details`, `/api/reports/department-summary`) |
| Swagger UI | <http://localhost:5100/swagger> |

The port comes from `src/EnterpriseHR.WebApp/Properties/launchSettings.json`. There is no
second host and no port 5200. Swagger is Development-only, which is the environment
`launchSettings.json` sets for `dotnet run`.

SQL Server must be running and `EnterpriseHRDB` initialized before the host serves
database-backed requests.

## Business rules

These are the rules the code enforces and the test matrix exercises. The same rules apply
whether a request arrives through a screen or through `/api/**`, because both call the same
services.

| # | Rule |
| - | ---- |
| A1 | A salary change requires an existing active employee. A request that changes neither salary nor position is rejected as a no-op. |
| A2 | A new employee's salary must fall inside the range of the selected position. |
| A3 | Employee Edit never changes salary or position. Every salary and position change goes through the salary-adjustment flow, which writes one audit row to `SalaryHistories`. |
| A4 | Workload counts assignments on `Active` and `Planning` projects only. A Completed or Cancelled assignment never blocks new work. |
| A5 | An active project's member count includes only assignments whose employee is active. |
| A6 | Deactivating an already inactive employee succeeds as a no-op, without writing. |
| A7 | API exception mapping: `NotFoundException` is 404, `BusinessRuleException` is 400, `ConflictException` is 409. Every response, including these, uses the `{ success, message, data }` envelope. |

Employees are never physically deleted: deactivation sets `IsActive = 0` and keeps the row,
its assignments and its salary history.

## Testing

Two documents cover acceptance:

| Document | Contents |
| -------- | -------- |
| [docs/TEST-SCENARIOS.md](docs/TEST-SCENARIOS.md) | The acceptance matrix: 22 API rows and 17 browser rows, each with Given / When / Expected and the measured result, plus the reset SQL. |
| [docs/TEST-SCENARIO-API.md](docs/TEST-SCENARIO-API.md) | The detailed per-group API reference: every route, its success and failure paths, the seed fixture and the envelope rules. |

[`src/EnterpriseHR.Api/requests.http`](src/EnterpriseHR.Api/requests.http) is the runnable
collection behind the API table: 63 requests, each with a `# ->` comment stating the status and
payload to assert. Run them with the VS Code REST Client, the JetBrains HTTP Client, or any
`.http` runner.

`dotnet build EnterpriseHRSystem.sln` builds with no warnings and no errors.

## Troubleshooting

**Start over with a clean database.** This deletes the `sqldata` volume and with it all local
database data; containers and images are recreated on the next `up`.

```bash
docker compose down -v
docker compose up -d
docker compose run --rm db-init
```

**Container exits immediately or restart-loops.** Check the log first; the most common causes
are a password that fails complexity rules and a host port already in use.

```bash
docker compose logs sqlserver
lsof -nP -iTCP:1433 -sTCP:LISTEN
```

**`db-init` fails partway through.** It runs with `-b`, so the first SQL error aborts the
script. Fix the offending statement and re-run `docker compose run --rm db-init`; scripts are
idempotent, so already-applied parts are skipped.

**`The certificate chain was issued by an authority that is not trusted`.** The development
container uses a self-signed certificate. Keep `TrustServerCertificate=True` in the local
connection string.

**Password changes.** `MSSQL_SA_PASSWORD` is only applied when the data volume is initialized.
After changing it in `.env`, recreate the volume with `docker compose down -v` and update
`src/EnterpriseHR.WebApp/appsettings.Development.json` to match.

**`Connection string 'EnterpriseHR' is not configured`.** The host started outside
Development, where `appsettings.Development.json` is not loaded. Set
`ConnectionStrings__EnterpriseHR` as shown above.

**Apple Silicon (M1/M2/M3/M4).** The SQL Server image is built for `linux/amd64` only. If the
container fails to start, add the platform to **both** services in `docker-compose.yml`:

```yaml
services:
  sqlserver:
    platform: linux/amd64
    # ...
  db-init:
    platform: linux/amd64
    # ...
```

That alone is not always enough. SQL Server cannot run under QEMU user-mode emulation (the
`binfmt`/`qemu-x86_64` path) and exits with:

```
sqlservr: Invalid mapping of address 0x... in reserved address space below 0x400000000000
```

Use Rosetta emulation instead: in Docker Desktop enable *Settings → General → Use Rosetta for
x86_64/amd64 emulation*, or start Colima with `colima start --vz-rosetta`. Where Rosetta is
unavailable, run the containers inside an amd64 VM, for example
`colima start --profile mssql --arch x86_64`, and point `DOCKER_HOST` at that profile's socket
before running `docker compose`:

```bash
export DOCKER_HOST=unix://$HOME/.colima/mssql/docker.sock
```

The stack itself needs no changes; SQL Server is slower under emulation either way.

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
| T27 | WebApp shell: design-system layout, navigation, alerts, pager, home cards | done |
| T28 | Employees index: search, department filter, include-inactive, paging | done |
| T29 | Employees create and edit forms with dynamic department, position and manager selects | done |
| T30 | Employee deactivation confirmation and salary/promotion adjustment with audit history | done |
| T31 | Projects index: directory table with dates, status badges and active team size | done |
| T32 | Project details: information card, member table with allocation bars and inactive markers, add-member form with workload feedback | done |
| T33 | Budget dashboard: department summary with over-budget rows, status badges and totals | done |
| T33.5 | Sidebar navigation, dashboard at `/`, and a reorganised employee filter panel | done |
| T33.6 | Form grid alignment on Create/Edit/add-member, and "project directory" naming | done |
| T33.7 | Documentation and code-comment cleanup across the repository | done |
| T34 | README for a fresh clone, acceptance matrix, dead-file removal, final review | done |

The dashboard is the landing page at `/`, so opening the app shows department budgets
immediately. One row per department compares monthly payroll with its budget, totals sit on
top, and a department whose payroll exceeds the budget carries the error surface and a
labelled badge.

The employee directory searches by name or number, filters by department, includes former
staff on request and pages the results. Employees can be created, edited, deactivated through
a confirmation card, and given a raise or promotion that writes a salary-history row. The
project directory lists every project with its department, dates, status badge and active team
size, and links into a detail page with per-member allocation bars, former members marked
inactive, and an add-member form whose workload cap is enforced by the repository and reported
back on the same screen.

Stage-by-stage detail, including which decisions were made and why, is in
[docs/development-stages.md](docs/development-stages.md).

## Scope

Out of scope unless separately requested: authentication and authorization, EF Core or any
migration tool, test projects, CI pipelines, and containerization of the .NET applications.

Database access rules that apply to every feature:

- Dapper asynchronous APIs only (`QueryAsync`, `ExecuteAsync`, `QueryMultipleAsync`); no
  synchronous calls, `.Result` or `.Wait()`.
- Parameterized SQL everywhere, including `DynamicParameters` for stored procedures; no string
  concatenation or interpolation of user input.
- One `await using` connection per repository method, with
  `CommandDefinition(..., cancellationToken: ct)` on every call.
- A transaction for operations that must be atomic across statements.
- No EF Core, no ORM change tracking, no lazy loading.

## Documentation

Full setup, architecture, database and stage history live in `docs/`; this README stays a
front page.

| Document | Contents |
| -------- | -------- |
| [docs/index.md](docs/index.md) | Index of all documentation. |
| [docs/getting-started.md](docs/getting-started.md) | Prerequisites, local setup, configuration, running the application, troubleshooting. |
| [docs/architecture.md](docs/architecture.md) | Projects, layer responsibilities, data-access rules, API conventions, design-system contract, scope. |
| [docs/database.md](docs/database.md) | Schema, constraints, stored procedure, seed fixtures. |
| [docs/TEST-SCENARIOS.md](docs/TEST-SCENARIOS.md) | Acceptance matrix: API and browser scenarios with their measured results. |
| [docs/TEST-SCENARIO-API.md](docs/TEST-SCENARIO-API.md) | Detailed API scenario reference, paired with `src/EnterpriseHR.Api/requests.http`. |
| [docs/development-stages.md](docs/development-stages.md) | T01–T34 stage log and what each stage added. |
| [db/README.md](db/README.md) | Rules for the numbered SQL init scripts. |
| [AGENTS.md](AGENTS.md) | Working rules for coding agents, including where each kind of knowledge belongs. |

`design-system/` holds the exported HR Workspace UI reference (tokens, screens, component
states) and is the visual source of truth for UI work. Start at `design-system/index.html`,
then `DESIGN-HANDOFF.md` and `design-system.html`.
