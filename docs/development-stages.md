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
| T26 | API request collection and scenario matrix | done |
| T27 | WebApp shell: design-system layout, top navigation, alerts, pager, home cards | done |
| T28 | Employees index: search, department filter, include-inactive, paging | done |
| T29 | Employees create and edit forms with dynamic department/position/manager selects | done |
| T30+ | Remaining employee actions and the project and dashboard screens | pending |

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

## T26 — API request collection and scenario matrix

T26 adds no application code. It adds the two documents that make the API testable by hand:
`src/EnterpriseHR.Api/requests.http` (63 runnable requests with a `# -> <status>` comment above
each) and `docs/TEST-SCENARIO-API.md` (the same scenarios as a table, plus the seed fixture
they assume).

The collections are grouped by endpoint — resolve ids, employee list, employee list
validation, salary adjustment, salary validation/transport, project details, assignment
rejections, assignment create, department report, routing and method edges, Swagger — so a
reviewer can run one group or the whole file. `@host = http://localhost:5100` is declared once;
`@e001`-`@e005` and `@prj001`-`@prj006` alias the deterministic seed ids so no request carries a
bare magic number. Employee ids are additionally resolvable over HTTP
(`GET /api/employees?search=E004` returns id 6), which is called out rather than assumed.

The expectations are measured. A script executed all 63 requests against the running host and
compared each status with its own comment: 63 passed, 0 failed, and every enveloped body was
checked for `success` matching its status class. The single non-enveloped response is the
documented one: `Content-Type: text/plain` on the salary PATCH is a bare 415 produced by MVC
before any application code runs.

Re-runnability was proved rather than claimed. Two write groups exist: the salary group is a
closed cycle (12,000,000 -> 13,000,000 -> 30,000,000/promotion -> 12,000,000), and the
assignment group adds one row that no endpoint can delete. After a full run the database held
exactly the three accepted salary-history rows and one new assignment; applying the clean-up
block documented at the top of the file and running everything again gave another 63/63.
`SalaryHistories` never held a row from any of the eleven rejected salary requests, which is the
proof that the guard clauses run before either statement.

The scenario matrix also records what the API does **not** have: there is no
`GET /api/projects` list yet, so project ids can only come from the seed and
`GET /api/projects/{id}/details` is the closest thing to a lookup. That gap is stated in the doc
instead of being worked around.

## T27 — WebApp shell, layout, alerts and pager

T27 turns the MVC host from the template into the HR Workspace shell. The visual source is
`design-system/`, not a framework: `wwwroot/css/site.css` now contains the export's
`OD-LAYOUT-PRIMITIVES` block and `design-system/styles.css` verbatim (tokens, type scale,
buttons, panels, tables, pagination, states), followed by one labelled section for the pieces
the export does not ship. Bootstrap CSS, Bootstrap Icons and the scoped
`EnterpriseHR.WebApp.styles.css` link are gone from the layout; the `wwwroot/lib/bootstrap`
files stay on disk for a later cleanup, but nothing references them.

The export's shell is a sidebar with a topbar. The task requires top navigation, so
`_Layout.cshtml` composes the same brand, `.nav-item` (`.is-active` plus `aria-current="page"`)
and topbar tokens into one sticky `.site-header`, keeps the footer treatment, and adds the skip
link and icon sprite. The active route is derived from `RouteData`; the four links are
Tag-Helper routes to `Home`, `Employees`, `Projects` and `Dashboard`. At T27 only Home existed, so
the other three intentionally answered the branded 404; T28 adds the Employees screen and leaves
Projects and Dashboard on that 404 until their stages land. Below 640px the nav
takes its own row and below 480px the icons drop, which keeps all four labels on one line at
360px with no horizontal overflow.

`_Alerts.cshtml` renders `TempData["Success"]`, `["Warning"]` and `["Error"]` from the layout,
above `RenderBody()`. The export has no inline alert component, so `.alert` extends the existing
success/warning/error background and text pairs plus the toast border tints; it uses
`role="status"` for success, `role="alert"` for warnings and errors, and a dismiss button
removes the banner. `_Pager.cshtml` renders the export's `.pagination` markup from
`(Page, TotalPages, RouteValues)`, overriding only `page` so filters survive navigation.

`Program.cs` adds `UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}")`, and
`HomeController.Error` puts the original status back on the response, so `/Dashboard` answers a
branded 404 instead of an empty body. The middleware sits outside the `/api` branch, which
keeps the JSON envelope on unknown API routes: `/api/nope` still returns the 404 envelope with
`Content-Type: application/json`. The Home page is a page header and one card per module; no
POST form exists yet, and `_ValidationScriptsPartial` plus its jQuery assets are still served
for the forms that follow.

Verification was done against the running host, not the Razor source. The build is 0 warnings,
0 errors. In Chromium the home page, skip link (Tab, Enter moves focus to `main`), nav links and
404 page were exercised at 320, 360, 390, 430, 600, 820, 1024, 1440 and 1920 widths with no
horizontal overflow; the layout keeps one nav row at every width. Alert rendering was probed
with all three TempData keys including `<script>alert('xss')</script>` and `<em>` payloads,
which appeared as literal text, and the dismiss control removed the banner. The pager probe
rendered page 2 of 4 with `search` and `departmentId` preserved and `page` overridden, plus the
page-1-of-1 disabled state. Both probes were reverted. The API request collection was re-run
after the pipeline change: 63 passed, 0 failed; the seed snapshot was restored to
`4|10|9|6|7|0`.

## T28 — Employees index screen

T28 adds the first real screen, `EmployeesController.Index` plus `Views/Employees/Index.cshtml`.
The action builds an `EmployeeFilter` with `PageSize = 10` and `IsActive = includeInactive ? null
: true`, calls `IEmployeeService.GetPagedAsync` and `GetDepartmentOptionsAsync`, and hands the
`PagedResult<EmployeeDto>` to `EmployeeIndexViewModel` (`Result`, `Search`, `DepartmentId`,
`IncludeInactive`, `SelectList Departments`). The screen is a plain GET form, so every filter
lives in the query string and a filtered list can be bookmarked or reloaded: nothing on this page
writes. `IsActive` stays a server-side decision, exactly as in `GET /api/employees`; the checkbox
only widens the list to former staff.

Out-of-range pages are corrected with a redirect rather than rendered. `page=40` of one page,
`page=-3` and `page=0` all land on the clamped URL (`/Employees`, or the same filters with the
last real page), so the address bar, the pager and the rows cannot disagree. The redirect keeps
only non-default values, and writes `includeInactive=true` as a string because a bool in a
`RouteValueDictionary` would serialize as `True`.

The view reuses the export's components instead of new ones: `.page`/`.page-header`/`.page-actions`
with the Create-new button, `.filter-panel`/`.field`/`.input-prefix.search-control` for the form,
`.panel` + `.data-table` + `.badge active|inactive` for the directory, `.mobile-records` +
`.record-card` below 1024px, `.empty-state` for no results, and `_Pager` in `.panel-footer`.
Salary is formatted `N0` in the current culture; a null manager renders an em dash plus
screen-reader-only "No manager"; the Deactivate action is a POST form with the antiforgery token
and is omitted for an inactive row. Two components the export lacks are extended from its tokens:
`.checkbox-field` (no checkbox ships, so the native control takes `accent-color: var(--color-primary)`)
and a full-width `.filter-actions` row (the export filters live as you type and has no submit row).
`i-search` and `i-plus` were added to the layout's sprite from `design-system.html`.

Edit, Salary, Create and Deactivate are Tag-Helper targets for later stages and answer the branded
404 today, which is how the rest of the shell behaves.

Verification ran against the live host and seed fixture. Build is 0 warnings, 0 errors. Search
`E00` returned 8 active employees, `siti` returned 1, department 1 returned 4, and
`E00 + department 1` returned 4; `siti + department 3` returned 0 with the empty state and no
pager. `includeInactive=true` returned 10 with E005 Rudi Hartono carrying the Inactive badge and
no Deactivate action. With `PageSize` temporarily set to 3, the pager reported 3 pages with
`search`, `departmentId` and `includeInactive` preserved across a page click and `page=4`/`page=9`
redirected to page 3; `PageSize` was reverted to 10. Hostile query values
(`<script>alert(1)</script>`, `%`, `_`, `' OR 1=1--`, `siti & co`) were echoed HTML-encoded and
matched nothing. Chromium at 320/360/390/430/600/820/1024/1440/1920 widths showed no horizontal
overflow, the table above 1024px and record cards below it. `requests.http` still passes 63/63,
and the seed snapshot was restored to `4|10|9|6|7|0`.

## T29 - Employee create and edit forms

T29 adds the two write screens the directory already linked to: `EmployeesController.Create`
and `EmployeesController.Edit`, with `EmployeeCreateViewModel` and `EmployeeEditViewModel`.

Both view models compose the request contract instead of inheriting it. That is a deliberate
departure from "one model, one contract": an unfilled `<select>` posts an empty string, which
binds to `int` as *"The value '' is invalid."* - a message that names no correction. Making the
field `int?` with a named `[Required]` turns the same submit into *"Select a department."* The
rest of the attributes mirror `CreateEmployeeRequest` / `UpdateEmployeeRequest` one for one, and
the data layer stays the authority for everything a form cannot know: unique number and email,
the position's salary range, an unknown department, an inactive manager. The option lists
(`Departments`, `Positions`, `Managers`) are display data, never posted back; the controller
rebuilds them from `GetFormOptionsAsync` on every render, including after a rejected POST, so a
stale or tampered list cannot reach the binding layer.

GET fills the selects and defaults the join date to today. POST checks `ModelState`, maps to the
request contract, calls `CreateAsync` / `UpdateAsync`, and catches `BusinessRuleException` and
`ConflictException` into `ModelState.AddModelError(string.Empty, ...)`. Those two exception types
describe something the person at the form can correct, so the message becomes a form-level
`error-summary` and the form re-renders with every submitted value and every select intact.
Success sets `TempData["Success"]` and redirects to the directory (POST/Redirect/GET), so a
reload cannot post the record twice; `_Alerts` renders the message.

Edit is narrower than Create on purpose. Assumption A3 says `UpdateEmployeeSql` writes only the
name, email, department, manager and join date, so the page has no position or salary control:
it shows both as read-only text and links to the salary page. The manager list is built with
`GetFormOptionsAsync(excludeEmployeeId: id)`, so nobody can be made their own manager, and only
active employees appear. An unknown id answers `NotFound()` on GET and on POST, and a rejected
POST re-reads the number, position and salary from the stored row rather than trusting the body.
Both POST actions carry `[ValidateAntiForgeryToken]`; a POST without the token answers 400 and
writes nothing.

The views add no new components. They use `.panel`, `.form-grid` (two columns, one below
767px), `.field`, `.input-prefix` for the IDR amount, `.error-summary` for
`asp-validation-summary="ModelOnly"`, `.field-error` for `asp-validation-for`, and `.button`
primary/secondary in a `.panel-footer`. `site.css` gains one labelled `Employee forms (T29)`
section with four rules: `.form-actions` (the export only draws its submit row inside
`.dialog-actions`), `.field.full`, the error border for jQuery unobtrusive validation (it marks
controls with `.input-validation-error`, while the design system keys off `aria-invalid`, which
only the server-rendered pass sets), and the marker/indent reset that stops a one-line
`.error-summary` from rendering behind a stray bullet.

Verification ran against the live host, the seed fixture and a real browser. Build is 0
warnings, 0 errors. GET /Employees/Create lists 4 departments, 7 positions and 9 active managers
with `-- Select --` and `-- No manager --` first and nothing selected. Client-side validation
blocks an empty submit and shows all six messages. A valid create (`E014`, department 1, position
3, manager E001, 26,000,000, 2024-05-06) answered 302 to `/Employees` with the success alert and
the new active row; the database held `PositionId=3, ManagerId=1, Salary=26000000.00,
JoinDate=2024-05-06, IsActive=1`. A create with no manager posted `ManagerId=NULL`. A duplicate
email, a duplicate employee number, an out-of-range salary (`Salary must be between 6.000.000 and
12.000.000 for this position.`), an unknown department, an unknown position and an inactive
manager each re-rendered the form with the message in the summary and every input preserved;
the employee and salary-history counts were unchanged after all six. GET /Employees/Edit/6 lists
9 managers and does not include E004; it has no position or salary control, shows both read-only
(`Senior Developer`, `12.000.000`) and links `/Employees/Salary/6`. Editing E004 to department 4
and manager E003 answered 302 and stored exactly that, with salary and position unchanged.
Self-as-manager, an inactive manager and a duplicate email were each rejected as a form error.
`Edit/999` answers 404 on GET and on POST; a POST without the antiforgery token answers 400. The
read-only and validation states were checked in Chromium at 320/360/390/430/600/820/1024/1440/1920
with no horizontal overflow, one column below 767px and two above it. The shells nav matched
only `Index`, so the Employees item lost its active state on the new sub-screens; `_Layout.cshtml`
now matches by controller, which keeps Home active on `/` only. `requests.http` still
passes 63/63 and the seed snapshot was restored to `4|10|9|6|7|0`.

## Next

T30 onward adds the salary adjustment and deactivation screens, then the project list and detail
screens. Each new screen starts from the existing shell, `.page`, `.panel`, `.data-table`,
`.field` and now `.form-grid`; each new API endpoint gets a group in `requests.http` and a section
in `docs/TEST-SCENARIO-API.md`.
