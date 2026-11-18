using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Models;

/// <summary>
/// Everything <c>Views/Employees/Index.cshtml</c> renders: the page of employees the service
/// returned plus the filter values the request came in with, so the form, the pager links and
/// the empty state all describe the same query.
/// </summary>
public class EmployeeIndexViewModel
{
    public PagedResult<EmployeeDto> Result { get; set; } = new();

    /// <summary>The trimmed search term, or null when the request did not filter on text.</summary>
    public string? Search { get; set; }

    public int? DepartmentId { get; set; }

    /// <summary>True when the list includes former staff; the select list defaults to active only.</summary>
    public bool IncludeInactive { get; set; }

    // Value/Text are the property names on SelectOptionDto, which is what the lookup repository
    // projects. SelectList has to be told them explicitly: with no field names it would use
    // ToString() of the record as both value and text.
    public SelectList Departments { get; set; } = new(
        Array.Empty<SelectOptionDto>(),
        nameof(SelectOptionDto.Value),
        nameof(SelectOptionDto.Text));
}
