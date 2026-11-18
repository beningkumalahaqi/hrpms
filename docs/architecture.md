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
| Hosting | One process, `EnterpriseHR.WebApp`, serving MVC at `/` and the API at `/api/**`; `EnterpriseHR.Api` is a class library |
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

Both modules serve from one ASP.NET Core process, `EnterpriseHR.WebApp`, on one origin:
the dashboard uses normal MVC routes (`/`, `/employees`) while the API answers under
`/api/**`. `EnterpriseHR.Api` is a class library of controllers that the host discovers as
an MVC application part — it is not a second executable.

Both modules call the same `EnterpriseHR.DataAccess` project. Business rules live in
`EnterpriseHR.Domain`, so the dashboard and the API apply identical validation and
calculation logic.

## Projects

```
EnterpriseHRSystem.sln
├── src/EnterpriseHR.Domain        # entities, enums, business rules
├── src/EnterpriseHR.DataAccess    # Dapper repositories, SQL, transactions
├── src/EnterpriseHR.WebApp        # the only executable host: MVC views + /api/**
└── src/EnterpriseHR.Api           # class library: API controllers, envelope, API middleware
```

| Project | Responsibility | Depends on |
| ------- | -------------- | ---------- |
| `EnterpriseHR.Domain` | Entities, enums, DTOs, request types, domain validation and calculation rules, exception hierarchy, and the repository/service interfaces. No database or web dependencies. | none |
| `EnterpriseHR.DataAccess` | Dapper repositories, services (thin orchestration), parameterized SQL, stored-procedure calls, mapping, transaction handling, and DI registration. Owns the `Microsoft.Data.SqlClient` connection factory and connection-string configuration. | Domain |
| `EnterpriseHR.WebApp` | The only executable application: ASP.NET Core MVC host with Razor Views, view models, controllers, Tag Helpers, the design-system layout, Swagger, and the JSON/validation configuration. Registers `EnterpriseHR.Api` as an MVC application part so its controllers are served from this process. Calls DataAccess for all persistence. | Domain, DataAccess, Api |
| `EnterpriseHR.Api` | Class library, not a host: API controllers deriving from `ApiControllerBase`, the `ApiResponse<T>` envelope, and API-specific middleware (`ExceptionHandlingMiddleware`). No `Program.cs`, no `appsettings`, no launch profile. Routes stay under `api/[controller]`. | Domain, DataAccess |

There is no fifth project, no separate services project and no reverse proxy: the
orchestration services live in `EnterpriseHR.DataAccess/Services/`, next to the repositories
they call. `EnterpriseHR.DataAccess` is the single place where SQL is written; nothing opens
a `SqlConnection` directly, which is what keeps the MVC dashboard and the API consistent as
the schema evolves.

The `EnterpriseHR.Api` reference direction is one-way — WebApp → Api — so the API assembly
cannot depend on the host. That is what lets `Api` stay a plain library with a
`Microsoft.AspNetCore.App` framework reference and nothing executable in it.

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
  convention, not code: Domain references no ASP.NET Core assemblies, and
  `ExceptionHandlingMiddleware` in `EnterpriseHR.Api` translates the types into status codes.
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
`ConnectionStrings:EnterpriseHR`, registers `IDbConnectionFactory` as a singleton, and
registers the four repositories and three services as scoped. `EnterpriseHR.WebApp` calls it
exactly once, in its `Program.cs`, so the process fails at startup when the connection string
is missing, and a missing registration surfaces on the first request instead of at startup.
The API controllers resolve those same registrations because they run inside the same host.

Current repositories: `LookupRepository` (department, position and employee option
lists), `EmployeeRepository`, which implements `IEmployeeRepository` (paged reads with
four-type multi-mapping, create, update, soft delete, and salary adjustment),
`ProjectRepository`, which implements `IProjectRepository` (project list with active-member
counts, details with every assignment in one batch, and assigning an employee under the
100% workload cap), and `ReportRepository`, which implements `IReportRepository` and calls
`dbo.sp_GetDepartmentSummary` as a stored procedure. Deactivation never removes the row:
`IsActive` becomes 0, and a transaction that holds the row under `UPDLOCK` refuses the
change while an active employee still reports to this person, or while they hold an
assignment on an `Active` project. A salary adjustment changes `Salary`/`PositionId` and
writes the matching `SalaryHistories` row in the same transaction.

Current services in `src/EnterpriseHR.DataAccess/Services/`: `EmployeeService`,
`ProjectService` and `ReportService`, each implementing its Domain interface. They are thin
orchestration only — null-guard the request, trim the text a form can send padded, clamp
paging, delegate. No SQL and no business rule lives there: the workload cap, project status
checks, salary range, uniqueness guards and soft-delete protections all stay in the
repositories, which is what keeps the MVC host and the API consistent. `EmployeeService`
normalises the filter into a copy so the caller's `EmployeeFilter` is untouched;
`GetFormOptionsAsync` fetches departments, positions and managers with three sequential
awaits and asks the lookup repository for active managers only, excluding the employee being
edited. `ProjectService.GetAssignableEmployeeOptionsAsync` asks for active employees with no
exclusion. See [development-stages.md](development-stages.md) for the stage that added them.

## API conventions

The API answers with one envelope, success or failure:

```json
{ "success": true, "message": "OK", "data": { } }
```

`ApiResponse<T>` in `src/EnterpriseHR.Api/Models/ApiResponse.cs` builds it via
`Ok(data, message = "OK")` and `Fail(message, data = default)`. Controllers derive from
`ApiControllerBase`, which carries `[ApiController]` and the `api/[controller]` route template
plus the `OkResponse<T>` and `CreatedResponse<T>` helpers, so the shape cannot drift per
endpoint. Serialization is camelCase, pinned explicitly in the WebApp `Program.cs` rather than
inherited.

The API is served by the MVC host, not a second process. `Program.cs` calls
`AddControllersWithViews().AddApplicationPart(typeof(ApiControllerBase).Assembly)`, so the
controllers in `EnterpriseHR.Api` are discovered without their source living in WebApp, then
`MapControllers()` (attribute routes, `/api/**`) before the conventional MVC route. Both
routes share one origin, one `AddEnterpriseHrDataAccess` registration and one connection
string.

| Situation | Status |
| --------- | ------ |
| Read / update OK | 200 |
| Assignment created | 201 (with a `Location` header) |
| Model validation failed, `BusinessRuleException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| Unhandled | 500, generic message, no stack trace |

A model-binding failure returns 400 with the same envelope, `message` = `"Validation failed."`
and `data` = a field-to-messages dictionary keyed by the camelCase path the client sent
(`reason`, `body.name`). Swagger UI is served at `/swagger` in Development only; the document
is titled **EnterpriseHR API v1** and documents the controllers from the Api assembly.

### Endpoints

The endpoints implemented so far. Controllers live in `src/EnterpriseHR.Api/Controllers/`;
every `/api/**` path not listed here is a 404 envelope.

| Endpoint | Response `data` | Notes |
| -------- | --------------- | ----- |
| `GET /api/employees` | `PagedResult<EmployeeDto>` | Active employees only. `search` (name, employee number, email; 255 chars max), `departmentId`, `page` (default 1), `pageSize` (default 10, max 100). |
| `PATCH /api/employees/{id:int}/salary` | `EmployeeDto` | Body `UpdateSalaryRequest`: `newSalary`, `newPositionId`, `reason`. Success `message` is `"Salary updated."`. |
| `GET /api/projects/{id:int}/details` | `ProjectDetailDto` | The project plus its `members` array. A former employee stays listed, with `isEmployeeActive: false`. |
| `POST /api/projects/{id:int}/assign` | `{ assignmentId, projectId, employeeId, allocationPercentage }` | Body `AssignEmployeeRequest`: `employeeId`, `roleInProject`, `allocationPercentage`. 201 with a `Location` header pointing at that project's details. |
| `GET /api/reports/department-summary` | `DepartmentSummaryDto[]` | One row per department from `dbo.sp_GetDepartmentSummary`, ordered by department name. `remainingBudget` goes negative when payroll exceeds budget; `isOverBudget` is the same fact as a boolean. |

`GET /api/employees` is hard-wired to active employees: the controller builds its own
`EmployeeFilter` with `IsActive = true` instead of forwarding the bound one, so a client-sent
`?isActive=false` is ignored rather than widening the list to former staff. The remaining
values are copied through, so query binding and the `EmployeeFilter` validation attributes
still apply — `pageSize=0` or `pageSize=101` is a 400 validation failure, not a silent clamp.
Paging past the last page returns an empty `items` with the real `totalCount`.

`PATCH /api/employees/{id:int}/salary` answers with the refreshed `EmployeeDto`, read back via
`GetByIdAsync` after the write, so the caller sees the stored salary, position, department and
manager names rather than an echo of the request. Business failures are not special-cased in
the controller: `ChangeSalaryAsync` throws, and the exception middleware returns 404 for an
unknown id or 400 for an out-of-range salary, an inactive employee, a missing target position
or a no-op change, with `data: null`.

`GET /api/projects/{id:int}/details` throws `NotFoundException("Project not found.")` when the
service returns null rather than returning a bare `NotFound()`, so the 404 body is built by the
same middleware as every other error envelope.

`POST /api/projects/{id:int}/assign` follows the same rule by omission: it has no try/catch, and
the repository's exception types already match the status map — `BusinessRuleException` (400)
for an inactive employee, a project that is not `Active`/`Planning`, or an out-of-range
percentage; `ConflictException` (409) for a duplicate assignment or an allocation that would
exceed 100%; `NotFoundException` (404) for an unknown project or employee. The percentage is a
share of the employee's *total* workload across projects, not of this one. The 201 body is built
from the id the insert returned plus the values just persisted, so no read-back is needed; the
`projectId` comes from the route, since the body deliberately carries none.
`CreatedAssignmentDto` is nested inside `ProjectsController` because it is the response shape of
that one endpoint and nothing else reads it.

`GET /api/reports/department-summary` is the only route declared with an explicit
`[Route("api/reports")]`. The inherited `api/[controller]` template would give `/api/Reports`,
which routing matches case-insensitively anyway, but the report contract names the path in
lowercase and Swagger publishes whatever the template says — so the literal keeps the document
honest rather than relying on case-insensitive matching. `[HttpGet("department-summary")]` fixes
the action segment. The action awaits `GetDepartmentSummaryAsync`, which runs
`dbo.sp_GetDepartmentSummary` through async Dapper with
`commandType: CommandType.StoredProcedure`, so the aggregation is one server-side round trip.
`data` is the array itself, not a wrapper object.

The XML summaries and the `[ProducesResponseType]` attributes on these actions feed Swagger, but
the XML half only reaches the document once `GenerateDocumentationFile` is enabled on the Api
project — today `IncludeXmlComments` finds no file and the descriptions stay empty.

### Exercising the API

`src/EnterpriseHR.Api/requests.http` is the runnable counterpart of this section: 63 requests
for every route above, each preceded by a `# -> <status>` comment stating what to assert, with
`@host` declared once and the deterministic seed ids aliased rather than hard-coded inline. The
same matrix is written out as tables in [TEST-SCENARIO-API.md](TEST-SCENARIO-API.md), including
the seed fixture the expectations depend on.

Two groups write rows. The salary group is a closed cycle that ends where it started; the
assignment group adds one row that no endpoint can delete, so the file documents the clean-up
SQL at the top and is safe to run twice in a row after it. A full run must leave
`SalaryHistories` holding one row per accepted adjustment and nothing from the rejected ones.

### Exception handling

`ExceptionHandlingMiddleware` in `src/EnterpriseHR.Api/Middleware/` is the single place API
failures become status codes. It is registered in the WebApp pipeline after `UseStaticFiles()`
and before `UseRouting()`, which is early enough to cover routing, filters and controller
execution, and inside the MVC exception handler so MVC pages keep their own handling.

| Thrown | Status | `message` |
| ------ | ------ | --------- |
| `NotFoundException` | 404 | the exception message |
| `BusinessRuleException` | 400 | the exception message |
| `ConflictException` | 409 | the exception message |
| `SqlException` 2627 / 2601 | 409 | duplicate-constraint sentence |
| `SqlException` 547 | 400 | missing/in-use reference sentence |
| `OperationCanceledException` with `RequestAborted` | none | no response; the client is gone |
| anything else | 500 | `"An unexpected error occurred."` |

The SQL numbers are a safety net: repositories translate them already, so a `SqlException`
arriving at the host means one escaped, and that case is logged at `Error` even though the
response is a clean 4xx. Only the 500 path logs the full exception (`LogError`); the response
never carries a stack trace, an exception message or a SQL identifier. Each is written with
`WriteAsJsonAsync` as `ApiResponse<object>.Fail(message)`, so `data` is `null` — the
`data`-carrying variant stays reserved for model validation, whose `data` holds the field
errors.

Two guards keep it honest. If `Response.HasStarted` the middleware cannot rewrite the response,
so it logs and rethrows rather than appending JSON to a partially sent body. And the whole
middleware is skipped for any path outside `/api`: a non-API exception is rethrown to the MVC
exception handler, so an MVC page still renders the developer exception page in Development
and `/Home/Error` in Production. The same split applies to framework-generated statuses:
`UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}")` renders a branded page for a
body-less MVC 404, while the `UseWhen` branch on `/api` wraps body-less API 404 and 405
responses in the envelope. The status-code middleware sits outside that branch, so it only
sees an API response after the envelope has started and leaves it alone.

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

Porting rules for the MVC screens:

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

### Ported so far

| Asset | What it holds |
| ----- | ------------- |
| `wwwroot/css/site.css` | The export's `OD-LAYOUT-PRIMITIVES` block, then `design-system/styles.css` verbatim (`:root` tokens, type scale, buttons, panels, tables, pagination, states), then one labelled section for the app shell. Change the export first and re-port; do not restyle the copied rules inline. |
| `Views/Shared/_Layout.cshtml` | Skip link, icon sprite, sticky top navigation, `<main>` and footer. The export's sidebar plus topbar are composed into one header because the screens use top navigation; the brand, `.nav-item` and active-state tokens are unchanged, and the active route is derived from `RouteData`. |
| `Views/Shared/_Alerts.cshtml` | `TempData["Success"]`, `["Warning"]` and `["Error"]` as inline alerts with `role="status"`/`role="alert"` and a dismiss control. Rendered by the layout above `RenderBody()`. |
| `Views/Shared/_Pager.cshtml` | The design-system `.pagination` markup, built from `(Page, TotalPages, RouteValues)`. Links keep the current query string and override only `page`. |
| `Views/Home/Index.cshtml` | Page header plus one card per module. |
| `Views/Employees/Index.cshtml` | The employee directory: GET filter panel (search, department, include-inactive, Search/Reset), `.data-table` above 1024px, record cards below, pager in the panel footer. The Search/Reset row and `.checkbox-field` are the only additions, and they extend `.filter-panel` and `.field` from the export's tokens. |
| `Views/Shared/Error.cshtml` | The design-system error state for `HomeController.Error`, reached by `UseExceptionHandler` and by status-code re-execution; the action puts the original status back on the response. |

Bootstrap is not referenced by the layout. `wwwroot/lib/bootstrap` and the template
`Views/Shared/_Layout.cshtml.css` still exist on disk but nothing links them. Only the Home and
Employees screens exist, so the Projects and Dashboard nav links answer the branded 404 until
their stages land. New screens start from `.page`, `.page-header`, `.panel`, `.data-table`,
`.field` and the `.button` variants instead of new CSS.

## Scope

This repository implements only what the practical exam requires. Out of scope unless
separately requested: authentication and authorization, EF Core or any migration tool,
test projects, CI pipelines, and containerization of the .NET applications.
