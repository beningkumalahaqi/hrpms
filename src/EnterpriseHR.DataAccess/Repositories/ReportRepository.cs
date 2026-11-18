using System.Data;
using Dapper;
using EnterpriseHR.DataAccess.Infrastructure;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;

namespace EnterpriseHR.DataAccess.Repositories;

/// <summary>
/// Reporting reads. The department summary is a stored procedure because the aggregation
/// (active headcount and payroll per department, plus active project counts from a separate
/// table) is cheaper to keep in one server-side plan than to compose from several queries.
/// </summary>
public sealed class ReportRepository(IDbConnectionFactory factory)
    : RepositoryBase(factory), IReportRepository
{
    // The procedure aggregates employees and projects in separate CTEs, so its aliases line
    // up with DepartmentSummaryDto one to one. IsOverBudget is computed by the DTO.
    private const string GetDepartmentSummarySql = "dbo.sp_GetDepartmentSummary";

    public async Task<IReadOnlyList<DepartmentSummaryDto>> GetDepartmentSummaryAsync(
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        // commandType: StoredProcedure, not a text command: the procedure takes no parameters,
        // so passing none is correct and Dapper must not try to parse the name as a batch.
        var summary = await conn.QueryAsync<DepartmentSummaryDto>(
            new CommandDefinition(
                GetDepartmentSummarySql,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

        return summary.AsList();
    }
}
