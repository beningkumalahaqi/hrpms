# Enterprise HR & Project Management System

An internal HR and project-management application built on .NET 8 and SQL Server 2022.
It combines a server-rendered admin dashboard for HR staff with a documented REST API
for system-to-system integration, both reading and writing the same `EnterpriseHRDB`
database through one shared data-access library.

The project is being built in stages against a practical exam specification. See
[Project status](#project-status) for what exists in the repository today.

## Purpose

The system manages employees, departments, projects, project assignments, and the
attendance and leave records that HR depends on. It is delivered as two application
modules over a single database:

| Module | Project | Audience | Technology |
| ------ | ------- | -------- | ---------- |
| HR / admin dashboard | `EnterpriseHR.WebApp` | HR staff and administrators working in a browser | ASP.NET Core MVC, Razor Views, HR Workspace design system, Tag Helpers |
| RESTful integration API | `EnterpriseHR.Api` | External systems and integration clients | ASP.NET Core Web API, controller-based endpoints, Swagger/OpenAPI |

Both modules call the same `EnterpriseHR.DataAccess` project. Business rules live in
`EnterpriseHR.Domain`, so the dashboard and the API apply identical validation and
calculation logic.

## Technology Stack

| Area | Technology |
| ---- | ---------- |
| Language | C# 12 |
| Runtime / framework | .NET 8 (`net8.0`) |
| Web application | ASP.NET Core MVC with Razor Views and Tag Helpers, styled by the HR Workspace design system |
| REST API | ASP.NET Core Web API, controller-based endpoints, Swagger/OpenAPI, global exception-handling middleware |
| Database | Microsoft SQL Server 2022, database `EnterpriseHRDB`, run locally with Docker Compose |
| Data access | Dapper (asynchronous methods only) with `Microsoft.Data.SqlClient` |
| Database programming | Parameterized SQL, `IDbTransaction`, stored procedures, Dapper multi-mapping, `QueryMultipleAsync` |
| Architecture | One solution, `EnterpriseHRSystem.sln`, with exactly four projects |
| Frontend | Server-rendered Razor Views styled with the exported HR Workspace design system in `design-system/` (custom CSS tokens, IBM Plex Sans). No Bootstrap, no SPA framework, no separate frontend app |
| Local infrastructure | Docker Compose |
| Documentation | Markdown README and docs |

Entity Framework Core is deliberately **not** used. All database access goes through
Dapper with hand-written, parameterized SQL.

## Solution Architecture

The solution is `EnterpriseHRSystem.sln` and contains exactly four projects. There is
no separate services or repository project; orchestration stays in the two hosts, and
SQL stays in DataAccess.

```
EnterpriseHRSystem.sln
├── src/EnterpriseHR.Domain        # entities, enums, business rules
├── src/EnterpriseHR.DataAccess    # Dapper repositories, SQL, transactions
├── src/EnterpriseHR.WebApp        # ASP.NET Core MVC dashboard
└── src/EnterpriseHR.Api           # ASP.NET Core Web API
```

| Project | Responsibility | Depends on |
| ------- | -------------- | ---------- |
| `EnterpriseHR.Domain` | Entities, enums, value objects, domain validation and calculation rules. No database or web dependencies. | none |
| `EnterpriseHR.DataAccess` | Dapper repositories, parameterized SQL, stored-procedure calls, mapping, and transaction handling. Owns the `Microsoft.Data.SqlClient` connection factory and connection-string configuration. | Domain |
| `EnterpriseHR.WebApp` | ASP.NET Core MVC host. Razor Views, view models, controllers, Tag Helpers, and the design-system layout. Calls DataAccess for all persistence. | Domain, DataAccess |
| `EnterpriseHR.Api` | ASP.NET Core Web API host. Controller-based JSON endpoints, Swagger/OpenAPI documentation, global exception-handling middleware, DTOs. Calls DataAccess for all persistence. | Domain, DataAccess |

`EnterpriseHR.DataAccess` is the single place where SQL is written. Neither host
opens a `SqlConnection` directly, which is what keeps the MVC dashboard and the API
consistent as the schema evolves.

Operations that change several tables together (for example creating a project with
its initial assignments, or recording an attendance correction alongside its audit
row) run inside an `IDbTransaction` owned by the DataAccess layer. Read paths use
Dapper's asynchronous APIs, and multi-result queries use `QueryMultipleAsync` with
`ReadAsync` rather than several round trips.

## Frontend and Design System

The MVC dashboard does not use Bootstrap. The UI follows the exported **HR Workspace**
design system in `design-system/`, which is the visual source of truth and outranks any
framework default.

| File | Role |
| ---- | ---- |
| `index.html` | Launcher / overview. A navigation entry, not a combined final screen. |
| `design-system.html` | Component and token reference screen. |
| `styles.css`, `design-system.css` | Tokens and component CSS. `:root` holds the palette (primary `#3730a3`), type scale, spacing, radii, and shadows. |
| `app.js`, `design-system.js` | Interaction behavior to reproduce: filters, dialogs, copy actions, component states. |
| `DESIGN-MANIFEST.json` | Machine-readable map of screens, tokens, required states, and viewports. |
| `DESIGN-HANDOFF.md` | Fidelity contract: preserve geometry, tokens, copy, and states; drop prototype chrome. |

Porting rules for the dashboard:

- Copy the tokens into `src/EnterpriseHR.WebApp/wwwroot/css/` and keep the `:root`
  variable names. Do not substitute a framework theme or a new palette.
- One Razor view per screen. `index.html` is a launcher, so it is not copied wholesale
  into a single view.
- Forms, links, and validation use Tag Helpers on top of the design-system markup.
  JavaScript stays plain browser scripts; there is no SPA, virtual DOM, or client-side
  router.
- Keep the exported responsive behavior and check 360x800, 390x844, 430x932, 600x960,
  820x1180, 1024x768, 1366x768, 1440x900, and 1920x1080 with no horizontal overflow.
- The export is reference material. Nothing from it is served by the application until
  a screen or component is ported into a Razor view and static assets.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet --version` should report `8.x`)
- Docker Desktop, or Docker Engine with the Compose plugin (`docker compose version`)
- Git
- A SQL client for inspecting the database (optional): `sqlcmd`, Azure Data Studio, or DBeaver

On Apple Silicon, see [Troubleshooting](#reset-and-troubleshooting) before starting the containers;
the SQL Server image is published for `linux/amd64` only.

## Getting Started

```bash
git clone <repository-url> EnterpriseHRSystem
cd EnterpriseHRSystem
cp .env.example .env
```

Open `.env` and set `MSSQL_SA_PASSWORD` to a password that satisfies SQL Server
complexity rules: at least 8 characters, using three of uppercase, lowercase, digits,
and symbols. `.env` is ignored by Git; never commit it and never put a real password
in this README.

Start SQL Server:

```bash
docker compose up -d
```

The first run pulls the `mcr.microsoft.com/mssql/server:2022-latest` image and takes a
few minutes. The `sqlserver` container is ready when Docker reports it as healthy:

```bash
docker compose ps
```

## Database Setup

SQL Server listens on `localhost:1433` with the `sa` login and the password from
`.env`. The database itself is created by SQL scripts in `db/`, not by the container
image.

The `db-init` service in `docker-compose.yml` waits for `sqlserver` to become healthy,
then runs every file matching `db/0*.sql` in filename order using `sqlcmd -b`, so the
first error stops the run. Scripts are mounted read-only at `/scripts`.

```bash
# Run (or re-run) initialization manually
docker compose run --rm db-init

# Follow the init log
docker compose logs -f db-init
```

Initialization scripts are written to be idempotent, which is why re-running the
command above is safe after editing a script. Each new schema, procedure, or seed file
must keep that property.

Check that the server is responding before blaming the application:

```bash
docker compose ps                      # sqlserver should show "healthy"
docker exec enterprisehr-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<your-password>' -C -Q "SELECT @@VERSION"
```

To confirm the database and its tables exist:

```bash
docker exec enterprisehr-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<your-password>' -C -d EnterpriseHRDB \
  -Q "SELECT name FROM sys.tables ORDER BY name"
```

A completed schema run lists six tables: `Departments`, `Employees`, `Positions`,
`ProjectAssignments`, `Projects`, `SalaryHistories`.

### Database scripts

| File | Contents | Status |
| ---- | -------- | ------ |
| `db/01_schema.sql` | `EnterpriseHRDB` database, tables, keys, indexes | present |
| `db/02_sp_GetDepartmentSummary.sql` | `dbo.sp_GetDepartmentSummary` department report | present (T03) |
| `db/03_seed.sql` | Seed data for all five tables (test-matrix fixture set) | present (T04) |

`db/01_schema.sql` creates the six tables in dependency order, with named `PK_`, `UQ_`,
`FK_`, `CK_` and `DF_` constraints, plus indexes on the main foreign-key columns. It is
idempotent, so re-running `db-init` skips objects that already exist. It does not alter
existing tables: after changing a definition, recreate the volume
(`docker compose down -v`) before running `db-init` again.

## Configuration

Both hosts read the same connection string, under the key
`ConnectionStrings:EnterpriseHR`:

```json
{
  "ConnectionStrings": {
    "EnterpriseHR": "Server=localhost,1433;Database=EnterpriseHRDB;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=True"
  }
}
```

Both `src/EnterpriseHR.WebApp/appsettings.Development.json` and
`src/EnterpriseHR.Api/appsettings.Development.json` ship this exact local development
string, built from the `.env.example` placeholder password. If your `.env` uses a
different `MSSQL_SA_PASSWORD`, update both files to match. Only commit a placeholder
password in appsettings files; `.env` is ignored by Git, `appsettings` is not.

The connection string is read once by `AddEnterpriseHrDataAccess` in
`EnterpriseHR.DataAccess`, which throws `InvalidOperationException` at startup when the
key is missing and registers the Dapper connection factory for both hosts. Because the
key only exists in `appsettings.Development.json`, a non-Development environment must
supply it, for example with the standard ASP.NET Core configuration override:

```bash
export ConnectionStrings__EnterpriseHR="Server=localhost,1433;Database=EnterpriseHRDB;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=True"
```

The double underscore maps to the `:` hierarchy. Environment variables win over
`appsettings*.json`, which is the intended way to configure the API in a container or
pipeline.

## Running the Applications

The solution `EnterpriseHRSystem.sln` and the four projects exist. The commands below work
from the repository root; the dev ports come from each project's
`Properties/launchSettings.json`.

| Application | URL |
| ----------- | --- |
| MVC dashboard | <http://localhost:5100> |
| REST API | <http://localhost:5200> |
| Swagger UI | <http://localhost:5200/swagger> |

```bash
# Terminal 1 - MVC dashboard
dotnet run --project src/EnterpriseHR.WebApp --urls http://localhost:5100

# Terminal 2 - REST API
dotnet run --project src/EnterpriseHR.Api --urls http://localhost:5200
```

SQL Server must be running and `EnterpriseHRDB` initialized before either host starts
serving requests that touch the database.

## Repository Structure

```
.
├── docker-compose.yml          # SQL Server 2022 + db-init
├── .env.example                # template for local secrets
├── AGENTS.md                   # guidance for coding agents: design, docs, memory
├── EnterpriseHRSystem.sln      # solution: the four projects below
├── db/                         # SQL init scripts, run by db-init
├── design-system/              # exported HR Workspace UI reference (tokens, screens, states)
├── docs/                       # supporting documentation (created with the first design note)
└── src/
    ├── EnterpriseHR.Domain/
    ├── EnterpriseHR.DataAccess/
    ├── EnterpriseHR.WebApp/
    └── EnterpriseHR.Api/
```

## Project Status

| Stage | Scope | State |
| ----- | ----- | ----- |
| T01 | Repository, Docker Compose SQL Server, README | done |
| T02 | Database schema and `EnterpriseHRDB` creation | done |
| T03 | Stored procedures and data-access queries | done |
| T04 | Seed data | done |
| T05 | Solution and the four projects | done |
| T06 | Connection factory and DI wiring | done |
| T07+ | Application features | pending |

The schema from T02 is in place, so `db-init` now actually runs `db/01_schema.sql`
instead of reporting that it has nothing to do. T03 adds
`dbo.sp_GetDepartmentSummary` in `db/02_sp_GetDepartmentSummary.sql`; both scripts are
idempotent and re-run cleanly. T04 adds `db/03_seed.sql`, which fills the five data tables
with the test-matrix fixture set (4 departments, 7 positions, 10 employees, 6 projects,
7 assignments) and leaves `SalaryHistories` empty. The seed is skipped entirely once
`dbo.Departments` has a row, so re-running `db-init` neither errors nor duplicates data.

T05 adds `EnterpriseHRSystem.sln` and the four projects under `src/`, wired
`DataAccess → Domain` and `{WebApp, Api} → {Domain, DataAccess}`. `dotnet build
EnterpriseHRSystem.sln` succeeds with no warnings, the MVC host serves on
<http://localhost:5100>, and the API serves Swagger UI on <http://localhost:5200/swagger>.
The projects are still templates: `Program.cs`, the Razor views, and the controllers
carry no HR features yet.

T06 adds the shared data-access wiring: `IDbConnectionFactory` with the
`Microsoft.Data.SqlClient` implementation (`DataAccess/Infrastructure/`), and
`AddEnterpriseHrDataAccess` which validates `ConnectionStrings:EnterpriseHR` and registers
the factory as a singleton. Both hosts call it from `Program.cs`, so either one fails at
startup with a clear message when the connection string is missing. Repositories register
as scoped in `AddEnterpriseHrDataAccess` as they are added. T04's seed data is not read
until the first repository lands.

The design system export in `design-system/` is static reference material at this
stage. No Razor view or layout uses it yet; that starts when the dashboard screens are
ported.

## Reset and Troubleshooting

**Start over with a clean database.** This deletes the `sqldata` volume and with it
all local database data; containers and images are recreated on the next `up`.

```bash
docker compose down -v
docker compose up -d
docker compose run --rm db-init
```

**Container exits immediately or restart-loops.** Check the log first; the most common
causes are a password that fails complexity rules and a host port already in use.

```bash
docker compose logs sqlserver
lsof -nP -iTCP:1433 -sTCP:LISTEN
```

**`db-init` fails partway through.** It runs with `-b`, so the first SQL error aborts
the script. Fix the offending statement and re-run `docker compose run --rm db-init`;
scripts are idempotent, so already-applied parts are skipped.

**`The certificate chain was issued by an authority that is not trusted`.** The
development container uses a self-signed certificate. Keep `TrustServerCertificate=True`
in the local connection string.

**Apple Silicon (M1/M2/M3/M4).** The SQL Server image is built for `linux/amd64` only.
If the container fails to start, add the platform to **both** services in
`docker-compose.yml`:

```yaml
services:
  sqlserver:
    platform: linux/amd64
    # ...
  db-init:
    platform: linux/amd64
    # ...
```

That alone is not always enough. SQL Server cannot run under QEMU user-mode emulation
(the `binfmt`/`qemu-x86_64` path) and exits with:

```
sqlservr: Invalid mapping of address 0x... in reserved address space below 0x400000000000
```

Use Rosetta emulation instead: in Docker Desktop enable *Settings → General → Use
Rosetta for x86_64/amd64 emulation*, or start Colima with `colima start --vz-rosetta`.
Where Rosetta is unavailable, run the containers inside an amd64 VM, for example
`colima start --profile mssql --arch x86_64`, and point `DOCKER_HOST` at that profile's
socket before running `docker compose`. The stack itself needs no changes; SQL Server
is slower under emulation either way.

**Password changes.** `MSSQL_SA_PASSWORD` is only applied when the data volume is
initialized. After changing it in `.env`, recreate the volume with
`docker compose down -v` before starting again.

## Scope

This repository implements only what the practical exam requires. Out of scope unless
separately requested: authentication and authorization, EF Core or any migration tool,
test projects, CI pipelines, and containerization of the .NET applications.

Database access rules that apply to every feature added here:

- Dapper asynchronous APIs only (`QueryAsync`, `ExecuteAsync`, `QueryMultipleAsync`);
  no synchronous calls.
- Parameterized SQL everywhere, including `DynamicParameters` for stored procedures;
  no string concatenation of user input.
- `IDbTransaction` for operations that must be atomic across statements.
- No EF Core, no ORM change tracking, no lazy loading.

Frontend rules:

- Server-rendered Razor Views only, styled from `design-system/`. No Bootstrap or other
  CSS framework, no SPA, no client-side router.

## Documentation

Design notes, API contracts, and database documentation belong in `docs/`. Each stage
adds its own notes there rather than expanding this README beyond setup.

`AGENTS.md` points coding agents at the three reference areas: `design-system/` for the
visual contract, `docs/` for written documentation, and `agents/memory/` for local
working notes. The memory directory is intentionally not committed; it holds verified
environment facts and task notes that only matter on a developer machine.
