using EnterpriseHR.Domain.Dtos;

namespace EnterpriseHR.Domain.Interfaces;

public interface IReportService
{
    Task<IReadOnlyList<DepartmentSummaryDto>> GetDepartmentSummaryAsync(CancellationToken ct = default);
}
