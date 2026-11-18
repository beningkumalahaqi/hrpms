using EnterpriseHR.Domain.Dtos;

namespace EnterpriseHR.Domain.Interfaces;

public interface ILookupRepository
{
    Task<IReadOnlyList<SelectOptionDto>> GetDepartmentOptionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SelectOptionDto>> GetPositionOptionsAsync(CancellationToken ct = default);

    // Text = "FullName (EmployeeNumber)"
    Task<IReadOnlyList<SelectOptionDto>> GetEmployeeOptionsAsync(
        bool activeOnly,
        int? excludeEmployeeId,
        CancellationToken ct = default);
}
