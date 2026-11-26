# Test scenarios

The acceptance matrix for EnterpriseHRSystem: one table for the REST API, one for the MVC
screens. Every row is a scenario that was run against the seeded database and the running
host; the **Actual** column records what the run returned, not what the code looks like.

Use this file to accept a build. Use [TEST-SCENARIO-API.md](TEST-SCENARIO-API.md) when you
want the per-group detail behind each API row, and
[`src/EnterpriseHR.Api/requests.http`](../src/EnterpriseHR.Api/requests.http) when you want a
runnable collection with a `# ->` assertion above every request.

## Preconditions

Both tables start from the seed fixture in `db/03_seed.sql`:

```bash
docker compose up -d
docker compose run --rm db-init
dotnet run --project src/EnterpriseHR.WebApp      # http://localhost:5100
```

The seed is: 4 departments, 10 employees (9 active), 16 projects, 7 assignments, 0 salary
history rows. The rows below use these stable ids and numbers, which the seed always
produces:

| Entity | Id | Numbered | Notes |
| ------ | -- | -------- | ----- |
| Dewi Lestari | employee 6 | E004 | Senior Developer, 12.000.000, band 12.000.000 to 25.000.000 |
| Siti Rahma | employee 4 | E002 | 100% allocated across PRJ-001 and PRJ-002 |
| Andi Wijaya | employee 5 | E003 | 30% on PRJ-001 |
| Rudi Hartono | employee 7 | E005 | inactive |
| Budi Santoso | employee 1 | E001 | manages 3 active employees |
| Lina Marlina | employee 10 | E010 | 1 active project assignment |
| Hadi Gunawan | employee 9 | E009 | no active project assignment, so it can be deactivated |
| ERP Revamp | project 1 | PRJ-001 | Active, 2 active members, 90% allocated |
| Mobile App | project 2 | PRJ-002 | Active |
| HR Portal | project 3 | PRJ-003 | Planning |
| Legacy Migration | project 4 | PRJ-004 | Completed |
| Vendor Audit | project 5 | PRJ-005 | Cancelled |

Rows that write (A02 to A04, A07, A11, M03 to M06, M10, M13) are run in that order and the
database is restored afterwards. The restore statements are at the end of this file.

## API matrix

Run against `http://localhost:5100`. Every response, success or failure, is the envelope
`{ success, message, data }` unless the row says otherwise.

| # | Given | When | Expected | Actual (measured) |
| - | ----- | ---- | -------- | ----------------- |
| A01 | Seeded database, 4 active employees in department 1 (IT) | `GET /api/employees?departmentId=1&page=1&pageSize=2` | 200 envelope; 2 items; `totalCount` 4; `page` 1 | 200, `success: true`, 2 items, `totalCount` 4, `page` 1, `pageSize` 2 |
| A02 | E004 active, Senior Developer, salary 12.000.000 | `PATCH /api/employees/6/salary` with `newSalary: 11999999` | 400 envelope naming the position band; employee row and salary history unchanged | 400, "New salary 11.999.999 is outside the allowed range 12.000.000 – 25.000.000 for position 'Senior Developer'."; DB stayed `7 assignments / 0 history / 12.000.000 / position 2` |
| A03 | E004 active, salary 12.000.000 | `PATCH /api/employees/6/salary` with `newSalary: 13000000` | 200 envelope; salary 13.000.000; exactly one salary-history row | 200, "Salary updated.", `salary` 13000000, `positionId` 2; `historyRows=1`, `12000000.00->13000000.00` |
| A04 | E004 active, salary 13.000.000, Senior Developer | `PATCH /api/employees/6/salary` with `newSalary: 30000000, newPositionId: 3`, then retry `newSalary: 20000000, newPositionId: 3` | First 200 (promotion to Engineering Manager at 30.000.000); second 400 because 20.000.000 is below that band; salary, position and history unchanged by the retry | 200 then 400 with the band message for 'Engineering Manager'; DB `7\|2\|30000000.00/3` before and after the rejected retry |
| A05 | E005 (employee 7) is inactive | `PATCH /api/employees/7/salary` with `newSalary: 9000000` | 400 envelope; no mutation | 400, "Cannot adjust the salary of an inactive employee."; assignment, history and salary columns unchanged |
| A06 | E002 already holds 100% across Active/Planning projects | `POST /api/projects/3/assign` with `employeeId: 4, allocationPercentage: 10` | 409 envelope giving current, requested and total; no assignment row | 409, "Allocation would reach 110% (current 100% + requested 10%). Maximum is 100%."; 7 assignments before and after |
| A07 | E003 at 30% on PRJ-001; PRJ-002 is Active | `POST /api/projects/2/assign` with `employeeId: 5, roleInProject: "Developer", allocationPercentage: 50` | 201 envelope with the new assignment id; row persisted; total allocation 80% | 201, "Created.", `data.assignmentId` is the new identity, `projectId` 2, `employeeId` 5, 50%; rows 7 to 8; E003 total 30 to 80 |
| A08 | E005 is inactive; PRJ-001 is Active | `POST /api/projects/1/assign` with `employeeId: 7` | 400 envelope; no assignment row | 400, "Employee is not active."; assignment count unchanged |
| A09 | PRJ-004 is Completed and PRJ-005 is Cancelled | `POST /api/projects/4/assign` and `POST /api/projects/5/assign` with `employeeId: 1` | 400 envelope for both, naming the project status; no assignment row | 400 twice, messages name Completed and Cancelled; assignment count unchanged |
| A10 | E002 is already assigned to PRJ-001 | `POST /api/projects/1/assign` with `employeeId: 4` again | 409 envelope; no duplicate row | 409, "Employee is already assigned to this project."; assignment count unchanged |
| A11 | E004 has one assignment, on Completed PRJ-004, so no counted workload | `POST /api/projects/1/assign` with `employeeId: 6, allocationPercentage: 100` | 201; the Completed assignment is excluded, so 100% is accepted; PRJ-001 gains the member | 201, "Created.", 100%; PRJ-001 then reports 3 members and `totalAllocationOfMembers` 190 |
| A12 | PRJ-001 has 2 active members; project 999 does not exist | `GET /api/projects/1/details` and `GET /api/projects/999/details` | 200 with project and 2 members; 404 envelope for 999 | 200, PRJ-001, `status` Active, 2 members, 90 total allocation; 404, "Project not found." |
| A13 | Seeded database; IT payroll 77.000.000 against a 70.000.000 budget; Operations has no employees | `GET /api/reports/department-summary` | 200 with 4 rows from the stored procedure; IT `isOverBudget` true with remaining -7.000.000; OPS headcount and payroll 0 | 200, 4 rows: FIN 3 / 48.000.000 / remaining 2.000.000, HR 2 / 38.000.000 / 22.000.000, IT 4 / 77.000.000 / **-7.000.000** / over, OPS 0 / 0 / 20.000.000 |
| A14 | A malformed body, an unknown route and a wrong method are all reachable | `POST /api/projects/1/assign` with `employeeId: 0, roleInProject: "", allocationPercentage: 0`; `GET /api/nope`; `DELETE /api/employees`; `PUT /api/employees/6/salary` | 400, 404, 405, 405, each in the standard envelope | 400 "Validation failed." with `employeeId`/`roleInProject`/`allocationPercentage` keys; 404 envelope; 405 envelope; 405 envelope |
| A15 | Search text is user input and must not act as a wildcard | `GET /api/employees?search=%25` (a literal `%`) | 200 with 0 items; the LIKE wildcard is escaped | 200, `items: []`, `totalCount` 0 |
| A16 | Paging bounds are 1 to 100 | `GET /api/employees?pageSize=101`, `?pageSize=0`, `?page=0` | 400 envelope per request, naming the offending field | 400 with `pageSize` key twice and `page` key once, messages "must be between 1 and 100" and "must be between 1 and 2147483647" |
| A17 | The list is active-only by design | `GET /api/employees?isActive=false&pageSize=100` | 200 with the 9 active employees; the filter is not honoured because the endpoint does not expose inactive staff | 200, 9 items, all `isActive: true` |
| A18 | Employee 999 does not exist | `PATCH /api/employees/999/salary` with a valid body | 404 envelope; no history row | 404 envelope, message "Employee 999 was not found." |
| A19 | An empty JSON object and truncated JSON both reach model binding | `PATCH /api/employees/6/salary` with `{}`, then with `{"newSalary": 13000` | 400 envelope listing the missing fields; the truncated body is a 400 with the JSON error path | `{}` returned `reason` and `newSalary` keys; truncated body returned `request` plus `$.newSalary` "invalid end of a number" |
| A20 | The API is JSON only | `PATCH /api/employees/6/salary` with `Content-Type: text/plain` | 415; framework-generated body, not the envelope | 415 with `application/problem+json`, `"title": "Unsupported Media Type"` |
| A21 | No endpoint is mounted at the API root | `GET /api` | 404 envelope | 404, "The requested resource was not found." |
| A22 | SQL Server is stopped while the host is running | `GET /api/reports/department-summary` | 500 envelope with a generic message and no stack trace | 500, `{"success":false,"message":"An unexpected error occurred.","data":null}` |

## MVC browser matrix

Run in a browser against `http://localhost:5100`. Selectors are given where a row depends on
a specific control.

| # | Given | When | Expected | Actual (measured) |
| - | ----- | ---- | -------- | ----------------- |
| M01 | Seeded database, 9 active employees | Open `/Employees`; search `dewi`; search `E010`; filter department 1; request `?page=2` and `?page=0` | Default list shows the 9 active employees with "Active employees only"; name and number search each return one row; IT filter returns 4 rows and keeps the select on 1; a page beyond the last redirects to the first | Default: 9 rows, chip "9 employees", "Active employees only, sorted by name.", "Page 1 of 1"; `dewi` 1 row (E004); `E010` 1 row; `departmentId=1` 4 rows, `#departmentId` value `1`; both out-of-range pages 302 to `/Employees` |
| M02 | E005 is the only inactive employee | Open `/Employees`, note the checkbox, then open `?includeInactive=true` | Checkbox drives the filter; default excludes inactive staff; enabling it adds E005 with an Inactive badge | Default: `#includeInactive` unchecked, 9 rows; enabled: checked, chip "10 employees", "Active and former employees, sorted by name.", E005 present with an `Inactive` badge on a single page |
| M03 | A valid new employee: E011, Operations, Senior Developer, manager E001, 15.000.000 | Open `/Employees/Create`, inspect the three selects, submit | Department, position and manager options come from the database; the employee is created and a success alert appears | 4 departments, 7 positions, 9 active managers, JoinDate defaulted to today; created id 40 with dept 4 / position 2 / manager 1 / 15.000.000 / active; alert "Employee E011 - T34 Test Employee was created." |
| M04 | `dewi.lestari@enterprisehr.local` belongs to E004 | Submit Create with that email and a new E012 number | Clear error on the form, submitted values kept, no row inserted | Form re-rendered with summary "Email 'dewi.lestari@enterprisehr.local' is already in use."; number, name, email, department, position and salary kept; selects rebuilt (5 department options); 0 rows for E012 |
| M05 | Employee 40 exists, Operations, Senior Developer, 15.000.000 | Open `/Employees/Edit/40`, inspect the controls, change name, email, department and manager, submit | Only name, email, join date, department and manager are editable; position and salary are read-only; the update persists and writes no salary history | Salary and position rendered as `readonly` inputs `#salaryAmount` (15.000.000) and `#positionTitle`; update moved department 4 to 2 and manager 1 to 2, salary and position unchanged, `salaryHistoryRows=0`; alert "Employee E011 - T34 Renamed Employee was updated." |
| M06 | E009, E001, E010, E005 and a missing id 999 | Attempt deactivation for each | E009 succeeds; E001 and E010 show the blocking rule; E005 is a no-op success; 999 renders Not Found | E009: success alert, `IsActive=0`, row still exists. E001: "Cannot deactivate: employee manages 3 active employee(s).", `IsActive=1`. E010: "Cannot deactivate: employee is still assigned to 1 active project(s).", `IsActive=1`. E005: 302 with warning "Employee E005 - Rudi Hartono is already inactive.", `IsActive=0`. 999: branded 404 page |
| M07 | Seeded projects | Open `/Projects` | All 16 projects with department, start and end dates, status badge and active member count; PRJ-001 shows 2 and the ten additive projects show 0 | 16 rows, chip "16 projects"; every row shows department, both dates (open-ended rows show the "No end date" marker), status badge and count; PRJ-001 = 2, PRJ-002–PRJ-006 = 1, PRJ-007–PRJ-016 = 0 |
| M08 | PRJ-001 exists; project 999 does not | Open `/Projects/Details/1`, then `/Projects/Details/999` | Details show code, department, dates, 2 active members and the member table; 999 renders Not Found | PRJ-001: Active badge, code PRJ-001, Information Technology, Jan 5 2026 to Dec 31 2026, "Active team members = 2", "Open for team assignments", members E003 30% and E002 60%; 999: branded 404 |
| M09 | E002 is at 100% across Active/Planning projects | On `/Projects/Details/3`, add E002 at 10% | Warning naming current, requested and total percentages; no assignment saved; member list unchanged | Warning "Allocation would reach 110% (current 100% + requested 10%). Maximum is 100%."; assignments stayed 7; 0 rows for E002 on PRJ-003; PRJ-003 still shows 1 member |
| M10 | E003 is at 30% | On `/Projects/Details/2`, add E003 as Developer at 50% | Success feedback; member list refreshes with the new row; total allocation 80% | Alert "Andi Wijaya (E003) was added to PRJ-002 as Developer at 50%."; member list 2 rows (E003 50%, E002 40%); E003 total allocation 80 |
| M11 | PRJ-004 is Completed | Open `/Projects/Details/4`; then POST the assign route directly | Add-member form is not rendered; a hand-crafted POST is rejected | "Closed for new assignments", 0 forms on the page, no `#assignTitle`; direct `POST /Projects/Assign/4` returned 400 |
| M12 | IT payroll exceeds its budget; Operations has no employees | Open `/` (the dashboard) | IT row carries the over-budget treatment and an "Over budget" badge; FIN, HR and OPS are normal; OPS is still shown with zeros | IT row `class="over-budget"`, cell background `rgb(253, 236, 239)`, badge `badge over` "Over budget", remaining `STRONG.negative` -7.000.000; FIN, HR and OPS badges `badge within`; OPS row present with 0 headcount and 0 payroll |
| M13 | E004 is active, Senior Developer, 12.000.000 | Open `/Employees/AdjustSalary/6`, submit 13.000.000, then 11.999.999 | Valid change succeeds and writes one audit row; invalid change shows the band error and leaves employee and history unchanged | 13.000.000: success alert, salary 13.000.000, one history row `12000000.00->13000000.00`, reason stored. 11.999.999: error summary with the band message, submitted salary and reason kept, salary still 13.000.000, history still 1 row |
| M14 | The Create form is submitted empty | Open `/Employees/Create` and submit without filling anything | Six field errors appear and no employee is inserted | Errors "Enter an employee number.", "Enter the employee's full name.", "Enter an email address.", "Select a department.", "Select a position.", "Enter a monthly salary."; no row created |
| M15 | A client posts a form without the antiforgery token | `POST /Employees/Create` with a bare body | 400 from antiforgery validation; no employee inserted | 400 |
| M16 | A wrong method is used on a GET-only page; an unknown path is requested | `POST /Projects/Details/1`; `GET /nope`; `GET /Employees/Edit/999`; `GET /Employees/AdjustSalary/999` | Each returns its status with the branded error page, not a raw stack trace | 405 branded "Error - EnterpriseHR"; 404; 404; 404 |
| M17 | M03 left 10 active and 11 total employees, so the list now spans two pages | Open `/Employees?includeInactive=true`, then follow the Next link | Chip counts all 11; page 1 holds 10 rows with "Page 1 of 2"; Next loads page 2 with the remaining row and "Page 2 of 2" | Chip "11 employees", 10 rows, "Page 1 of 2"; Next went to `/Employees?includeInactive=true&page=2` showing E011 and "Page 2 of 2" |

## Restoring the seed after a run

The write rows leave rows behind that have no HTTP delete path. Run this between full passes
and before accepting the build. The pair list mirrors the seven assignments in
`db/03_seed.sql`; keep the two in step if the seed changes.

```sql
DELETE FROM dbo.ProjectAssignments WHERE NOT EXISTS (
  SELECT 1
  FROM (VALUES ('PRJ-001','E002'), ('PRJ-002','E002'), ('PRJ-001','E003'),
               ('PRJ-004','E004'), ('PRJ-003','E007'), ('PRJ-005','E009'),
               ('PRJ-006','E010')) AS seed (ProjectCode, EmployeeNumber)
  JOIN dbo.Projects p ON p.ProjectCode = seed.ProjectCode
  JOIN dbo.Employees e ON e.EmployeeNumber = seed.EmployeeNumber
  WHERE p.Id = dbo.ProjectAssignments.ProjectId
    AND e.Id = dbo.ProjectAssignments.EmployeeId
);
```

That keeps only the seed pairs, so it also clears anything a pass added. Then:

```sql
DELETE FROM dbo.SalaryHistories;
DELETE FROM dbo.Employees WHERE EmployeeNumber = 'E011';

UPDATE dbo.Employees SET Salary = 12000000.00, PositionId = 2 WHERE EmployeeNumber = 'E004';
UPDATE dbo.Employees SET Salary = 22000000.00, PositionId = 2 WHERE EmployeeNumber = 'E002';
UPDATE dbo.Employees SET IsActive = 1 WHERE EmployeeNumber = 'E009';
```

Then confirm the fixture is back:

```sql
SELECT CONCAT(
  (SELECT COUNT(*) FROM dbo.Departments), '|',
  (SELECT COUNT(*) FROM dbo.Employees), '|',
  (SELECT COUNT(*) FROM dbo.Employees WHERE IsActive = 1), '|',
  (SELECT COUNT(*) FROM dbo.Projects), '|',
  (SELECT COUNT(*) FROM dbo.ProjectAssignments), '|',
  (SELECT COUNT(*) FROM dbo.SalaryHistories));
```

Expected: `4|10|9|6|7|0`.

## Notes on what is not in the matrix

- `Home/Privacy` was deleted in T34 along with its controller action, so `/Home/Privacy` answers the
  branded 404. `Home/Index` is still an unlinked page: it renders the three module cards and is
  reachable only by URL, so it has no acceptance row either.
- The `GET /api/employees/{id}` read, the project list endpoint and the employee write endpoints
  under `/api/**` are not implemented; see "Not covered" in
  [TEST-SCENARIO-API.md](TEST-SCENARIO-API.md). The MVC screens cover the same operations
  through `IEmployeeService` and `IProjectService`.
- The API returns a framework `application/problem+json` body for 415 and for a malformed JSON
  document that fails before model binding (A19, A20). That is ASP.NET Core behaviour, not the
  envelope; the contract commits to the envelope only for statuses the application produces.
