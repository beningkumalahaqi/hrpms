using EnterpriseHR.Domain.Dtos;

namespace EnterpriseHR.Domain.Interfaces;

public interface IReportRepository
{
    Task<IReadOnlyList<DepartmentSummaryDto>> GetDepartmentSummaryAsync(CancellationToken ct = default);
}
