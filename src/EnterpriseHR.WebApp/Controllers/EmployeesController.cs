using System.Globalization;
using EnterpriseHR.Domain.Common;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Exceptions;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;
using EnterpriseHR.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Controllers;

/// <summary>
/// The employee directory screens. They list, search, filter and page with the same
/// <see cref="IEmployeeService"/> the API uses, so the screens and <c>/api/employees</c>
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

    /// <summary>GET /Employees/Create: an empty form with the three selects loaded.</summary>
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        var options = await _employees.GetFormOptionsAsync(excludeEmployeeId: null, ct);

        return View(new EmployeeCreateViewModel
        {
            // A new record starts today, which is what most of these forms are used for.
            JoinDate = DateTime.Today,
            Departments = ToSelectList(options.Departments, null),
            Positions = ToSelectList(options.Positions, null),
            Managers = ToSelectList(options.Managers, null),
        });
    }

    /// <summary>POST /Employees/Create</summary>
    /// <remarks>
    /// Success redirects to the directory (POST/Redirect/GET), so a reload cannot post the record
    /// twice. A rejected write re-renders the form with the submitted values intact and the
    /// selects rebuilt from the database.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeCreateViewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (ModelState.IsValid)
        {
            // A manager cannot be the new record: it has no id yet, so there is nobody to
            // exclude and no ordering problem.
            var request = new CreateEmployeeRequest
            {
                EmployeeNumber = model.EmployeeNumber,
                FullName = model.FullName,
                Email = model.Email,
                DepartmentId = model.DepartmentId!.Value,
                PositionId = model.PositionId!.Value,
                ManagerId = model.ManagerId,
                Salary = model.Salary!.Value,
                JoinDate = model.JoinDate!.Value,
            };

            // The repository owns the rules a form cannot check: a duplicate number or email,
            // a salary outside the position's range, an unknown department, an inactive manager.
            // Both exception types describe something the person at the form can correct, so the
            // message becomes a form-level error rather than an error page.
            try
            {
                await _employees.CreateAsync(request, ct);
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (ConflictException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }

            if (ModelState.IsValid)
            {
                TempData["Success"] =
                    $"Employee {request.EmployeeNumber} - {request.FullName} was created.";

                return RedirectToAction(nameof(Index));
            }
        }

        await PopulateOptionsAsync(model, excludeEmployeeId: null, ct);

        return View(model);
    }

    /// <summary>GET /Employees/Edit/{id}</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        var employee = await _employees.GetByIdAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        var options = await _employees.GetFormOptionsAsync(id, ct);

        return View(new EmployeeEditViewModel
        {
            Id = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            PositionTitle = employee.PositionTitle,
            Salary = employee.Salary,
            IsActive = employee.IsActive,
            FullName = employee.FullName,
            Email = employee.Email,
            DepartmentId = employee.DepartmentId,
            ManagerId = employee.ManagerId,
            JoinDate = employee.JoinDate,
            Departments = ToSelectList(options.Departments, employee.DepartmentId),
            Managers = ToSelectList(options.Managers, employee.ManagerId),
        });
    }

    /// <summary>POST /Employees/Edit/{id}</summary>
    /// <remarks>
    /// Only the fields <c>UpdateEmployeeSql</c> writes are posted: the number, position and salary
    /// are read-only on this screen (A3). An unknown id answers 404 both on GET and on POST, so a
    /// deleted record cannot be resurrected by replaying a form.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EmployeeEditViewModel model,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var employee = await _employees.GetByIdAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var request = new UpdateEmployeeRequest
            {
                FullName = model.FullName,
                Email = model.Email,
                DepartmentId = model.DepartmentId!.Value,
                ManagerId = model.ManagerId,
                JoinDate = model.JoinDate!.Value,
            };

            try
            {
                await _employees.UpdateAsync(id, request, ct);
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (ConflictException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }

            if (ModelState.IsValid)
            {
                TempData["Success"] = $"Employee {employee.EmployeeNumber} - {request.FullName} was updated.";

                return RedirectToAction(nameof(Index));
            }
        }

        // Never take the read-only values from the body: re-read them from the stored row so the
        // summary the page shows cannot be rewritten by a crafted POST.
        var options = await _employees.GetFormOptionsAsync(id, ct);

        model.EmployeeNumber = employee.EmployeeNumber;
        model.PositionTitle = employee.PositionTitle;
        model.Salary = employee.Salary;
        model.IsActive = employee.IsActive;
        // The submitted selection is kept here, so the person sees what they picked next to the
        // message that explains why it was rejected.
        model.Departments = ToSelectList(options.Departments, model.DepartmentId);
        model.Managers = ToSelectList(options.Managers, model.ManagerId);

        return View(model);
    }

    /// <summary>GET /Employees/Delete/{id}: the confirmation card for a soft delete.</summary>
    /// <remarks>
    /// Deactivation is a state change, so it is confirmed on its own page and posted, never
    /// performed by following a link. An already inactive record is sent back to the directory:
    /// the repository would accept the call as a no-op and report success, which would imply
    /// something changed.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var employee = await _employees.GetByIdAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        if (!employee.IsActive)
        {
            TempData["Warning"] =
                $"Employee {employee.EmployeeNumber} - {employee.FullName} is already inactive.";

            return RedirectToAction(nameof(Index));
        }

        return View(employee);
    }

    /// <summary>POST /Employees/Delete/{id}</summary>
    /// <remarks>
    /// The repository refuses the soft delete while the employee still manages active staff or
    /// holds an Active project assignment. Both are <see cref="ConflictException"/>, and the
    /// message names the blocker and the number of records involved, so it is shown in full on
    /// the directory rather than replaced with a generic sentence.
    /// </remarks>
    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
    {
        var employee = await _employees.GetByIdAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        if (!employee.IsActive)
        {
            TempData["Warning"] =
                $"Employee {employee.EmployeeNumber} - {employee.FullName} is already inactive.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _employees.DeactivateAsync(id, ct);
        }
        catch (NotFoundException)
        {
            // Deleted between the read above and this call.
            return NotFound();
        }
        catch (ConflictException ex)
        {
            TempData["Error"] = ex.Message;

            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Employee {employee.EmployeeNumber} - {employee.FullName} was deactivated.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// GET /Employees/AdjustSalary/{id}: the record's current salary and position plus the
    /// fields that change them.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AdjustSalary(int id, CancellationToken ct = default)
    {
        var employee = await _employees.GetByIdAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        var options = await _employees.GetFormOptionsAsync(id, ct);

        var model = new AdjustSalaryViewModel
        {
            // The select starts on the stored position, so leaving it alone means "same
            // position" and only a different value turns the request into a promotion.
            NewPositionId = employee.PositionId,
        };

        PopulateAdjustSalary(model, employee, options);

        return View(model);
    }

    /// <summary>POST /Employees/AdjustSalary/{id}</summary>
    /// <remarks>
    /// Only a changed selection is sent as <c>NewPositionId</c>: the repository treats null as
    /// "keep the current position", and sending the unchanged id would make an ordinary raise
    /// indistinguishable from a move. An unknown id answers 404 on GET and on POST, so a replay
    /// cannot write against a record that is gone.
    /// </remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustSalary(
        int id,
        AdjustSalaryViewModel model,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var employee = await _employees.GetByIdAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        var options = await _employees.GetFormOptionsAsync(id, ct);

        if (ModelState.IsValid)
        {
            var request = new UpdateSalaryRequest
            {
                NewSalary = model.NewSalary!.Value,
                NewPositionId = model.NewPositionId != employee.PositionId
                    ? model.NewPositionId
                    : null,
                Reason = model.Reason,
            };

            // Range, inactive employee, unknown position, no-op and self-manager rules live in
            // the repository. They all describe something the person at the form can correct, so
            // the message becomes a form-level error instead of an error page.
            try
            {
                await _employees.ChangeSalaryAsync(id, request, ct);
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var title = request.NewPositionId is null
                    ? employee.PositionTitle
                    : options.Positions
                        .FirstOrDefault(option => option.Value == request.NewPositionId)?.Text
                        ?? employee.PositionTitle;

                TempData["Success"] =
                    $"Employee {employee.EmployeeNumber} - {employee.FullName} is now {title} "
                    + $"at {request.NewSalary.ToString("N0", CultureInfo.CurrentCulture)} per month.";

                return RedirectToAction(nameof(Index));
            }
        }

        // The read-only summary and the option list are rebuilt from the current row and the
        // database, never from the body. The submitted salary, position and reason are kept so
        // the rejected values stay visible next to the message.
        PopulateAdjustSalary(model, employee, options);

        return View(model);
    }

    /// <summary>
    /// Fills the read-only half of the salary page from the stored row and rebuilds the position
    /// select, keeping <see cref="AdjustSalaryViewModel.NewPositionId"/> as submitted.
    /// </summary>
    private static void PopulateAdjustSalary(
        AdjustSalaryViewModel model,
        EmployeeDto employee,
        EmployeeFormOptions options)
    {
        model.Id = employee.Id;
        model.EmployeeNumber = employee.EmployeeNumber;
        model.FullName = employee.FullName;
        model.DepartmentName = employee.DepartmentName;
        model.PositionId = employee.PositionId;
        model.PositionTitle = employee.PositionTitle;
        model.CurrentSalary = employee.Salary;
        model.IsActive = employee.IsActive;
        model.Positions = ToSelectList(options.Positions, model.NewPositionId);
    }

    /// <summary>
    /// Rebuilds the create form's selects from the database, keeping the submitted selection.
    /// Called on every render of an invalid POST: the option lists are display data, so they are
    /// never trusted from the body.
    /// </summary>
    private async Task PopulateOptionsAsync(
        EmployeeCreateViewModel model,
        int? excludeEmployeeId,
        CancellationToken ct)
    {
        var options = await _employees.GetFormOptionsAsync(excludeEmployeeId, ct);

        model.Departments = ToSelectList(options.Departments, model.DepartmentId);
        model.Positions = ToSelectList(options.Positions, model.PositionId);
        model.Managers = ToSelectList(options.Managers, model.ManagerId);
    }

    /// <summary>The lookup rows as select items for the given selection.</summary>
    private static SelectList ToSelectList(
        IReadOnlyList<SelectOptionDto> options,
        int? selectedValue)
    {
        return new SelectList(
            options,
            nameof(SelectOptionDto.Value),
            nameof(SelectOptionDto.Text),
            selectedValue);
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
