using System.Data.Common;
using System.Text;
using Dapper;
using EnterpriseHR.DataAccess.Infrastructure;
using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Entities;
using EnterpriseHR.Domain.Exceptions;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.DataAccess.Repositories;

/// <summary>
/// Employee reads and writes. This file owns the read paths (T13); the write paths land in
/// separate partial files, and <c>IEmployeeRepository</c> is added to the base list once the
/// last of its members exists.
/// </summary>
public sealed partial class EmployeeRepository(IDbConnectionFactory factory)
    : RepositoryBase(factory)
{
    private const int MaxPageSize = 100;

    // Column order is part of the contract: Dapper's splitOn splits the row at each "Id" it
    // is told about, so the four groups below must stay in this exact sequence
    // (employee, department, position, manager).
    private const string SelectEmployeeColumnsSql = """
        SELECT e.Id,
               e.EmployeeNumber,
               e.FullName,
               e.Email,
               e.DepartmentId,
               e.PositionId,
               e.ManagerId,
               e.Salary,
               e.JoinDate,
               e.IsActive,
               d.Id,
               d.DepartmentCode,
               d.DepartmentName,
               d.Budget,
               p.Id,
               p.Title,
               p.MinSalary,
               p.MaxSalary,
               m.Id,
               m.EmployeeNumber,
               m.FullName
        """;

    // The manager join is LEFT: the three managers in the seed have ManagerId = NULL, and
    // the multi-map turns that all-NULL fourth group into a null Employee.
    private const string EmployeeJoinsSql = """

        FROM dbo.Employees e
        INNER JOIN dbo.Departments d ON d.Id = e.DepartmentId
        INNER JOIN dbo.Positions p   ON p.Id = e.PositionId
        LEFT  JOIN dbo.Employees m   ON m.Id = e.ManagerId
        """;

    private const string EmployeeFilterSql = """

        WHERE (@IsActive IS NULL OR e.IsActive = @IsActive)
          AND (@DepartmentId IS NULL OR e.DepartmentId = @DepartmentId)
          AND (@Search IS NULL
               OR e.FullName LIKE @Pattern ESCAPE '\'
               OR e.EmployeeNumber LIKE @Pattern ESCAPE '\')
        """;

    private const string SelectEmployeesSql = $"""
        {SelectEmployeeColumnsSql}{EmployeeJoinsSql}{EmployeeFilterSql}
        ORDER BY e.FullName, e.Id
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        """;

    // Same joins and predicates as the list query, so page rows and TotalCount can never
    // disagree; the joins are PK lookups, so the extra cost is negligible.
    private const string CountEmployeesSql = $"""
        SELECT COUNT(*){EmployeeJoinsSql}{EmployeeFilterSql};
        """;

    private const string SelectEmployeeByIdSql = $"""
        {SelectEmployeeColumnsSql}{EmployeeJoinsSql}

        WHERE e.Id = @Id;
        """;

    private const string SelectPositionSalaryRangeSql = """
        SELECT MinSalary, MaxSalary
        FROM dbo.Positions
        WHERE Id = @Id;
        """;

    private const string SelectDepartmentExistsSql = """
        SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Departments WHERE Id = @Id)
                         THEN 1 ELSE 0 END AS BIT);
        """;

    // A manager must be an existing, still-active employee. One row back means both hold.
    private const string SelectActiveEmployeeExistsSql = """
        SELECT CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM dbo.Employees WHERE Id = @Id AND IsActive = 1)
               THEN 1 ELSE 0 END AS BIT);
        """;

    // The columns are CI-collated, so these comparisons are case-insensitive without lowering
    // either side; @ExcludeId lets the update path ignore the row being edited.
    private const string SelectEmployeeNumberExistsSql = """
        SELECT CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM dbo.Employees
                   WHERE EmployeeNumber = @EmployeeNumber
                     AND (@ExcludeId IS NULL OR Id <> @ExcludeId))
               THEN 1 ELSE 0 END AS BIT);
        """;

    private const string SelectEmailExistsSql = """
        SELECT CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM dbo.Employees
                   WHERE Email = @Email
                     AND (@ExcludeId IS NULL OR Id <> @ExcludeId))
               THEN 1 ELSE 0 END AS BIT);
        """;

    // IsActive is left to DF_Employees_IsActive (= 1); Salary and PositionId are only ever set
    // here, never by UpdateEmployeeAsync (A3).
    private const string InsertEmployeeSql = """
        INSERT INTO dbo.Employees
            (EmployeeNumber, FullName, Email, DepartmentId, PositionId, ManagerId, Salary, JoinDate)
        VALUES
            (@EmployeeNumber, @FullName, @Email, @DepartmentId, @PositionId, @ManagerId, @Salary, @JoinDate);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """;

    // Salary and PositionId are deliberately absent: the edit form cannot change them (A3).
    private const string UpdateEmployeeSql = """
        UPDATE dbo.Employees
        SET FullName     = @FullName,
            Email        = @Email,
            DepartmentId = @DepartmentId,
            ManagerId    = @ManagerId,
            JoinDate     = @JoinDate
        WHERE Id = @Id;
        """;

    // UPDLOCK/ROWLOCK holds the row for the rest of the transaction, so a concurrent
    // deactivation cannot pass the two guards below at the same time as this one.
    private const string SelectEmployeeStateForDeleteSql = """
        SELECT Id, IsActive
        FROM dbo.Employees WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @Id;
        """;

    private const string CountActiveReportsSql = """
        SELECT COUNT(*)
        FROM dbo.Employees
        WHERE ManagerId = @Id AND IsActive = 1;
        """;

    // Only an assignment on an Active project blocks deactivation; Planning, Completed and
    // Cancelled projects do not.
    private const string CountActiveProjectAssignmentsSql = """
        SELECT COUNT(*)
        FROM dbo.ProjectAssignments pa
        INNER JOIN dbo.Projects p ON p.Id = pa.ProjectId
        WHERE pa.EmployeeId = @Id AND p.Status = 'Active';
        """;

    // Soft delete only: employees are never removed from the table (A6).
    private const string DeactivateEmployeeSql = """
        UPDATE dbo.Employees
        SET IsActive = 0
        WHERE Id = @Id;
        """;

    public async Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(
        EmployeeFilter filter,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);

        // long, not int: Page is unbounded and (Page - 1) * PageSize would overflow int
        // long before SQL Server ever saw a negative OFFSET.
        var offset = (long)(page - 1) * pageSize;

        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var parameters = new
        {
            search,
            pattern = BuildLikePattern(search),
            departmentId = filter.DepartmentId,
            isActive = filter.IsActive,
            offset,
            pageSize,
        };

        await using var conn = await OpenAsync(ct);

        // Exactly two round trips, independent of how many rows come back.
        var totalCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(CountEmployeesSql, parameters, cancellationToken: ct));

        ct.ThrowIfCancellationRequested();

        var rows = await conn.QueryAsync<Employee, Department, Position, Employee, EmployeeDto>(
            new CommandDefinition(SelectEmployeesSql, parameters, cancellationToken: ct),
            (employee, department, position, manager) => new EmployeeDto
            {
                Id = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                FullName = employee.FullName,
                Email = employee.Email,
                DepartmentId = department.Id,
                DepartmentName = department.DepartmentName,
                PositionId = position.Id,
                PositionTitle = position.Title,
                ManagerId = manager?.Id,
                ManagerName = manager?.FullName,
                Salary = employee.Salary,
                JoinDate = employee.JoinDate,
                IsActive = employee.IsActive,
            },
            splitOn: "Id,Id,Id");

        return new PagedResult<EmployeeDto>
        {
            Items = rows.AsList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        ct.ThrowIfCancellationRequested();

        var rows = await conn.QueryAsync<Employee, Department, Position, Employee, EmployeeDto>(
            new CommandDefinition(SelectEmployeeByIdSql, new { id }, cancellationToken: ct),
            (employee, department, position, manager) => new EmployeeDto
            {
                Id = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                FullName = employee.FullName,
                Email = employee.Email,
                DepartmentId = department.Id,
                DepartmentName = department.DepartmentName,
                PositionId = position.Id,
                PositionTitle = position.Title,
                ManagerId = manager?.Id,
                ManagerName = manager?.FullName,
                Salary = employee.Salary,
                JoinDate = employee.JoinDate,
                IsActive = employee.IsActive,
            },
            splitOn: "Id,Id,Id");

        return rows.FirstOrDefault();
    }

    /// <summary>
    /// Wraps a trimmed search term in <c>%…%</c> and backslash-escapes the LIKE metacharacters
    /// <c>\</c>, <c>%</c>, <c>_</c> and <c>[</c>, matching the <c>ESCAPE '\'</c> in the query.
    /// Returns <c>null</c> for a blank term so the filter predicate switches itself off.
    /// </summary>
    private static string? BuildLikePattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        var pattern = new StringBuilder(trimmed.Length + 2);
        pattern.Append('%');

        foreach (var ch in trimmed)
        {
            if (ch is '\\' or '%' or '_' or '[')
            {
                pattern.Append('\\');
            }

            pattern.Append(ch);
        }

        pattern.Append('%');
        return pattern.ToString();
    }

    public async Task<int> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var conn = await OpenAsync(ct);

        // A2: the salary has to fit the selected position, and the position has to exist.
        var range = await conn.QuerySingleOrDefaultAsync<PositionSalaryRange>(
            new CommandDefinition(SelectPositionSalaryRangeSql, new { id = request.PositionId },
                cancellationToken: ct));

        if (range is null)
        {
            throw new BusinessRuleException("Position not found.");
        }

        if (request.Salary < range.MinSalary || request.Salary > range.MaxSalary)
        {
            throw new BusinessRuleException(
                $"Salary must be between {range.MinSalary:N0} and {range.MaxSalary:N0} for this position.");
        }

        if (!await DepartmentExistsAsync(conn, request.DepartmentId, ct))
        {
            throw new BusinessRuleException("Department not found.");
        }

        if (request.ManagerId is { } managerId
            && !await ActiveEmployeeExistsAsync(conn, managerId, ct))
        {
            throw new BusinessRuleException("Manager not found or is not active.");
        }

        await EnsureEmployeeNumberAvailableAsync(conn, request.EmployeeNumber, null, ct);
        await EnsureEmailAvailableAsync(conn, request.Email, null, ct);

        ct.ThrowIfCancellationRequested();

        // The uniqueness checks above are advisory: two concurrent creates can both pass them.
        // UQ_Employees_EmployeeNumber / UQ_Employees_Email are the real guard, and the translator
        // turns their 2627 into the same ConflictException the checks raise.
        return await ExecuteSqlAsync(async () =>
            await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(InsertEmployeeSql, new
                {
                    request.EmployeeNumber,
                    request.FullName,
                    request.Email,
                    request.DepartmentId,
                    request.PositionId,
                    request.ManagerId,
                    request.Salary,
                    request.JoinDate,
                }, cancellationToken: ct)));
    }

    public async Task UpdateEmployeeAsync(
        int id,
        UpdateEmployeeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var conn = await OpenAsync(ct);

        if (!await DepartmentExistsAsync(conn, request.DepartmentId, ct))
        {
            throw new BusinessRuleException("Department not found.");
        }

        if (request.ManagerId is { } managerId)
        {
            // Checked here as well as by CK_Employees_Manager_NotSelf: the constraint message is
            // meaningless to a caller, and it only fires once the row is being written.
            if (managerId == id)
            {
                throw new BusinessRuleException("An employee cannot be their own manager.");
            }

            if (!await ActiveEmployeeExistsAsync(conn, managerId, ct))
            {
                throw new BusinessRuleException("Manager not found or is not active.");
            }
        }

        // Excluding self is what lets an update keep the employee's current email address.
        await EnsureEmailAvailableAsync(conn, request.Email, id, ct);

        ct.ThrowIfCancellationRequested();

        var affected = await ExecuteSqlAsync(async () =>
            await conn.ExecuteAsync(
                new CommandDefinition(UpdateEmployeeSql, new
                {
                    id,
                    request.FullName,
                    request.Email,
                    request.DepartmentId,
                    request.ManagerId,
                    request.JoinDate,
                }, cancellationToken: ct)));

        if (affected == 0)
        {
            throw new NotFoundException($"Employee {id} was not found.");
        }
    }

    public async Task DeleteEmployeeAsync(int id, CancellationToken ct = default)
    {
        await ExecuteInTransactionAsync(
            async (conn, tx) =>
            {
                // UPDLOCK holds this row until commit, so the two guards below and the update
                // cannot be interleaved with a concurrent deactivation or re-hire.
                var state = await conn.QuerySingleOrDefaultAsync<EmployeeState>(
                    new CommandDefinition(SelectEmployeeStateForDeleteSql, new { id },
                        transaction: tx, cancellationToken: ct));

                if (state is null)
                {
                    throw new NotFoundException($"Employee {id} was not found.");
                }

                // A6: deactivating an already inactive employee succeeds without side effects.
                if (!state.IsActive)
                {
                    return;
                }

                var activeReports = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(CountActiveReportsSql, new { id },
                        transaction: tx, cancellationToken: ct));

                if (activeReports > 0)
                {
                    throw new ConflictException(
                        $"Cannot deactivate: employee manages {activeReports} active employee(s).");
                }

                var activeProjects = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(CountActiveProjectAssignmentsSql, new { id },
                        transaction: tx, cancellationToken: ct));

                if (activeProjects > 0)
                {
                    throw new ConflictException(
                        $"Cannot deactivate: employee is still assigned to {activeProjects} active project(s).");
                }

                await conn.ExecuteAsync(new CommandDefinition(DeactivateEmployeeSql, new { id },
                    transaction: tx, cancellationToken: ct));
            },
            ct);
    }

    private static async Task<bool> DepartmentExistsAsync(
        DbConnection conn,
        int departmentId,
        CancellationToken ct)
    {
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SelectDepartmentExistsSql, new { id = departmentId },
                cancellationToken: ct));
    }

    private static async Task<bool> ActiveEmployeeExistsAsync(
        DbConnection conn,
        int employeeId,
        CancellationToken ct)
    {
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SelectActiveEmployeeExistsSql, new { id = employeeId },
                cancellationToken: ct));
    }

    private static async Task EnsureEmployeeNumberAvailableAsync(
        DbConnection conn,
        string employeeNumber,
        int? excludeId,
        CancellationToken ct)
    {
        var taken = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SelectEmployeeNumberExistsSql,
                new { employeeNumber, excludeId }, cancellationToken: ct));

        if (taken)
        {
            throw new ConflictException(
                $"Employee number '{employeeNumber}' is already in use.");
        }
    }

    private static async Task EnsureEmailAvailableAsync(
        DbConnection conn,
        string email,
        int? excludeId,
        CancellationToken ct)
    {
        var taken = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(SelectEmailExistsSql,
                new { email, excludeId }, cancellationToken: ct));

        if (taken)
        {
            throw new ConflictException($"Email '{email}' is already in use.");
        }
    }

    /// <summary>Salary bounds of the position a new employee is being hired into.</summary>
    private sealed class PositionSalaryRange
    {
        public decimal MinSalary { get; set; }

        public decimal MaxSalary { get; set; }
    }

    /// <summary>Read under UPDLOCK by the delete path, so the row is held until commit.</summary>
    private sealed class EmployeeState
    {
        public int Id { get; set; }

        public bool IsActive { get; set; }
    }
}

