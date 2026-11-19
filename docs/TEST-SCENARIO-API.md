# API test scenarios

The scenario matrix for the REST API: every implemented route, its success path, and the
failure paths the contract commits to. Run it with `src/EnterpriseHR.Api/requests.http`
(VS Code REST Client, JetBrains HTTP Client, or any `.http` runner). Every request in that
file carries a `# ->` comment stating the status and payload to assert.

`docs/TEST-SCENARIO-API.md` is the "what must hold" document. `requests.http` is the
"press this and see" document. The response shapes themselves live in
[architecture.md](architecture.md) -> "API conventions" and "Endpoints".

For acceptance, use [TEST-SCENARIOS.md](TEST-SCENARIOS.md): it carries these scenarios as an
API table plus the MVC browser table, with the measured result per row. This file keeps the
per-group detail behind those rows.

## Preconditions

```bash
docker compose up -d && docker compose run --rm db-init   # fixture in db/03_seed.sql
dotnet run --project src/EnterpriseHR.WebApp              # serves /api/** on port 5100
```

The expectations assume a freshly seeded database
(`4 departments, 10 employees, 9 active, 6 projects, 7 assignments, 0 salary histories`).
Two groups in `requests.http` write rows; both are closed cycles, so a full top-to-bottom run
leaves only the assignment row that the create scenario adds. Clean-up statements are at the
top of `requests.http`.

## Response envelope

Every API response, success and failure, is the same three keys:

```json
{ "success": true, "message": "OK", "data": { } }
```

| Situation | Status | `success` | `message` | `data` |
| --------- | ------ | --------- | --------- | ------ |
| Read / update OK | 200 | true | `"OK"`, or `"Salary updated."` | the payload |
| Assignment created | 201 | true | `"Created."` | the created values |
| Model binding / DataAnnotations failure | 400 | false | `"Validation failed."` | field-to-messages dict |
| `BusinessRuleException` | 400 | false | the rule's sentence | null |
| `NotFoundException` | 404 | false | the exception's sentence | null |
| Unmatched route | 404 | false | `"The requested resource was not found."` | null |
| Wrong method on a known route | 405 | false | `"The HTTP method is not supported for this resource."` | null |
| `ConflictException` | 409 | false | the conflict's sentence | null |
| Unhandled | 500 | false | `"An unexpected error occurred."` | null |

Two 4xx cases are deliberately **not** enveloped, because MVC produces them before any
application code runs: a malformed JSON parser key is still enveloped (400 through the
model-binding factory), but an unsupported request `Content-Type` returns a bare 415
`application/problem+json`. See "Transport failures" below.

## Seed fixture used by these scenarios

Ids are stable across a volume reset because the seed inserts in a fixed order. Ids do **not**
follow `EmployeeNumber` order.

| Alias | Id | Employee | Position | Salary | Allocation | Note |
| ----- | -- | -------- | -------- | ------ | ---------- | ---- |
| E001 | 1 | Budi Santoso | Engineering Manager | 35,000,000 | 0% | free for the create scenario |
| E002 | 4 | Siti Rahma | Senior Developer | 22,000,000 | 100% | PRJ-001 60% + PRJ-002 40% |
| E003 | 5 | Andi Wijaya | Junior Developer | 8,000,000 | 30% | PRJ-001 only |
| E004 | 6 | Dewi Lestari | Senior Developer | 12,000,000 | 0% | salary scenario target, at the band floor |
| E005 | 7 | Rudi Hartono | Junior Developer | 7,000,000 | 0% | **inactive** |

| Alias | Id | Project | Status |
| ----- | -- | ------- | ------ |
| PRJ-001 | 1 | ERP Revamp | Active |
| PRJ-002 | 2 | Mobile App | Active |
| PRJ-003 | 3 | HR Portal | Planning |
| PRJ-004 | 4 | Legacy Migration | Completed |
| PRJ-005 | 5 | Vendor Audit | Cancelled |
| PRJ-006 | 6 | Budget Tool | Active |

Salary bands come from `Positions`: Junior Developer 6,000,000-12,000,000, Senior Developer
12,000,000-25,000,000, Engineering Manager 25,000,000-40,000,000, HR Specialist
7,000,000-15,000,000, HR Manager 15,000,000-30,000,000, Accountant 8,000,000-18,000,000,
Finance Manager 18,000,000-35,000,000.

## S1 - `GET /api/employees`

Paged list of **active** employees with optional `search` (name, employee number, email;
255 chars), `departmentId`, `page` (1+), `pageSize` (1-100, default 10).

| # | Request | Expect |
| - | ------- | ------ |
| 1 | no query string | 200, `items` 9, `totalCount` 9, `totalPages` 1 |
| 2 | `?departmentId=1&page=1&pageSize=2` | 200, `items` 2, `totalCount` 4, `totalPages` 2 |
| 3 | `?search=dewi` | 200, one item, E004; case-insensitive |
| 4 | `?page=2&pageSize=4` | 200, `items` 4, `page` 2, `totalPages` 3 |
| 5 | `?page=99` | 200, `items` [] with the real `totalCount` 9 |
| 6 | `?isActive=false&pageSize=100` | 200, still 9 active only - the flag is ignored on purpose |
| 7 | `?departmentId=999` | 200, `items` [], `totalCount` 0 - empty, not 404 |
| 8 | `?search=%25` | 200, `items` [] - `%` is escaped to a literal, not a wildcard |
| 9 | `?pageSize=101` | 400, `data.pageSize` |
| 10 | `?pageSize=0` | 400, `data.pageSize` (no silent clamp) |
| 11 | `?page=0` | 400, `data.page` |
| 12 | `?pageSize=abc` | 400, `data.pageSize` |
| 13 | 256-character `search` | 400, `data.search` |

## S2 - `PATCH /api/employees/{id}/salary`

Body `{ newSalary, newPositionId?, reason }`. Updates `Salary`/`PositionId` and inserts the
matching `SalaryHistories` row in **one** transaction, then answers with the refreshed
`EmployeeDto` read back from the database (not an echo of the request).

| # | Request | Expect |
| - | ------- | ------ |
| 1 | E004, 13,000,000, no position | 200, `message` "Salary updated.", `salary` 13000000, `positionId` 2; one history row |
| 2 | E004, 13,000,000 again | 400, "Nothing to change." |
| 3 | E004, 11,999,999 | 400, outside 12,000,000-25,000,000 for Senior Developer |
| 4 | E004, 26,000,000 | 400, above the band |
| 5 | E004, position 999 | 400, "Target position not found." |
| 6 | E004, position 3 at 13,000,000 | 400, outside the Engineering Manager band |
| 7 | E004, position 3 at 30,000,000 | 200, `positionId` 3, `positionTitle` "Engineering Manager" |
| 8 | E004, restore 12,000,000 / position 2 | 200, closes the cycle |
| 9 | E005 (inactive) | 400, "Cannot adjust the salary of an inactive employee." |
| 10 | id 999 | 404, "Employee 999 was not found." |
| 11 | id 0 | 404, "Employee 0 was not found." |
| 12 | id `abc` | 404, generic envelope (route constraint `{id:int}` did not match) |
| 13 | body without `reason` | 400, `data.reason` |
| 14 | `newSalary` 0 | 400, `data.newSalary` |
| 15 | empty body `{}` | 400, `data.reason` + `data.newSalary` |
| 16 | truncated JSON | 400, `data` carries the parser path `$` and a `request` key |
| 17 | `Content-Type: text/plain` | **415** bare RFC 9110 body, not the envelope |

Every rejected request in this group writes nothing: the guard clauses all run before either
statement, and both statements share the transaction. Verify with:

```sql
SELECT Id, EmployeeId, OldSalary, NewSalary, ChangeReason FROM dbo.SalaryHistories ORDER BY Id;
SELECT Id, Salary, PositionId FROM dbo.Employees WHERE Id = 6;
```

## S3 - `GET /api/projects/{id}/details`

One project plus its `members` array from a single `QueryMultipleAsync` batch, including
members who have since been soft-deleted (`isEmployeeActive: false`).

| # | Request | Expect |
| - | ------- | ------ |
| 1 | PRJ-001 | 200, Active, 2 members, `totalAllocationOfMembers` 90 |
| 2 | PRJ-003 | 200, Planning, 1 member |
| 3 | PRJ-004 | 200, Completed - a non-accepting status still reads |
| 4 | id 999 | 404, "Project not found." |
| 5 | id `abc` | 404, generic envelope |

## S4 - `POST /api/projects/{id}/assign`

Body `{ employeeId, roleInProject, allocationPercentage }`. The percentage is a share of the
employee's **total** workload across projects, not of this project. The employee row is locked
before the sum is taken, so two concurrent assignments cannot both pass the cap.

| # | Request | Expect |
| - | ------- | ------ |
| 1 | PRJ-001, E002 (already assigned) | 409, "Employee is already assigned to this project." |
| 2 | PRJ-003, E002 at 10% | 409, "Allocation would reach 110% ..." |
| 3 | PRJ-001, E005 (inactive) | 400, "Employee is not active." |
| 4 | PRJ-004 (Completed), E001 | 400, "Project status 'Completed' does not accept new assignments." |
| 5 | PRJ-005 (Cancelled), E001 | 400, same for 'Cancelled' |
| 6 | project 999 | 404, "Project 999 was not found." |
| 7 | employee 999 | 404, "Employee 999 was not found." |
| 8 | `allocationPercentage` 0 | 400, `data.allocationPercentage` |
| 9 | `allocationPercentage` 101 | 400, `data.allocationPercentage` |
| 10 | no `roleInProject` | 400, `data.roleInProject` |
| 11 | empty body `{}` | 400, `data.employeeId` + `data.roleInProject` |
| 12 | truncated JSON | 400, `data` carries `$.employeeId` and `request` |
| 13 | PRJ-006, E001 at 40% | **201**, `message` "Created.", body `{ assignmentId, projectId 6, employeeId 1, allocationPercentage 40 }`, `Location` header |
| 14 | then `GET /api/projects/6/details` | 200, member count +1, `totalAllocationOfMembers` 80 |

Scenarios 1-12 must leave `dbo.ProjectAssignments` at its seed count of 7 rows.

## S5 - `GET /api/reports/department-summary`

Runs `dbo.sp_GetDepartmentSummary` through async Dapper; `data` is the array itself.

| # | Request | Expect |
| - | ------- | ------ |
| 1 | `/api/reports/department-summary` | 200, 4 rows ordered FIN, HR, IT, OPS (see below) |
| 2 | `/api/Reports/Department-Summary` | 200, identical - routing is case-insensitive |
| 3 | trailing slash | 200, identical |
| 4 | `/api/reports` | 404 envelope |

| Code | Budget | Active | Payroll | Remaining | ActivePrj | IsOverBudget |
| ---- | ------ | ------ | ------- | --------- | --------- | ------------ |
| FIN | 50,000,000 | 3 | 48,000,000 | 2,000,000 | 1 | false |
| HR | 60,000,000 | 2 | 38,000,000 | 22,000,000 | 0 | false |
| IT | 70,000,000 | 4 | 77,000,000 | **-7,000,000** | 2 | **true** |
| OPS | 20,000,000 | 0 | **0** | 20,000,000 | 0 | false |

This endpoint is read-only: after the group, the fixture must be unchanged.

## S6 - routing, method and transport edges

| # | Request | Expect |
| - | ------- | ------ |
| 1 | `GET /api/nope` | 404 envelope |
| 2 | `GET /api/employees/1` | 404 envelope - no read-by-id endpoint exists yet |
| 3 | `DELETE /api/employees` | 405 envelope |
| 4 | `POST /api/reports/department-summary` | 405 envelope |
| 5 | `GET /api/projects/1/assign` | 405 envelope |
| 6 | `PUT /api/employees/6/salary` | 405 envelope |
| 7 | `PATCH .../salary` with `text/plain` | 415, RFC 9110 body (see envelope note) |

## S7 - Swagger

| # | Request | Expect |
| - | ------- | ------ |
| 1 | `GET /swagger/index.html` | 200 in Development; 404 in Production |
| 2 | `GET /swagger/v1/swagger.json` | 200, five paths `/api/Employees`, `/api/Employees/{id}/salary`, `/api/Projects/{id}/details`, `/api/Projects/{id}/assign`, `/api/reports/department-summary` |

## Not covered - no endpoint exists yet

These are in the practical-exam scope but not implemented, so they have no scenario here:

- Employee create, read-by-id, update, delete.
- The project list (`GET /api/projects`). Project ids can therefore only come from the seed;
  `GET /api/projects/{id}/details` is the closest thing to a lookup over HTTP.

When those land, add their groups here and mirror them in `requests.http`.
