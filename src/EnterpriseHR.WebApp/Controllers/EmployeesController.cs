using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;
using EnterpriseHR.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Controllers;

/// <summary>
/// The employee directory screen. It lists, searches, filters and pages with the same
/// <see cref="IEmployeeService"/> the API uses, so the screen and <c>GET /api/employees</c>
/// cannot drift apart.
/// </summary>
public class EmployeesController : Controller
{
    // The screen shows a fixed 10-row page. It is pinned here rather than inherited from the
    // API default, so a future change to that default does not silently reshape this view.
    private const int PageSize = 10;

    private readonly IEmployeeService _employees;

    public EmployeesController(IEmployeeService employees)
    {
        _employees = employees;
    }

    /// <summary>
    /// GET /Employees?search=&amp;departmentId=&amp;includeInactive=false&amp;page=1
    /// </summary>
    /// <remarks>
    /// The form is a plain GET, so every filter lives in the query string and a filtered list
    /// can be bookmarked, reloaded or linked to. Nothing here writes.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        int? departmentId = null,
        bool includeInactive = false,
        int page = 1,
        CancellationToken ct = default)
    {
        // IsActive is a server-side decision, exactly as in the API: the checkbox only widens
        // the list to former staff, and no query value can narrow it any further.
        var filter = new EmployeeFilter
        {
            Search = search,
            DepartmentId = departmentId,
            IsActive = includeInactive ? null : true,
            Page = page,
            PageSize = PageSize,
        };

        var result = await _employees.GetPagedAsync(filter, ct);
        var departments = await _employees.GetDepartmentOptionsAsync(ct);

        // Page -5 or page 40 of 3 rows both land outside the real range. The design system's
        // directory clamps the requested page into range, so the URL is corrected with a
        // redirect and the browser is never left showing a pager that disagrees with the rows.
        var lastPage = Math.Max(result.TotalPages, 1);
        var targetPage = Math.Min(Math.Max(page, 1), lastPage);

        if (targetPage != page)
        {
            return RedirectToAction(nameof(Index), BuildRouteValues(
                search: string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                departmentId: departmentId,
                includeInactive: includeInactive,
                page: targetPage));
        }

        return View(new EmployeeIndexViewModel
        {
            Result = result,
            // Echo the trimmed term: the service trims it before searching, and the search box
            // should show what was actually searched for.
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            DepartmentId = departmentId,
            IncludeInactive = includeInactive,
            Departments = new SelectList(
                departments,
                nameof(SelectOptionDto.Value),
                nameof(SelectOptionDto.Text),
                departmentId),
        });
    }

    /// <summary>
    /// Only the values that differ from the defaults, so an unfiltered directory keeps a clean
    /// <c>/Employees</c> URL instead of <c>?search=&amp;departmentId=&amp;includeInactive=False</c>.
    /// </summary>
    private static RouteValueDictionary BuildRouteValues(
        string? search,
        int? departmentId,
        bool includeInactive,
        int page)
    {
        var values = new RouteValueDictionary();

        if (!string.IsNullOrWhiteSpace(search))
        {
            values["search"] = search;
        }

        if (departmentId is int id)
        {
            values["departmentId"] = id;
        }

        if (includeInactive)
        {
            // The string form, not the bool: RouteValueDictionary would write "True", while the
            // form control and the pager links both use the lowercase value a URL should carry.
            values["includeInactive"] = "true";
        }

        if (page > 1)
        {
            values["page"] = page;
        }

        return values;
    }
}
