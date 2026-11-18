# Database

`EnterpriseHRDB` on SQL Server 2022. The schema, stored procedure and seed data are
created by numbered scripts in `db/`, run by the `db-init` service — the container image
only provides the server. Setup steps are in [getting-started.md](getting-started.md);
the rules for new scripts are in `db/README.md`.

## Scripts

| File | Contents |
| ---- | -------- |
| `db/01_schema.sql` | `EnterpriseHRDB` database, six tables, named keys/constraints/indexes |
| `db/02_sp_GetDepartmentSummary.sql` | `dbo.sp_GetDepartmentSummary` department report |
| `db/03_seed.sql` | Test-matrix seed data for five of the six tables |

Scripts run in filename order and must stay idempotent. `01_schema.sql` guards every
object with `IF OBJECT_ID(..., 'U') IS NULL` and every index with an `IF NOT EXISTS` check
against `sys.indexes`, but it never alters an existing table: after changing a table
definition, recreate the volume (`docker compose down -v`) before running `db-init`
again. `02_...` uses `CREATE OR ALTER PROCEDURE`. `03_seed.sql` skips itself entirely once
`dbo.Departments` has a row.

## Schema

Six tables, created in dependency order, with named `PK_`, `UQ_`, `FK_`, `CK_` and `DF_`
constraints plus indexes on the main foreign-key columns.

| Table | Key columns and constraints |
| ----- | --------------------------- |
| `Departments` | `DepartmentCode` unique; `Budget >= 0`. |
| `Positions` | `Title`, `MinSalary`, `MaxSalary`; `MaxSalary >= MinSalary`. |
| `Employees` | `EmployeeNumber` and `Email` unique; FK to `Departments` and `Positions`; self-referencing `ManagerId`; `Salary >= 0`; `IsActive` defaults to 1; a manager cannot be the employee themselves. |
| `Projects` | `ProjectCode` unique; FK to `Departments`; `Status` limited to `Planning`, `Active`, `Completed`, `Cancelled`; `EndDate >= StartDate` when set. |
| `ProjectAssignments` | FKs to `Projects` and `Employees`; unique `(ProjectId, EmployeeId)`; `AllocationPercentage` between 1 and 100; `AssignedDate` defaults to `GETDATE()`. |
| `SalaryHistories` | FK to `Employees`; `OldSalary`, `NewSalary`, `ChangeReason`; `ChangedAt` defaults to `GETDATE()`. |

Indexes: `IX_Employees_DepartmentId`, `IX_Employees_PositionId`,
`IX_Employees_ManagerId`, `IX_Projects_DepartmentId`,
`IX_ProjectAssignments_EmployeeId`, `IX_SalaryHistories_EmployeeId`.

The database collation is `SQL_Latin1_General_CP1_CI_AS`, so string comparisons
(including the unique indexes on `Email` and `EmployeeNumber`) are case-insensitive
without `LOWER()`. Wrapping a column in a function would defeat those indexes.

## `dbo.sp_GetDepartmentSummary`

One row per department with active headcount, active payroll, remaining budget and active
project count. Employees and projects are aggregated in separate CTEs first; joining both
detail tables directly would multiply rows and inflate `COUNT`/`SUM`. The result-set
column aliases are the `DepartmentSummaryDto` property names and must stay that way. `OPS`
has no employees, which exercises the `LEFT JOIN`, and `IT` is deliberately over budget.

## Seed data

`db/03_seed.sql` is the fixture set the dashboard and API are tested against:

| Table | Rows | Notes |
| ----- | ---- | ----- |
| `Departments` | 4 | `IT` 70,000,000; `HR` 60,000,000; `FIN` 50,000,000; `OPS` 20,000,000 (no employees). |
| `Positions` | 7 | Monthly IDR ranges, junior developer 6,000,000–12,000,000 through the manager bands. |
| `Employees` | 10 | Three top-level managers with `ManagerId = NULL`, inserted first because the FK is self-referencing; `E005` is inactive. |
| `Projects` | 6 | Mixed statuses across `IT`, `HR`, `FIN`. |
| `ProjectAssignments` | 7 | `AssignedDate` mirrors the project start date so the seed is deterministic. |
| `SalaryHistories` | 0 | Left empty on purpose. |

Foreign keys are resolved with subselects on the natural keys (`DepartmentCode`,
`Title`, `EmployeeNumber`, `ProjectCode`), never hard-coded identity values. The inserts
run in one transaction with `XACT_ABORT ON`, so a failed seed rolls back instead of
leaving a partial data set the guard would then skip forever. To reseed from scratch,
recreate the volume and re-run `db-init`.

## Alignment with the domain layer

The entity classes in `src/EnterpriseHR.Domain/Entities/` mirror the columns one-to-one,
and the `DataAnnotations` on the request types mirror the column lengths
(`EmployeeNumber` 20, `FullName` 255, `Email` 150 + email format, `RoleInProject` 100,
`Reason` 255, `AllocationPercentage` 1–100). When a column changes, all three places move
together: script, entity/request type, and any SQL that selects or writes it.
