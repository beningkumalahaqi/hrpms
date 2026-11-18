# db/

SQL Server initialization scripts for `EnterpriseHRDB`.

Scripts are picked up by the `db-init` service in `docker-compose.yml`, which
runs every file matching `0*.sql` in filename order against the `sqlserver`
container after it reports healthy.

| Prefix | Contents | Status |
| ------ | -------- | ------ |
| `01_*.sql` | Database and schema creation | added in T02 |
| `02_*.sql` | Stored procedures and supporting objects | added in T03 |
| `03_*.sql` | Seed / sample data | added in T04 |

Rules for these scripts:

- **Idempotent.** Each script must be safe to run more than once, because
  `db-init` is re-run manually with `docker compose run --rm db-init`.
- **Self-contained.** No interactive prompts and no SQLCMD variables beyond the
  defaults; `db-init` invokes `sqlcmd` with `-b` so any error aborts the run.
- **Ordered.** Filenames must keep a numeric prefix so the glob order is stable.

No SQL scripts exist yet, so `db-init` prints
`No numbered SQL scripts found yet; skipping database initialization.` and exits 0.
