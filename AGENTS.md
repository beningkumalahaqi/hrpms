# AGENTS.md

Guidance for coding agents working in this repository. Read this before starting a task.

## The project in short

- EnterpriseHRSystem: .NET 8 / C# 12 HR management app on SQL Server 2022 (`EnterpriseHRDB`).
- Four projects under `src/`: `EnterpriseHR.Domain`, `EnterpriseHR.DataAccess`,
  `EnterpriseHR.WebApp` (ASP.NET Core MVC + Razor Views), `EnterpriseHR.Api`
  (controller-based Web API + Swagger).
- Data flow: WebApp/Api → DataAccess (Dapper + `Microsoft.Data.SqlClient`) → SQL Server.
  Stored procedures and schema live in `db/`.
- No EF Core, no SPA, no separate frontend. `README.md` is a short front page (what the
  project is, quick start, status, links); the authoritative setup, architecture,
  database and scope documents live in `docs/`.
- Out of scope unless the task says otherwise: authentication, migrations, test projects,
  a fifth project, and .NET app containers.

## Where things live

| Path | What it is | Use it for |
| ---- | ---------- | ---------- |
| `design-system/` | Exported HR Workspace UI reference (tokens, screens, component states) | Visual source of truth for UI work. Start at `design-system/index.html`, then `DESIGN-HANDOFF.md` and `design-system.html`. Match typography, spacing, colors, states; do not invent a new look. |
| `docs/` | Long-form user documentation | `index.md` (index), `getting-started.md` (setup, config, troubleshooting), `architecture.md` (projects, data-access rules, design system, scope), `database.md` (schema, procedure, seed), `development-stages.md` (stage log). Add a file and link it from `docs/index.md` instead of growing the README. |
| `agents/memory/` | Local agent memory: verified facts, decisions, gotchas | Read before touching an area; start at `agents/memory/1index.md`. Working notes, not user docs. |
| `db/` | Numbered SQL init scripts run by `db-init` | Schema (`01_*.sql`), procedures (`02_*.sql`), seed (`03_*.sql`). |
| `src/` | The four .NET projects | Application code. |
| `README.md` | Short front page: purpose, projects, quick start, status, doc links | What a human reviewer reads first. Keep it short; detail goes in `docs/`. |

Design facts go in `design-system/`, explanations and contracts in `docs/`, operational
knowledge in `agents/memory/`. Do not duplicate content across them.

## What agents should do

1. **Read first.** `README.md`, `docs/index.md` plus the doc for the area you are touching
   (`getting-started.md`, `architecture.md`, `database.md`, `development-stages.md`),
   `agents/memory/1index.md` and any relevant memory entry, then the code you are about to
   change. Investigate unfamiliar files before overwriting.
2. **Stay in scope.** Change only the files the task names. Never silently rename public
   types, methods, routes, DTO properties, SQL columns or response fields.
3. **Follow the data-access rules.** Async Dapper only (`QueryAsync`, `ExecuteAsync`,
   `QueryMultipleAsync`) with `CancellationToken ct = default` and
   `CommandDefinition(..., cancellationToken: ct)`; SQL in repository private
   `const string` raw string literals; `await using` a connection per method. No
   `.Result`, `.Wait()`, sync DB calls, N+1 queries or unparameterized SQL. Call
   `ct.ThrowIfCancellationRequested()` right before a Dapper multi-map.
4. **Follow the visual contract.** Server-rendered Razor Views and Tag Helpers styled from
   `design-system/`. No SPA, no client-side router, no unrelated CSS framework.
5. **Keep SQL idempotent.** New or edited `db/` scripts use `GO` batches and
   `IF OBJECT_ID(...) IS NULL` / `IF NOT EXISTS` guards. There are no migrations; schema
   changes ship as numbered scripts.
6. **Verify, don't assume.** Exercise the real environment where possible (for SQL work,
   re-run `docker compose run --rm db-init` twice and query the result). Report what was
   checked and any test data you cleaned up.
7. **Update the docs after the work is implemented and verified.** Once the task's changes
   are in place and you have confirmed they work (build, `db-init`, HTTP probe, whatever
   the change needs), check whether `docs/*.md`, `README.md` or `db/README.md` now say
   something wrong or incomplete and fix it. If nothing user-facing changed, leave them
   alone — do not update docs for a change that was not verified. Long-form detail belongs
   in `docs/` (linked from `docs/index.md`), not in the README.
8. **Finish with memory and status.** After a task, add an entry to `agents/memory/` and a
   row to `agents/memory/1index.md`. Then update the **Project Status** table in
   `README.md`: the finished stage must read `done`, and the stage log in
   `docs/development-stages.md` gets the detail. This is part of "done", not optional
   polish.

## Memory entry format

One topic per file, kebab-case name (`db-schema-t02.md`, `sql-server-local-dev.md`).
Keep it short and factual; concrete names, paths and commands beat prose.

```markdown
# Topic

Last verified: YYYY-MM-DD (environment / version).

## What exists / what was decided
## How to verify (commands that worked)
## Gotchas
```

If an entry becomes wrong, fix or delete it rather than appending a contradiction.
