# Development stages

The project is built in numbered stages against a practical exam specification. This is
the log of what each stage added. The authoritative "what is done" summary is the
**Project Status** table in the root `README.md`.

| Stage | Scope | State |
| ----- | ----- | ----- |
| T01 | Repository, Docker Compose SQL Server, README | done |
| T02 | Database schema and `EnterpriseHRDB` creation | done |
| T03 | Stored procedures and data-access queries | done |
| T04 | Seed data | done |
| T05 | Solution and the four projects | done |
| T06 | Connection factory and DI wiring | done |
| T07 | Domain entities | done |
| T08 | DTOs, requests, common types | done |
| T09 | Domain exceptions | done |
| T10 | Domain interfaces | done |
| T11 | Repository base, transaction runner, SQL error translation | done |
| T12 | Lookup repository | done |
| T13 | Employee read operations (multi-mapping) | done |
| T14 | Employee create/update | done |
| T15 | Employee soft delete with referential protection | done |
| T16 | Salary adjustment and promotion with audit trail | done |
| T17 | Project reads: list and QueryMultiple details | done |
| T18 | Project assignment with workload cap and duplicate protection | done |
| T19 | Report repository: department summary via stored procedure | done |
| T20 | Services and dependency-injection registration | done |
| T21 | API bootstrap, response envelope, validation, Swagger | done |
| T21.5 | Single host: Api as a class library, WebApp serves MVC + `/api/**` + Swagger | done |
| T22 | Global API exception-handling middleware | done |
| T23 | Employee API endpoints: list and salary change | done |
| T24 | Project API endpoints: details and assignment | done |
| T25 | Report API endpoint: department summary | done |
| T26+ | Remaining API endpoints and Razor screens | pending |

## T01–T04 — Infrastructure and database

T01 added the repository, `docker-compose.yml` (SQL Server 2022 plus the `db-init` init
container), `.env.example` and this documentation.

T02 added `db/01_schema.sql`: the six tables with named constraints and the
foreign-key indexes, all guarded so `db-init` can be re-run. See
[database.md](database.md) for the resulting schema.

T03 added `dbo.sp_GetDepartmentSummary` in `db/02_sp_GetDepartmentSummary.sql`, the
department report the dashboard shows and the API exposes. Both T02 and T03 scripts re-run
cleanly.

T04 added `db/03_seed.sql`, the test-matrix fixture set: 4 departments, 7 positions, 10
employees, 6 projects, 7 assignments, and an empty `SalaryHistories`. The seed is skipped
once `dbo.Departments` has a row, so re-running `db-init` neither errors nor duplicates
data.

## T05–T06 — Solution and wiring

T05 added `EnterpriseHRSystem.sln` and the four projects under `src/`, wired
`DataAccess → Domain` and `{WebApp, Api} → {Domain, DataAccess}`.
`dotnet build EnterpriseHRSystem.sln` succeeds with no warnings, the MVC host served on
<http://localhost:5100>, and the API served Swagger UI on port 5200. T21.5 later collapsed
the two hosts into one (see below): `Api` is now a class library and the MVC host serves
both.

T06 added the shared data-access wiring: `IDbConnectionFactory` with the
`Microsoft.Data.SqlClient` implementation, and `AddEnterpriseHrDataAccess`, which
validates `ConnectionStrings:EnterpriseHR` and registers the factory as a singleton. T05's
two hosts each called it from their own `Program.cs`, so either one failed at startup with a
clear message when the connection string was missing. Repositories register as scoped in the
same method as they are added.

## T07–T10 — Domain layer

T07 added the six entity classes under `src/EnterpriseHR.Domain/Entities/`, one per table,
plus `ProjectStatus` with the four status values `CK_Projects_Status` accepts. No
navigation properties; repositories map flat rows.

T08 added the data-transfer layer: DTOs, request types with `DataAnnotations` mirroring
the column limits, and `PagedResult<T>`. `EmployeeFilter` pages with `Page`/`PageSize`
defaulting to 1 and 10. See [architecture.md](architecture.md) for the shapes.

T09 added the exception hierarchy: abstract `AppException` and the
`NotFoundException` (404), `BusinessRuleException` (400) and `ConflictException` (409)
siblings. Domain still references no ASP.NET Core assemblies.

T10 added the seven repository and service interfaces — 28 methods, `CancellationToken
ct = default` on every one, `Task<T?>` on the four single-record lookups, and DTO/request
types (never entities) crossing the repository boundary.

## T11–T20 — Data access

T11 added `RepositoryBase`: `OpenAsync`, the only transaction runner in the solution, and
`TranslateSql` for SQL Server errors 2627/2601/547. See
[architecture.md](architecture.md#data-access-rules).

T12 added `LookupRepository` with department, position and employee options. Every query
projects two columns aliased `Value` and `Text` for the positional `SelectOptionDto`
record. Employee options are labelled `FullName (EmployeeNumber)` and accept
`activeOnly` and `excludeEmployeeId`; the seed yields 9 active employees, 10 with
inactive records included.

T13 added the employee read paths: `GetAllEmployeesAsync` (filtered `COUNT(*)` plus one
`OFFSET`/`FETCH` page, never a query per row) and `GetEmployeeByIdAsync`. Both use one
four-type Dapper multi-map — employee, department, position, manager — with the select
list ordered so `splitOn: "Id,Id,Id"` splits correctly. The manager join is a `LEFT JOIN`,
so top-level managers come back with a null manager, and `Search` escapes `\`, `%`, `_`
and `[` for `LIKE` so a typed wildcard matches literally. Paging clamps `Page` to at least
1 and `PageSize` to 1–100 and computes the offset in `long`.

T14 added the employee write paths to the same class. `CreateEmployeeAsync` checks the
salary against the selected position's `MinSalary`/`MaxSalary` range, confirms the
department and that a given manager exists and is active, then rejects a duplicate
employee number or email with a `ConflictException` naming the field. `UpdateEmployeeAsync`
validates the department and manager, refuses `ManagerId == Id`, and updates only
`FullName`, `Email`, `DepartmentId`, `ManagerId` and `JoinDate` — salary and position are
deliberately absent from the statement. Zero affected rows raises `NotFoundException`.

Case-insensitive uniqueness comes from the database collation rather than lowercasing
either side, so the unique indexes stay usable. The explicit checks only produce a precise
message; the unique constraints behind them are translated to the same conflict, which
guards against two concurrent creates.

T15 added `DeleteEmployeeAsync`, a soft delete: the row stays, only `IsActive` becomes 0.
The method runs in one transaction and re-reads the employee with `UPDLOCK, ROWLOCK`
first, so concurrent deactivations serialise instead of racing. A missing id raises
`NotFoundException`, an already inactive employee returns successfully without writing
(A6), and two guards can block the update with a `ConflictException` (A7): active employees
still reporting to this manager, or an assignment on a project whose status is `Active`.
Planning, Completed and Cancelled projects do not block, and neither does an assignment on
a project the employee finished. There is no `DELETE FROM dbo.Employees` anywhere in the
solution, so an employee is never physically removed.

T16 added `UpdateEmployeeSalaryAsync`, the salary-adjustment and promotion workflow. It runs
in one transaction: the employee row is re-read under `UPDLOCK`, an inactive employee is
rejected as a business rule (A1), the target position — `NewPositionId` or the current one —
must exist and the new salary must fall inside its range, and a request that changes neither
salary nor position is refused as a no-op. The method then updates `Salary`/`PositionId` and
inserts one `SalaryHistories` row (old salary, new salary, reason, default `ChangedAt`) on
the same transaction, so a failure in either write rolls both back. With this member in
place `EmployeeRepository` implements `IEmployeeRepository`.

T17 added `ProjectRepository` with the two project read paths. `GetAllProjectsAsync` is a
single grouped statement joining departments and a filtered `Employees` join, so
`ActiveMemberCount` comes from one aggregate rather than a query per project — inactive
members hold their assignment but are not counted. `GetProjectDetailsAsync` sends the
project row and its assignments as two `SELECT`s in one `QueryMultipleAsync` batch, then
reads the first result set as `ProjectDetailDto` and attaches the second as
`ProjectMemberDto.Members`; `TotalAllocationOfMembers` is computed from the loaded list.
Assignments are never filtered by employee or project status, so a Completed or Cancelled
project still lists who worked on it and an inactive member appears with
`IsEmployeeActive = false`. An unknown id returns null rather than an empty project.

T18 added `AssignEmployeeToProjectAsync`, which completes `IProjectRepository`. One
transaction covers the whole rule set: the allocation must be 1–100 and the role
non-blank, the employee must exist and be active, the project must exist and be `Active` or
`Planning`, the employee must not already be on that project, and the resulting workload
must not exceed 100%. The workload is the sum of the employee's allocation across `Active`
and `Planning` projects only (A4), so an assignment on a Completed or Cancelled project never
blocks new work — the seed's E004 carries a 100% Completed assignment and can still take a
100% Active one. The employee row is read with `UPDLOCK, ROWLOCK`, so two concurrent
assignments for the same person serialize: the second one sees the first one's row and is
rejected, and the workload can never pass 100%. The insert returns
`SCOPE_IDENTITY()`, and `UQ_ProjectAssignments_Project_Employee` is translated to the same
duplicate conflict as a race safety net.

T19 added `ReportRepository`, which completes `IReportRepository` and therefore the
repository layer. It has a single method, `GetDepartmentSummaryAsync`, which calls
`dbo.sp_GetDepartmentSummary` as a stored procedure (`CommandType.StoredProcedure`) rather
than sending SQL text: no parameters, one round trip, and the aggregation stays in one
server-side plan. The procedure aggregates employees and projects through two CTEs so the
joins cannot multiply rows, and its aliases already match `DepartmentSummaryDto`, so Dapper
maps all eight columns directly and the DTO computes `IsOverBudget`. It returns all four
departments in name order — `FIN`, `HR`, `IT`, `OPS` — including OPS, which has no active
employees, so its salary is 0 and its whole budget is still available. Only IT is over
budget: 77,000,000 in monthly salary against a 70,000,000 budget leaves −7,000,000.

T20 added the three services and registered everything in `AddEnterpriseHrDataAccess`. The
services are thin on purpose: they trim the text a form can send padded (employee number,
name, email, assignment role, salary-change reason), clamp `Page`/`PageSize` into range
before the repository sees them, null-guard every request argument, and delegate. No SQL and
no business rule lives in a service — the 100% workload cap, the status checks, the salary
range and the uniqueness guards all stay in the repositories, so the MVC host and the API
cannot drift apart. `EmployeeService.GetFormOptionsAsync` loads departments, positions and
managers with three sequential awaits (each lookup opens its own connection, so there is
nothing to share) and asks for active managers while excluding the employee being edited;
`ProjectService.GetAssignableEmployeeOptionsAsync` asks for active employees with no
exclusion. `IDbConnectionFactory` stays a singleton, and all four repositories and three
services are scoped.

T21 gave the API its shape before any endpoint exists. `ApiResponse<T>` is the one envelope
every response uses, `ApiControllerBase` carries the `[ApiController]` attribute, the
`api/[controller]` route and the `OkResponse`/`CreatedResponse` helpers, and
`InvalidModelStateResponseFactory` replaces the default `ProblemDetails` body with the same
envelope, so a rejected request looks like a successful one apart from `success` and
`message`. Serialization is camelCase, pinned explicitly; the validation dictionary is keyed
by the camelCase property path the client sent (`reason`, `body.name`), with non-property keys
such as `body` and `$.name` passed through untouched. Swagger is titled **EnterpriseHR API
v1** and is served in Development only, so Production answers 404 at `/swagger`.

T21 also fixed a latent Domain bug it tripped over: `[Range(typeof(decimal), ...)]` parses its
string limits with the current culture, so on a locale whose decimal separator is `,`
(`en-ID`, the machine this was verified on) `"0.01"` parsed as 1 and the max limit threw
`ArgumentException` during validation — a 500 on every create/update employee and salary
request in either host. Both attributes now set `ParseLimitsInInvariantCulture = true`; the
bounds themselves are unchanged.

## T21.5 — One host, one origin

T21.5 collapsed the two hosts into one. `EnterpriseHR.Api` changed from
`Microsoft.NET.Sdk.Web` to `Microsoft.NET.Sdk` with a `Microsoft.AspNetCore.App`
`FrameworkReference`, so it still compiles controllers but is no longer an application: its
`Program.cs`, `Properties/launchSettings.json` and both `appsettings` files were deleted, and
the `Swashbuckle.AspNetCore` package moved to `EnterpriseHR.WebApp`, which is where the
host-only Swagger configuration had to live anyway. The `.http` file now points at port 5100.

`EnterpriseHR.WebApp` became the only executable project. Its `Program.cs` calls
`AddControllersWithViews()` and then `.AddApplicationPart(typeof(ApiControllerBase).Assembly)`,
which is what makes the controllers in `Api` discoverable without moving their source into
`WebApp` or using a reverse proxy. The JSON camelCase option, the
`InvalidModelStateResponseFactory` envelope and the `ToCamelCasePath` helper moved across
unchanged, as did the Swagger document definition; only the XML-comment path changed, and it
now looks for `EnterpriseHR.Api.xml` in the host's output directory rather than the host's own
XML file. `Program.cs` maps `MapControllers()` before the conventional
`{controller=Home}/{action=Index}/{id?}` route so `/api/**` resolves to attribute routes and
everything else falls through to MVC. `AddEnterpriseHrDataAccess(builder.Configuration)` is
still called exactly once, so both halves share one connection string and one set of scoped
registrations.

The reference graph is now `WebApp → {Domain, DataAccess, Api}`, `Api → {Domain, DataAccess}`,
`DataAccess → Domain`, `Domain → none`, which keeps `Api` from depending on its host.

Verified from a single `dotnet run --project src/EnterpriseHR.WebApp`: MVC at
<http://localhost:5100> served `<title>Home Page</title>` with 200, Swagger UI returned 200 at
`/swagger/index.html` (`/swagger` issues a 301 to it) and listed the probe controller's
`/api/T215Probe` routes, the API envelope answered 200/201/400 exactly as in T21, an API
controller resolved `IEmployeeService` and read live seed data (`total=10`, first employee
`Andi Wijaya`), and nothing listened on port 5200. An unknown `/api/**` path and an unknown
MVC path both return 404; `/api` is not reachable outside the prefix. Swagger remains
Development-only: in Production it answers 404 while MVC still serves 200.

## T22 — Global API exception handling

`src/EnterpriseHR.Api/Middleware/ExceptionHandlingMiddleware.cs` is a convention-based
middleware (`RequestDelegate` + `ILogger<ExceptionHandlingMiddleware>`) that turns a failed
API request into the same envelope a successful one uses. It maps `NotFoundException` → 404,
`BusinessRuleException` → 400 and `ConflictException` → 409 with the exception's own message,
and anything else → 500 with the fixed sentence `"An unexpected error occurred."`.
`SqlException` 2627/2601 → 409 and 547 → 400 are kept as a safety net mirroring
`RepositoryBase.TranslateSql`, so a constraint error that escapes a repository still produces
the right status instead of a 500; that case is logged at `Error` because it means a
repository missed a translation. An `OperationCanceledException` whose `RequestAborted` fired
is not an error at all — the client hung up, so the middleware writes no body and logs at
`Debug`.

Responses are written with `WriteAsJsonAsync` and `ApiResponse<object>.Fail(message)`, so
`data` is `null`; the `data`-carrying 400 stays reserved for model binding, which is handled
earlier by `InvalidModelStateResponseFactory`. The full exception is logged only on the 500
path, so the client never sees a stack trace, an exception message or a SQL identifier.
If the response already started the middleware cannot rewrite it, so it logs and rethrows
rather than appending JSON to a partly sent body.

Registration is in the single WebApp pipeline, after `UseStaticFiles()` and before
`UseRouting()`: early enough to cover routing, filters and action execution, and inside the
MVC exception handler so MVC pages are unaffected. The middleware itself returns immediately
for any path outside `/api`, rethrowing to that handler, which is what keeps the developer
exception page in Development and `/Home/Error` in Production on an MVC request. A `UseWhen`
branch on `/api` wraps the body-less framework statuses — 404 for an unknown route, 405 for a
wrong verb — in the same envelope, guarded by `HasStarted`, a zero `ContentLength` and a null
`ContentType` so a real payload is never overwritten.

Verified with a throwaway probe controller in `Api` and one in WebApp, both later deleted:
each domain exception returned its status and an envelope with `data: null`; a real duplicate
insert returned 409 and a real foreign-key violation 400, from SQL Server, without writing a
row; an unhandled action returned 500 `"An unexpected error occurred."` with zero occurrences
of the internal detail in the body while the log held the full exception; an unknown `/api`
route and a wrong verb returned envelope 404/405 while an unknown MVC route stayed a plain
body-less 404 and an MVC action still rendered the HTML error page in Production; a
model-binding failure still returned the T21 validation envelope with field errors. A response
that had already started was left untouched and logged. Client cancellation produced no
response and no error log.

## T23 — Employee endpoints

`src/EnterpriseHR.Api/Controllers/EmployeesController.cs` adds the first two real endpoints, so
`/api/employees` stopped being a 404.

`GET /api/employees` returns `PagedResult<EmployeeDto>` and lists active employees only. The
controller builds a fresh `EmployeeFilter` with `IsActive = true` and copies `search`,
`departmentId`, `page` and `pageSize` onto it, rather than forwarding the bound filter: `IsActive`
is a server-side decision, so `?isActive=false` cannot be used to list former staff. Copying
(not replacing) the bound object keeps query-string binding and the `EmployeeFilter`
`[Range]`/`[StringLength]` attributes live, so `pageSize=0`, `pageSize=101`, `page=0` and a
256-character search all return the 400 validation envelope instead of being clamped silently.
`EmployeeService` then trims the search and clamps paging as before, and a page past the end
returns an empty `items` with the real `totalCount`. Search stays parameterised and its LIKE
metacharacters escaped: `search=%`, `search=_` and `search=' OR 1=1--` all match nothing.

`PATCH /api/employees/{id:int}/salary` calls `ChangeSalaryAsync`, then reads the employee back
with `GetByIdAsync` and returns that DTO with `message` `"Salary updated."`. The read-back is
deliberate: it returns what the database holds, including department and manager names the
request never mentions. The controller has no try/catch — `NotFoundException` and
`BusinessRuleException` from the repository reach `ExceptionHandlingMiddleware` and leave as 404
and 400 with `data: null`, the same split the middleware already applies elsewhere. A missing id
in the read-back is treated defensively as a 404 rather than a null dereference.

Both actions carry `<summary>`/`<response>` XML docs and
`[ProducesResponseType(typeof(ApiResponse<T>), ...)]` for 200/400/(404)/409/500. Note that the
XML half is inert: `EnterpriseHR.Api.csproj` does not set `GenerateDocumentationFile`, so the
`IncludeXmlComments` call in `Program.cs` still finds no file and Swagger shows no summaries.
Enabling it is a one-line change but raises 9 CS1591 warnings in files this stage does not own,
so it is left for a review stage.

Verified against the live database. `GET /api/employees?departmentId=1&page=1&pageSize=2`
returned 2 items with `totalCount=4`, `totalPages=2`, and page 2 returned the other two.
`?isActive=false&pageSize=100` still reported `totalCount=9` with every item active; searching a
soft-deleted employee's name returned 0. `PATCH /api/employees/6/salary` (E004, Senior Developer,
band 12,000,000–25,000,000) returned 400 for `11,999,999`, for the no-op `12,000,000`, for
inactive E005, for a non-existent position and for employee `9999` (404), and 200 with
`"Salary updated."` and a refreshed DTO for `13,000,000`. After all five rejected requests
`SalaryHistories` still held 0 rows and E004 still read 12,000,000 in position 2; after the
accepted one it held the single `12,000,000 → 13,000,000` row. A second accepted request that
also changed position (to Engineering Manager at 26,000,000) wrote both columns and a second
history row. Malformed JSON, an empty body, a missing `reason`, a negative salary and a
non-numeric query value each returned the 400 validation envelope. The probe rows and the salary
changes were then reverted, leaving the seed at `4|10|9|6|7|0`.

## T24 — Project endpoints

`src/EnterpriseHR.Api/Controllers/ProjectsController.cs` exposes a project's details and the
assignment operation.

`GET /api/projects/{id:int}/details` calls `GetDetailsAsync` and turns a null result into
`NotFoundException("Project not found.")` — thrown rather than returned, so the 404 body comes
from the same middleware as every other error, giving the exact message the contract asks for
instead of a bare status. A found project returns `ProjectDetailDto` with its `members` array;
former employees stay in that array with `isEmployeeActive: false`, keeping project history
intact.

`POST /api/projects/{id:int}/assign` calls `AssignAsync` and answers 201 with
`{ assignmentId, projectId, employeeId, allocationPercentage }` and a `Location` header for
`GET /api/projects/{id}/details`. The body is built from the id the insert returned plus the
values just persisted — no read-back, because the endpoint returns the assignment, not the
project. `projectId` comes from the route: `AssignEmployeeRequest` deliberately carries only
`employeeId`, `roleInProject` and `allocationPercentage`. `CreatedAssignmentDto` is a record
nested in the controller, since it is this endpoint's response shape alone.

The controller has no try/catch, and it does not need one: the repository already throws the
types the middleware maps. Inactive employee, a project whose status is not
`Active`/`Planning`, and an out-of-range percentage are `BusinessRuleException` (400); a
duplicate assignment and a total allocation above 100% are `ConflictException` (409); an unknown
project or employee is `NotFoundException` (404). `allocationPercentage` is a share of the
employee's total workload across all projects, so E002 at 100% cannot accept another 10%.

Verified against the live database, starting from 7 assignments. Rejections with no write
afterwards (still 7 rows): duplicate E003 → PRJ-001 (409), E002 at 100% → PRJ-003 at 10% (409
with `Allocation would reach 110% (current 100% + requested 10%)`), inactive E005 (400),
`Completed` PRJ-004 (400), `Cancelled` PRJ-005 (400), unknown project 999 (404), unknown
employee 999 (404), and six validation failures — allocation 0, allocation 101, missing role,
101-character role, `employeeId` 0 and malformed JSON. The accepted request, E003 → PRJ-002 at
50%, returned 201, `Location: /api/Projects/2/details`, assignment id 24 and
`allocationPercentage: 50`; E003's total allocation moved 30% → 80% and PRJ-002's member list
grew to two. That row was then deleted and the seed snapshot returned to `4|10|9|6|7|0` with
E003 back at 30%.

`/api/projects` itself stays a 404: only `{id}/details` and `{id}/assign` are routed.

## T25 — Report endpoint

`src/EnterpriseHR.Api/Controllers/ReportsController.cs` exposes `GET
/api/reports/department-summary`, the endpoint the original practical exam asked for.

The route is declared with an explicit `[Route("api/reports")]` plus
`[HttpGet("department-summary")]` rather than inheriting `api/[controller]` from
`ApiControllerBase`. The inherited template would give `/api/Reports`; routing matches that
case-insensitively, so both work, but Swagger publishes exactly what the template says and the
contract names the path in lowercase.

The action awaits `IReportService.GetDepartmentSummaryAsync`, which delegates to
`ReportRepository` and runs `dbo.sp_GetDepartmentSummary` through async Dapper with
`commandType: CommandType.StoredProcedure`. There is no try/catch: a failure reaches
`ExceptionHandlingMiddleware` and leaves as the generic 500 envelope. `data` is the array itself
rather than an object wrapping it.

Verified against the live database and against an independent recomputation that does not use
the stored procedure (a correlated-subquery query over `Employees` and `Projects`); the four
rows agree. Finance 3 employees / 48,000,000 payroll / 2,000,000 remaining / 1 active project;
Human Resources 2 / 38,000,000 / 22,000,000 / 0; IT 4 / 77,000,000 / -7,000,000 / 2 with
`isOverBudget: true`; Operations 0 / 0 / 20,000,000 / 0. Values were checked field by field
against the SQL output rather than eyeballed.

The procedure really executes: `sys.dm_exec_procedure_stats` counted 15 executions before the
first request, 16 after one and 17 after two. The call is async end to end — no `.Result`,
`.Wait()`, `GetAwaiter().GetResult()` or synchronous Dapper call exists in the controller,
service or repository — and 20 concurrent requests completed in 445 ms wall clock against 6.2 s
of summed individual latency, which a blocking implementation could not achieve. Route edges:
`/api/reports` is a 404 envelope, a trailing slash and different casing both return 200, `POST`
on the path is the 405 envelope, and `/api/report/...` (singular) is a 404.

The failure path was exercised by temporarily replacing the procedure body with `SELECT 1/0`:
the endpoint returned the generic 500 envelope with zero occurrences of the SQL error text, the
procedure name or a stack trace, while the log held the full `SqlException`. The procedure was
then restored from `db/02_sp_GetDepartmentSummary.sql` and its `sys.sql_modules` definition hash
matched the pre-change hash exactly (`0xFE25CF50...`, 1186 characters). The endpoint is
read-only, so the seed snapshot stayed `4|10|9|6|7|0`.

## Next

T26 onward adds the remaining controllers — employee create/read/update/delete, and the project
list — and then the Razor screens ported from `design-system/`.
