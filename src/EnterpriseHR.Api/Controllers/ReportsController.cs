using EnterpriseHR.Api.Models;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseHR.Api.Controllers;

/// <summary>
/// Reporting endpoints. The department summary is the aggregate the dashboard needs for its
/// budget cards: one row per department with active headcount, monthly payroll, what is left of
/// the budget and how many projects are running.
/// </summary>
/// <remarks>
/// <see cref="RouteAttribute"/> is set explicitly rather than inherited from
/// <see cref="ApiControllerBase"/>: the base template is <c>api/[controller]</c>, which yields
/// <c>/api/Reports</c>. Routing matches that case-insensitively, but the report contract names
/// the path in lowercase and Swagger publishes whatever the template says, so the literal keeps
/// the document matching the contract instead of relying on case-insensitive matching.
/// </remarks>
[Route("api/reports")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// Gets one summary row per department, ordered by department name.
    /// </summary>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>
    /// <see cref="DepartmentSummaryDto"/> records in <c>data</c>. There is no envelope around
    /// the list beyond the usual one: <c>data</c> <em>is</em> the array. A department with no
    /// active employees reports <c>activeEmployeeCount: 0</c> and
    /// <c>totalMonthlySalary: 0</c> rather than being omitted, because the rows come from
    /// <c>Departments</c> with a left join.
    /// </returns>
    /// <remarks>
    /// Runs <c>dbo.sp_GetDepartmentSummary</c> through async Dapper, so the aggregation happens
    /// in one server-side round trip instead of being recomputed from the API. The procedure
    /// aggregates employees and projects in separate CTEs; joining both detail tables directly
    /// would multiply rows and inflate the counts and payroll sums.
    /// <para>
    /// <c>remainingBudget</c> is <c>budget - totalMonthlySalary</c> and goes negative when
    /// payroll exceeds the budget (IT is <c>-7,000,000</c> in the seed). <c>isOverBudget</c> is a
    /// computed property on the DTO that reports the same fact as a boolean.
    /// </para>
    /// </remarks>
    /// <response code="200">One row per department.</response>
    /// <response code="500">An unexpected failure; the message is generic and carries no internals.</response>
    [HttpGet("department-summary")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DepartmentSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DepartmentSummaryDto>>>> GetDepartmentSummary(
        CancellationToken ct)
    {
        var summary = await _reports.GetDepartmentSummaryAsync(ct);

        return OkResponse(summary);
    }
}
