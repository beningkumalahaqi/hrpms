using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.DataAccess.Services;

/// <summary>
/// Orchestrates the employee features: it normalises the input a form or controller can send
/// in raw form (untrimmed text, page numbers out of range, a null request body) and hands the
/// work to <see cref="IEmployeeRepository"/>. No SQL and no business rule lives here: the
/// repository owns both, so the API and the MVC host cannot drift apart.
/// </summary>
public sealed class EmployeeService(
    IEmployeeRepository employees,
    ILookupRepository lookups) : IEmployeeService
{
    // Mirrors the clamp inside EmployeeRepository.GetAllEmployeesAsync, which stays the
    // authority: the value is repeated here so the PagedResult the caller gets back reports
    // the page size that was actually used, not the one that was requested.
    private const int MaxPageSize = 100;

    public Task<PagedResult<EmployeeDto>> GetPagedAsync(
        EmployeeFilter filter,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return employees.GetAllEmployeesAsync(Normalize(filter), ct);
    }

    public Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return employees.GetEmployeeByIdAsync(id, ct);
    }

    public Task<int> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The request is mutated in place rather than copied: the caller (a controller owning
        // the bound model) owns this instance, and the trimmed values are what gets validated
        // and echoed back on a validation failure.
        request.EmployeeNumber = request.EmployeeNumber?.Trim() ?? string.Empty;
        request.FullName = request.FullName?.Trim() ?? string.Empty;
        request.Email = request.Email?.Trim() ?? string.Empty;

        return employees.CreateEmployeeAsync(request, ct);
    }

    public Task UpdateAsync(int id, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.FullName = request.FullName?.Trim() ?? string.Empty;
        request.Email = request.Email?.Trim() ?? string.Empty;

        return employees.UpdateEmployeeAsync(id, request, ct);
    }

    public Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        return employees.DeleteEmployeeAsync(id, ct);
    }

    public Task ChangeSalaryAsync(
        int id,
        UpdateSalaryRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Reason = request.Reason?.Trim() ?? string.Empty;

        return employees.UpdateEmployeeSalaryAsync(id, request, ct);
    }

    /// <summary>
    /// Everything the create/edit form needs. Sequential on purpose: each repository method
    /// opens and disposes its own connection, so there is nothing to share, and three awaits
    /// in a row keep the failure order predictable and the code boring.
    /// </summary>
    public async Task<EmployeeFormOptions> GetFormOptionsAsync(
        int? excludeEmployeeId,
        CancellationToken ct = default)
    {
        var departments = await lookups.GetDepartmentOptionsAsync(ct);
        var positions = await lookups.GetPositionOptionsAsync(ct);

        // activeOnly: an inactive employee cannot be picked as a manager, and the employee
        // being edited is excluded so nobody can be made their own manager from the dropdown.
        var managers = await lookups.GetEmployeeOptionsAsync(
            activeOnly: true,
            excludeEmployeeId,
            ct);

        return new EmployeeFormOptions
        {
            Departments = departments,
            Positions = positions,
            Managers = managers,
        };
    }

    public Task<IReadOnlyList<SelectOptionDto>> GetDepartmentOptionsAsync(
        CancellationToken ct = default)
    {
        return lookups.GetDepartmentOptionsAsync(ct);
    }

    /// <summary>
    /// Returns a copy with the search term trimmed and the paging inside the limits the
    /// repository enforces, so an out-of-range page never reaches SQL as a negative OFFSET.
    /// </summary>
    private static EmployeeFilter Normalize(EmployeeFilter filter)
    {
        return new EmployeeFilter
        {
            Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim(),
            DepartmentId = filter.DepartmentId,
            IsActive = filter.IsActive,
            Page = Math.Max(filter.Page, 1),
            PageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize),
        };
    }
}
