using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseHR.WebApp.Controllers;

/// <summary>
/// The budget dashboard. It reads the same department summary the API publishes at
/// <c>/api/reports/department-summary</c>, so the screen and the endpoint cannot disagree about
/// headcount, payroll or which department is over its budget.
/// </summary>
public class DashboardController : Controller
{
    private readonly IReportService _reports;

    public DashboardController(IReportService reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// GET /Dashboard: one row per department with active headcount, monthly payroll, budget,
    /// remaining budget and active project count.
    /// </summary>
    /// <remarks>
    /// A read with no parameters and no write path, so there is nothing to validate here. The
    /// aggregation itself is <c>dbo.sp_GetDepartmentSummary</c>, which left joins from
    /// <c>Departments</c>: a department with no active employees still gets a row with zeros
    /// (OPS in the seed) instead of disappearing from the screen.
    ///
    /// The ordering is the procedure's own (by department name, FIN/HR/IT/OPS) and is left
    /// alone: the list is short, and the budget status is already visible through the row
    /// treatment and the badge, so the screen adds no sort control it would have to fake.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        IReadOnlyList<DepartmentSummaryDto> summary = await _reports.GetDepartmentSummaryAsync(ct);

        return View(summary);
    }
}
