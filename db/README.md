# db/

SQL Server initialization scripts for `EnterpriseHRDB`.

Schema, constraint and seed-fixture reference: [../docs/database.md](../docs/database.md).
This file covers the script rules and the state of each file.

Scripts are picked up by the `db-init` service in `docker-compose.yml`, which
runs every file matching `0*.sql` in filename order against the `sqlserver`
container after it reports healthy.

| Prefix | Contents | Status |
| ------ | -------- | ------ |
| `01_schema.sql` | Database and schema creation | present (T02) |
| `02_sp_GetDepartmentSummary.sql` | `dbo.sp_GetDepartmentSummary` department report | present (T03) |
| `03_seed.sql` | Seed data: 4 departments, 7 positions, 10 employees, 16 projects, 7 assignments | present (T04) |

Rules for these scripts:

- **Idempotent.** Each script must be safe to run more than once, because
  `db-init` is re-run manually with `docker compose run --rm db-init`.
- **Self-contained.** No interactive prompts and no SQLCMD variables beyond the
  defaults; `db-init` invokes `sqlcmd` with `-b` so any error aborts the run.
- **Ordered.** Filenames must keep a numeric prefix so the glob order is stable.

`01_schema.sql` creates `EnterpriseHRDB` and its six tables. It guards every object with
`IF OBJECT_ID(..., 'U') IS NULL` and every index with an `IF NOT EXISTS` check on
`sys.indexes`, but it never alters an existing table. After changing a table definition,
recreate the local volume with `docker compose down -v` before running `db-init` again.

Expect `db-init` to print `Running /scripts/01_schema.sql` followed by
`Changed database context to 'EnterpriseHRDB'.` on both the first and any later run.

`02_sp_GetDepartmentSummary.sql` uses `CREATE OR ALTER PROCEDURE`, so re-running it
replaces the definition instead of failing. Its result-set column aliases are the
`DepartmentSummaryDto` property names and must stay that way.

`03_seed.sql` guards the base seed with `IF NOT EXISTS (SELECT 1 FROM dbo.Departments)`, then
appends ten extra projects (`PRJ-007` to `PRJ-016`) with a per-code `NOT EXISTS`, so an existing
database can receive them without a reseed. Foreign keys are resolved with
subselects on `DepartmentCode`, `Title`, `EmployeeNumber` and `ProjectCode`, never
hard-coded identity values, and employees are inserted managers-first because
`ManagerId` references `Employees`. It leaves `SalaryHistories` empty. The rows are the
test matrix fixture: IT active payroll (77,000,000) deliberately exceeds its 70,000,000
budget, `OPS` has no employees, and E005 is inactive. To reseed from scratch, recreate the
volume (`docker compose down -v`) and re-run `db-init`.
