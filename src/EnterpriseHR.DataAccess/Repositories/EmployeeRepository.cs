using System.Text;
using Dapper;
using EnterpriseHR.DataAccess.Infrastructure;
using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Entities;
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
}
