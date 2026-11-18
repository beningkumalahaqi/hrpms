using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.Domain.Interfaces;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> GetPagedAsync(EmployeeFilter filter, CancellationToken ct = default);

    Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default);

    Task UpdateAsync(int id, UpdateEmployeeRequest request, CancellationToken ct = default);

    Task DeactivateAsync(int id, CancellationToken ct = default);

    Task ChangeSalaryAsync(int id, UpdateSalaryRequest request, CancellationToken ct = default);

    Task<EmployeeFormOptions> GetFormOptionsAsync(int? excludeEmployeeId, CancellationToken ct = default);

    Task<IReadOnlyList<SelectOptionDto>> GetDepartmentOptionsAsync(CancellationToken ct = default);
}
