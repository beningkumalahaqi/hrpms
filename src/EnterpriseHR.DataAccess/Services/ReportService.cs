using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;

namespace EnterpriseHR.DataAccess.Services;

/// <summary>
/// Orchestrates the reports: a single read with nothing to normalise, so it delegates
/// unchanged. It exists so the hosts depend on the service contract rather than on the
/// repository, matching the employee and project features.
/// </summary>
public sealed class ReportService(IReportRepository reports) : IReportService
{
    public Task<IReadOnlyList<DepartmentSummaryDto>> GetDepartmentSummaryAsync(
        CancellationToken ct = default)
    {
        return reports.GetDepartmentSummaryAsync(ct);
    }
}
