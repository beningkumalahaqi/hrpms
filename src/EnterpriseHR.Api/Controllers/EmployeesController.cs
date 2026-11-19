using EnterpriseHR.Api.Models;
using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Exceptions;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseHR.Api.Controllers;

/// <summary>
/// Employee endpoints: a paged, searchable list of active employees and a salary adjustment
/// that records its history row in the same transaction.
/// </summary>
/// <remarks>
/// Failures are not handled here. The action lets the domain exceptions escape and
/// <c>ExceptionHandlingMiddleware</c> turns them into the shared envelope, so a business-rule
/// rejection and an unhandled fault cannot drift apart from the rest of the API.
/// </remarks>
public sealed class EmployeesController : ApiControllerBase
{
    private readonly IEmployeeService _employees;

    public EmployeesController(IEmployeeService employees)
    {
        _employees = employees;
    }

    /// <summary>
    /// Lists the active employees, with optional free-text search over name, employee number and
    /// email, an optional department filter, and paging.
    /// </summary>
    /// <param name="filter">
    /// Query string bound filter: <c>search</c>, <c>departmentId</c>, <c>page</c> (default 1) and
    /// <c>pageSize</c> (default 10, max 100). <c>isActive</c> is accepted but ignored: this
    /// endpoint only ever returns active employees.
    /// </param>
    /// <returns>
    /// <see cref="PagedResult{T}"/> of <see cref="EmployeeDto"/>. An out-of-range <c>page</c>
    /// is clamped, and a <c>page</c> past the last page returns an empty <c>items</c> with the
    /// real <c>totalCount</c>.
    /// </returns>
    /// <response code="200">The requested page of active employees.</response>
    /// <response code="400">
    /// A query value failed validation, e.g. <c>pageSize=0</c> or a search longer than 255
    /// characters. <c>message</c> is <c>"Validation failed."</c> and <c>data</c> lists the
    /// offending fields.
    /// </response>
    /// <response code="409">A database constraint was violated; the data layer normally prevents this.</response>
    /// <response code="500">An unexpected failure; the message is generic and carries no internals.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EmployeeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PagedResult<EmployeeDto>>>> GetEmployees(
        [FromQuery] EmployeeFilter filter,
        CancellationToken ct)
    {
        // A fresh filter rather than the bound one: IsActive is a server-side decision, so a
        // client sending ?isActive=false must not be able to widen the list to former staff.
        // The rest is copied through, including the binding-time defaults and validation.
        var effectiveFilter = new EmployeeFilter
        {
            Search = filter.Search,
            DepartmentId = filter.DepartmentId,
            Page = filter.Page,
            PageSize = filter.PageSize,
            IsActive = true,
        };

        var page = await _employees.GetPagedAsync(effectiveFilter, ct);

        return OkResponse(page);
    }

    /// <summary>
    /// Changes an employee's monthly salary and, when supplied, their position.
    /// </summary>
    /// <param name="request">
    /// <c>newSalary</c> (monthly IDR), optional <c>newPositionId</c> (omit to keep the current
    /// position) and a <c>reason</c> recorded on the history row.
    /// </param>
    /// <returns>
    /// The refreshed <see cref="EmployeeDto"/>, read back after the write so the caller sees the
    /// stored salary and position rather than an echo of the request.
    /// </returns>
    /// <remarks>
    /// A rejected adjustment writes nothing: the salary update and the <c>SalaryHistories</c>
    /// insert share one transaction, and the range, active-employee and no-op checks all run
    /// before either statement. A 400 here carries <c>data: null</c>; the field-errors variant of
    /// 400 only comes from model binding.
    /// </remarks>
    /// <response code="200">Salary updated; <c>message</c> is <c>"Salary updated."</c>.</response>
    /// <response code="400">
    /// The body failed validation, or the change breaks a rule: salary outside the target
    /// position's range, the employee is inactive, the position does not exist, or the change
    /// would be a no-op.
    /// </response>
    /// <response code="404">No employee with that id.</response>
    /// <response code="409">A database constraint was violated; the data layer normally prevents this.</response>
    /// <response code="500">An unexpected failure; the message is generic and carries no internals.</response>
    [HttpPatch("{id:int}/salary")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> UpdateSalary(
        int id,
        [FromBody] UpdateSalaryRequest request,
        CancellationToken ct)
    {
        await _employees.ChangeSalaryAsync(id, request, ct);

        // Read back instead of building a DTO from the request: the response is what the database
        // holds, including the department and manager names the request never mentioned.
        var employee = await _employees.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Employee {id} was not found.");

        // Not OkResponse(): this endpoint has its own message, and adding an overload to the base
        // class for one caller would make the shared helper less predictable.
        return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Salary updated."));
    }
}
