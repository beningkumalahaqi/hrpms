using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.Domain.Interfaces;

public interface IEmployeeRepository
{
    Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(EmployeeFilter filter, CancellationToken ct = default);

    Task<EmployeeDto?> GetEmployeeByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken ct = default);

    Task UpdateEmployeeAsync(int id, UpdateEmployeeRequest request, CancellationToken ct = default);

    Task DeleteEmployeeAsync(int id, CancellationToken ct = default); // soft delete

    Task UpdateEmployeeSalaryAsync(int id, UpdateSalaryRequest request, CancellationToken ct = default);
}
