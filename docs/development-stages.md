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
| T19+ | Application features | pending |

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
`dotnet build EnterpriseHRSystem.sln` succeeds with no warnings, the MVC host serves on
<http://localhost:5100>, and the API serves Swagger UI on
<http://localhost:5200/swagger>.

T06 added the shared data-access wiring: `IDbConnectionFactory` with the
`Microsoft.Data.SqlClient` implementation, and `AddEnterpriseHrDataAccess`, which
validates `ConnectionStrings:EnterpriseHR` and registers the factory as a singleton. Both
hosts call it from `Program.cs`, so either one fails at startup with a clear message when
the connection string is missing. Repositories register as scoped in the same method as
they are added.

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

## T11–T18 — Data access

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

## Next

T19 onward adds the application features: the report repository, the service
implementations, the API controllers with exception-to-status-code middleware, and the Razor
screens ported from `design-system/`. Repository registration in
`AddEnterpriseHrDataAccess` lands with the first service that resolves one.
