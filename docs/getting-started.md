# Getting Started

Everything needed to run EnterpriseHRSystem locally: SQL Server in Docker, the database
scripts, configuration, and the application.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet --version` should report `8.x`)
- Docker Desktop, or Docker Engine with the Compose plugin (`docker compose version`)
- Git
- A SQL client for inspecting the database (optional): `sqlcmd`, Azure Data Studio, or DBeaver

On Apple Silicon, read [Apple Silicon](#apple-silicon) before starting the containers; the
SQL Server image is published for `linux/amd64` only.

## 1. Clone and configure

```bash
git clone <repository-url> EnterpriseHRSystem
cd EnterpriseHRSystem
cp .env.example .env
```

Open `.env` and set `MSSQL_SA_PASSWORD` to a password that satisfies SQL Server
complexity rules: at least 8 characters, using three of uppercase, lowercase, digits,
and symbols. `.env` is ignored by Git; never commit it and never put a real password in
a tracked file.

## 2. Start SQL Server

```bash
docker compose up -d
```

The first run pulls the `mcr.microsoft.com/mssql/server:2022-latest` image and takes a
few minutes. Wait until Docker reports the container as healthy:

```bash
docker compose ps
```

SQL Server listens on `localhost:1433` with the `sa` login and the password from `.env`.
Confirm it responds before blaming the application:

```bash
docker exec enterprisehr-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<your-password>' -C -Q "SELECT @@VERSION"
```

`-C` trusts the self-signed development certificate.

## 3. Initialize the database

The database itself is created by the SQL scripts in `db/`, not by the container image.
The `db-init` service in `docker-compose.yml` waits for `sqlserver` to become healthy,
then runs every file matching `db/0*.sql` in filename order using `sqlcmd -b`, so the
first error stops the run. Scripts are mounted read-only at `/scripts`.

```bash
# Run (or re-run) initialization manually
docker compose run --rm db-init

# Follow the init log
docker compose logs -f db-init
```

The scripts are idempotent, which is why re-running the command above is safe after
editing one. Confirm the result:

```bash
docker exec enterprisehr-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<your-password>' -C -d EnterpriseHRDB \
  -Q "SELECT name FROM sys.tables ORDER BY name"
```

A completed run lists six tables: `Departments`, `Employees`, `Positions`,
`ProjectAssignments`, `Projects`, `SalaryHistories`.

See [database.md](database.md) for what each script creates and `db/README.md` for the
rules new scripts must follow.

## 4. Connection string

The application reads one connection string under the key `ConnectionStrings:EnterpriseHR`:

```json
{
  "ConnectionStrings": {
    "EnterpriseHR": "Server=localhost,1433;Database=EnterpriseHRDB;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=True"
  }
}
```

`src/EnterpriseHR.WebApp/appsettings.Development.json` ships this string built from the
`.env.example` placeholder password. If your `.env` uses a different `MSSQL_SA_PASSWORD`,
update that file to match. Only commit a placeholder password in `appsettings` files; those
files are tracked, `.env` is not.

`AddEnterpriseHrDataAccess` in `EnterpriseHR.DataAccess` reads the key once and throws
`InvalidOperationException` at startup when it is missing. Because the key only exists in
`appsettings.Development.json`, a non-Development environment must supply it, for example
with the standard ASP.NET Core configuration override:

```bash
export ConnectionStrings__EnterpriseHR="Server=localhost,1433;Database=EnterpriseHRDB;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=True"
```

The double underscore maps to the `:` hierarchy. Environment variables win over
`appsettings*.json`, which is the intended way to configure the application in a container or
pipeline.

## 5. Run the application

There is one runnable project, `src/EnterpriseHR.WebApp`. It serves the MVC dashboard, the
REST API under `/api/**` and Swagger, all on one origin. The development URL comes from
`src/EnterpriseHR.WebApp/Properties/launchSettings.json`.

| What | URL |
| ---- | --- |
| MVC dashboard | <http://localhost:5100> |
| REST API | <http://localhost:5100/api/**> |
| Swagger UI | <http://localhost:5100/swagger> |

```bash
dotnet run --project src/EnterpriseHR.WebApp
```

`EnterpriseHR.Api` is a class library, not a second application: it holds the API
controllers, which this host discovers as an MVC application part. There is nothing to run
on port 5200.

SQL Server must be running and `EnterpriseHRDB` initialized before the application serves
requests that touch the database.

## Reset and troubleshooting

**Start over with a clean database.** This deletes the `sqldata` volume and with it all
local database data; containers and images are recreated on the next `up`.

```bash
docker compose down -v
docker compose up -d
docker compose run --rm db-init
```

**Container exits immediately or restart-loops.** Check the log first; the most common
causes are a password that fails complexity rules and a host port already in use.

```bash
docker compose logs sqlserver
lsof -nP -iTCP:1433 -sTCP:LISTEN
```

**`db-init` fails partway through.** It runs with `-b`, so the first SQL error aborts the
script. Fix the offending statement and re-run `docker compose run --rm db-init`; scripts
are idempotent, so already-applied parts are skipped.

**`The certificate chain was issued by an authority that is not trusted`.** The
development container uses a self-signed certificate. Keep `TrustServerCertificate=True`
in the local connection string.

**Password changes.** `MSSQL_SA_PASSWORD` is only applied when the data volume is
initialized. After changing it in `.env`, recreate the volume with
`docker compose down -v` and update `src/EnterpriseHR.WebApp/appsettings.Development.json`.

### Apple Silicon

The SQL Server image is built for `linux/amd64` only. If the container fails to start,
add the platform to **both** services in `docker-compose.yml`:

```yaml
services:
  sqlserver:
    platform: linux/amd64
    # ...
  db-init:
    platform: linux/amd64
    # ...
```

That alone is not always enough. SQL Server cannot run under QEMU user-mode emulation
(the `binfmt`/`qemu-x86_64` path) and exits with:

```
sqlservr: Invalid mapping of address 0x... in reserved address space below 0x400000000000
```

Use Rosetta emulation instead: in Docker Desktop enable *Settings → General → Use Rosetta
for x86_64/amd64 emulation*, or start Colima with `colima start --vz-rosetta`. Where
Rosetta is unavailable, run the containers inside an amd64 VM, for example
`colima start --profile mssql --arch x86_64`, and point `DOCKER_HOST` at that profile's
socket before running `docker compose`:

```bash
export DOCKER_HOST=unix://$HOME/.colima/mssql/docker.sock
```

The stack itself needs no changes; SQL Server is slower under emulation either way.
