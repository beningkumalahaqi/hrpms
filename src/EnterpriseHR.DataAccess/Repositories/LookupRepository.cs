using Dapper;
using EnterpriseHR.DataAccess.Infrastructure;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;

namespace EnterpriseHR.DataAccess.Repositories;

/// <summary>
/// Read-only option lists for the select controls in the dashboard and the API's lookup
/// endpoints. Every query projects exactly two columns, aliased <c>Value</c> and <c>Text</c>,
/// which is what Dapper binds to the positional <see cref="SelectOptionDto"/> record.
/// </summary>
public sealed class LookupRepository(IDbConnectionFactory factory)
    : RepositoryBase(factory), ILookupRepository
{
    private const string SelectDepartmentOptionsSql = """
        SELECT Id AS Value,
               DepartmentName AS Text
        FROM dbo.Departments
        ORDER BY DepartmentName;
        """;

    private const string SelectPositionOptionsSql = """
        SELECT Id AS Value,
               Title AS Text
        FROM dbo.Positions
        ORDER BY Title;
        """;

    private const string SelectEmployeeOptionsSql = """
        SELECT Id AS Value,
               FullName + N' (' + EmployeeNumber + N')' AS Text
        FROM dbo.Employees
        WHERE (@ActiveOnly = 0 OR IsActive = 1)
          AND (@Exclude IS NULL OR Id <> @Exclude)
        ORDER BY FullName;
        """;

    public async Task<IReadOnlyList<SelectOptionDto>> GetDepartmentOptionsAsync(
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        var options = await conn.QueryAsync<SelectOptionDto>(
            new CommandDefinition(SelectDepartmentOptionsSql, cancellationToken: ct));

        return options.AsList();
    }

    public async Task<IReadOnlyList<SelectOptionDto>> GetPositionOptionsAsync(
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        var options = await conn.QueryAsync<SelectOptionDto>(
            new CommandDefinition(SelectPositionOptionsSql, cancellationToken: ct));

        return options.AsList();
    }

    public async Task<IReadOnlyList<SelectOptionDto>> GetEmployeeOptionsAsync(
        bool activeOnly,
        int? excludeEmployeeId,
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        var options = await conn.QueryAsync<SelectOptionDto>(
            new CommandDefinition(
                SelectEmployeeOptionsSql,
                new { activeOnly, exclude = excludeEmployeeId },
                cancellationToken: ct));

        return options.AsList();
    }
}
