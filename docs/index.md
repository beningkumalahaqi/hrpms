# Documentation

Long-form documentation for EnterpriseHRSystem. The root `README.md` is the short
front page: what the project is, how to run it, and where to read more. Everything that
would bloat it lives here.

| Document | Read it when you need |
| -------- | --------------------- |
| [getting-started.md](getting-started.md) | Prerequisites, `.env`, Docker Compose SQL Server, `db-init`, connection strings, the dev URL, reset and troubleshooting (including Apple Silicon). |
| [architecture.md](architecture.md) | Project responsibilities, dependency direction, DI wiring, transaction and error-translation rules, and the UI/design-system contract. |
| [database.md](database.md) | `EnterpriseHRDB` tables, constraints, indexes, `dbo.sp_GetDepartmentSummary`, seed fixtures, and the rules for `db/` scripts. |
| [TEST-SCENARIO-API.md](TEST-SCENARIO-API.md) | The API scenario matrix: every route, its success path and every failure path with the status to expect. Paired with `src/EnterpriseHR.Api/requests.http`. |
| [development-stages.md](development-stages.md) | The T01–T28 stage log: what each stage added and what is still pending. |

Related reference material that does not belong in `docs/`:

- `db/README.md` — rules and status of the numbered SQL init scripts.
- `design-system/` — exported HR Workspace UI: tokens, screens, component states. Start
  at `design-system/index.html`, then `DESIGN-HANDOFF.md` and `design-system.html`.
- `AGENTS.md` — working rules for coding agents, including where each kind of knowledge
  belongs.
- `agents/memory/` — local, uncommitted agent notes (verified environment facts and
  gotchas). Not user documentation.

Convention: one topic per file, kebab-case name, and link it from this index. Add a new
file instead of growing the README or this page.
