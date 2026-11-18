# Architecture

How the four projects fit together, the rules data access follows, and the visual
contract for the dashboard.

## Technology stack

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
| Frontend | Server-rendered Razor Views styled from the exported HR Workspace design system in `design-system/` (custom CSS tokens, IBM Plex Sans). No Bootstrap, no SPA framework, no separate frontend app |
| Local infrastructure | Docker Compose |

Entity Framework Core is deliberately **not** used. All database access goes through
Dapper with hand-written, parameterized SQL.

## Modules

The system manages employees, departments, projects, project assignments, and the
attendance and leave records that HR depends on. It is delivered as two application
modules over a single database:

| Module | Project | Audience |
| ------ | ------- | -------- |
| HR / admin dashboard | `EnterpriseHR.WebApp` | HR staff and administrators working in a browser |
| RESTful integration API | `EnterpriseHR.Api` | External systems and integration clients |

Both call the same `EnterpriseHR.DataAccess` project. Business rules live in
`EnterpriseHR.Domain`, so the dashboard and the API apply identical validation and
calculation logic.

## Projects

```
EnterpriseHRSystem.sln
├── src/EnterpriseHR.Domain        # entities, enums, business rules
├── src/EnterpriseHR.DataAccess    # Dapper repositories, SQL, transactions
├── src/EnterpriseHR.WebApp        # ASP.NET Core MVC dashboard
└── src/EnterpriseHR.Api           # ASP.NET Core Web API
```

| Project | Responsibility | Depends on |
| ------- | -------------- | ---------- |
| `EnterpriseHR.Domain` | Entities, enums, DTOs, request types, domain validation and calculation rules, exception hierarchy, and the repository/service interfaces. No database or web dependencies. | none |
| `EnterpriseHR.DataAccess` | Dapper repositories, parameterized SQL, stored-procedure calls, mapping, and transaction handling. Owns the `Microsoft.Data.SqlClient` connection factory and connection-string configuration. | Domain |
| `EnterpriseHR.WebApp` | ASP.NET Core MVC host. Razor Views, view models, controllers, Tag Helpers, and the design-system layout. Calls DataAccess for all persistence. | Domain, DataAccess |
| `EnterpriseHR.Api` | ASP.NET Core Web API host. Controller-based JSON endpoints, Swagger/OpenAPI documentation, global exception-handling middleware, DTOs. Calls DataAccess for all persistence. | Domain, DataAccess |

There is no separate services or repository project: orchestration stays in the two
hosts, and SQL stays in DataAccess. `EnterpriseHR.DataAccess` is the single place where
SQL is written; neither host opens a `SqlConnection` directly, which is what keeps the
MVC dashboard and the API consistent as the schema evolves.

## Domain layer

- `Entities/` — one plain class per table, property names, order and CLR types matching
  the schema columns, no navigation properties. `ProjectStatus` holds the four status
  constants the `CK_Projects_Status` constraint accepts.
- `Dtos/` — `EmployeeDto`, `ProjectListItemDto`, `ProjectMemberDto`, `ProjectDetailDto`,
  `DepartmentSummaryDto`, `EmployeeFormOptions`, `SelectOptionDto`. Mutable classes with a
  parameterless constructor so Dapper multi-map callbacks can build them column by
  column. Computed values (`TotalAllocationOfMembers`, `IsOverBudget`, `TotalPages`) are
  read-only properties.
- `Requests/` — `CreateEmployeeRequest`, `UpdateEmployeeRequest`, `UpdateSalaryRequest`,
  `AssignEmployeeRequest`, `EmployeeFilter`. `DataAnnotations` limits mirror the table
  columns, so WebApp `ModelState` checks and the API's automatic 400s share one set of
  rules.
- `Exceptions/` — abstract `AppException` plus `NotFoundException` (404),
  `BusinessRuleException` (400) and `ConflictException` (409). The mapping is a
  convention, not code: Domain references no ASP.NET Core assemblies, and the hosts'
  middleware/filter translates the types into status codes.
- `Interfaces/` — `ILookupRepository`, `IEmployeeRepository`, `IProjectRepository`,
  `IReportRepository` and the `IEmployeeService`, `IProjectService`, `IReportService`
  contracts. Every method takes `CancellationToken ct = default` last; the four
  single-record lookups return `Task<T?>` so callers decide what a missing row means.
  Repositories speak DTOs and request types, never entities.

## Data access rules

These apply to every feature added to the repository.

- Dapper asynchronous APIs only (`QueryAsync`, `ExecuteAsync`, `QueryMultipleAsync`,
  `ExecuteScalarAsync`); no `.Result`, `.Wait()` or synchronous calls.
- SQL lives in the repository as a private `const string` raw string literal, never
  inline string interpolation of user input; parameters only, including
  `DynamicParameters` for stored procedures.
- One `await using` connection per method, obtained from `IDbConnectionFactory`, and
  `CommandDefinition(..., cancellationToken: ct)` on every call.
- `ct.ThrowIfCancellationRequested()` immediately before a Dapper multi-map.
- No N+1 queries: list endpoints cost one `COUNT(*)` plus one paged query, or a single
  `QueryMultipleAsync` with `ReadAsync`.
- Operations that must be atomic across statements use `IDbTransaction`.

`RepositoryBase` (`src/EnterpriseHR.DataAccess/Infrastructure/`) is the base every
repository inherits. It exposes:

- `OpenAsync` for single-statement calls.
- `ExecuteInTransactionAsync`, the only transaction runner in the solution: one
  connection, one `ReadCommitted` transaction, commit on success, rollback with
  `CancellationToken.None` before rethrowing.
- `TranslateSql` / `ExecuteSqlAsync`, which map SQL Server errors 2627 and 2601 to
  `ConflictException` and 547 to `BusinessRuleException`; every other error stays a
  `SqlException`. The original error is kept as `InnerException`, so the SQL error number
  survives.

`AddEnterpriseHrDataAccess` (in `ServiceCollectionExtensions.cs`) validates
`ConnectionStrings:EnterpriseHR` and registers `IDbConnectionFactory` as a singleton. Both
hosts call it from `Program.cs`. Repositories are not registered there yet: they become
scoped services when the first service that needs them lands.

Current repositories: `LookupRepository` (department, position and employee option
lists), `EmployeeRepository`, which implements `IEmployeeRepository` (paged reads with
four-type multi-mapping, create, update, soft delete, and salary adjustment), and
`ProjectRepository`, which implements `IProjectRepository` (project list with active-member
counts, details with every assignment in one batch, and assigning an employee under the
100% workload cap). Deactivation never removes the row: `IsActive` becomes 0, and a
transaction that holds the row under `UPDLOCK` refuses the change while an active employee
still reports to this person, or while they hold an assignment on an `Active` project. A
salary adjustment changes `Salary`/`PositionId` and writes the matching `SalaryHistories`
row in the same transaction. The report repository and all service implementations come
with the application features — see [development-stages.md](development-stages.md).

## Frontend and design system

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
- The export is reference material. Nothing from it is served by the application until a
  screen or component is ported into a Razor view and static assets.

The dashboard is still the ASP.NET Core MVC template: no Razor view or layout uses the
design system yet. That starts when the first screen is ported.

## Scope

This repository implements only what the practical exam requires. Out of scope unless
separately requested: authentication and authorization, EF Core or any migration tool,
test projects, CI pipelines, and containerization of the .NET applications.
