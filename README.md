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
| RESTful integration API | `EnterpriseHR.Api` | External systems and integration clients | ASP.NET Core Web API, controller-based endpoints, Swagger/OpenAPI |

Both modules call the same `EnterpriseHR.DataAccess` project, and business rules live in
`EnterpriseHR.Domain`, so the dashboard and the API apply identical validation and
calculation logic. No EF Core — all access is Dapper over hand-written, parameterized SQL.

## Structure

```
EnterpriseHRSystem.sln
├── src/EnterpriseHR.Domain        # entities, DTOs, requests, exceptions, interfaces
├── src/EnterpriseHR.DataAccess    # Dapper repositories, SQL, transactions
├── src/EnterpriseHR.WebApp        # ASP.NET Core MVC dashboard
├── src/EnterpriseHR.Api           # ASP.NET Core Web API
├── db/                            # numbered SQL init scripts, run by db-init
├── design-system/                 # exported HR Workspace UI reference
└── docs/                          # long-form documentation
```

## Quick start

Prerequisites: .NET 8 SDK, Docker (or Docker Engine with the Compose plugin), Git.

```bash
cp .env.example .env            # then set MSSQL_SA_PASSWORD (8+ chars, 3 of 4 character groups)

docker compose up -d            # SQL Server 2022 on localhost:1433
docker compose run --rm db-init # create + seed EnterpriseHRDB

dotnet run --project src/EnterpriseHR.WebApp --urls http://localhost:5100
dotnet run --project src/EnterpriseHR.Api --urls http://localhost:5200
```

| Application | URL |
| ----------- | --- |
| MVC dashboard | <http://localhost:5100> |
| REST API | <http://localhost:5200> |
| Swagger UI | <http://localhost:5200/swagger> |

`dotnet build EnterpriseHRSystem.sln` builds with no warnings. SQL Server must be up and
`EnterpriseHRDB` initialized before either host serves database-backed requests.

Full setup — `.env` rules, connection-string overrides, `db-init` verification, reset
steps, and the Apple Silicon caveats — is in
[docs/getting-started.md](docs/getting-started.md).

## Project status

| Stage | Scope | State |
| ----- | ----- | ----- |
| T01–T04 | Repository, Docker SQL Server, schema, stored procedure, seed data | done |
| T05–T06 | Solution and four projects, connection factory and DI wiring | done |
| T07–T10 | Domain entities, DTOs/requests, exceptions, interfaces | done |
| T11–T16 | Repository base, lookup repository, employee reads, writes, soft delete and salary history | done |
| T17+ | Application features (services, API controllers, Razor screens) | pending |

The database, domain layer and data-access foundation are complete. The hosts are still
MVC/API templates: no API controllers, no service implementations, and no Razor screen
ported from the design system yet. Stage-by-stage detail is in
[docs/development-stages.md](docs/development-stages.md).

Out of scope unless separately requested: authentication and authorization, EF Core or any
migration tool, test projects, CI pipelines, and containerization of the .NET applications.

## Documentation

| Document | Contents |
| -------- | -------- |
| [docs/index.md](docs/index.md) | Index of all documentation. |
| [docs/getting-started.md](docs/getting-started.md) | Prerequisites, local setup, configuration, running the hosts, troubleshooting. |
| [docs/architecture.md](docs/architecture.md) | Projects, layer responsibilities, data-access rules, design-system contract, scope. |
| [docs/database.md](docs/database.md) | Schema, constraints, stored procedure, seed fixtures. |
| [docs/development-stages.md](docs/development-stages.md) | T01–T16 stage log and what comes next. |
| [db/README.md](db/README.md) | Rules for the numbered SQL init scripts. |
| [AGENTS.md](AGENTS.md) | Working rules for coding agents, including where each kind of knowledge belongs. |

`design-system/` holds the exported HR Workspace UI reference (tokens, screens, component
states) and is the visual source of truth for dashboard work. Start at
`design-system/index.html`, then `DESIGN-HANDOFF.md` and `design-system.html`.
