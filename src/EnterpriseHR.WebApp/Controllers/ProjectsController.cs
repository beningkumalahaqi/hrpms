using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Exceptions;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;
using EnterpriseHR.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Controllers;

/// <summary>
/// The project screens. Both the list and the detail read through the same
/// <see cref="IProjectService"/> that answers <c>/api/projects/{id}/details</c>, and the
/// assignment writes through the same <see cref="IProjectService.AssignAsync"/> that answers
/// <c>/api/projects/{id}/assign</c>, so the screens and the API cannot drift apart.
/// </summary>
public class ProjectsController : Controller
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    /// <summary>GET /Projects: the whole directory with its active team size.</summary>
    /// <remarks>
    /// The list is small and unpaged by design: the repository returns every project ordered by
    /// start date, and the screen adds no filters it cannot honour. Nothing here writes.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var projects = await _projects.GetAllAsync(ct);

        return View(projects);
    }

    /// <summary>
    /// GET /Projects/Details/{id}: one project, every member assigned to it, and the
    /// add-member form.
    /// </summary>
    /// <remarks>
    /// The member list is not filtered: a completed project still shows who worked on it, and a
    /// member who has since been deactivated stays on the list with its marker. The employee
    /// options are only used by the form, but they are loaded on every render so a post that
    /// fails validation can rebuild the screen without a second round trip inside the view.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var project = await _projects.GetDetailsAsync(id, ct);

        if (project is null)
        {
            return NotFound();
        }

        var employees = await _projects.GetAssignableEmployeeOptionsAsync(ct);

        return View(BuildModel(project, employees, new AssignEmployeeRequest()));
    }

    /// <summary>POST /Projects/Assign/{id}: adds one member to a project.</summary>
    /// <remarks>
    /// The form is only rendered for <c>Active</c> and <c>Planning</c> projects, but the rules
    /// are not re-implemented here: a hand-crafted post to a finished project still reaches the
    /// repository, which is the only place that decides what a status permits.
    ///
    /// The route is declared explicitly and named because <c>EnterpriseHR.Api</c> also has a
    /// <c>Projects.Assign</c> action, at <c>api/Projects/{id}/assign</c>. With two candidates for
    /// the same controller and action names, link generation resolved <c>asp-action="Assign"</c>
    /// to the API route, so the form posted to the JSON endpoint. Naming this one removes the
    /// ambiguity instead of depending on which candidate wins.
    ///
    /// A rejected submission re-renders the same screen with the submitted values intact and the
    /// project and employee lists rebuilt from the database, so nothing displayed is taken from
    /// the body. The two rule failures are reported in the place the user can act on them:
    /// <see cref="ConflictException"/> is the workload cap - the message names the current,
    /// requested and resulting percentages - and it is a warning because the request was
    /// understood and could succeed at a smaller percentage. <see cref="BusinessRuleException"/>
    /// covers an inactive employee or a closed project, which the form cannot fix, so it is an
    /// error.
    /// </remarks>
    [HttpPost("Projects/Assign/{id:int}", Name = "ProjectAssign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(
        int id,
        ProjectDetailsViewModel model,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var project = await _projects.GetDetailsAsync(id, ct);

        if (project is null)
        {
            return NotFound();
        }

        var employees = await _projects.GetAssignableEmployeeOptionsAsync(ct);

        if (!ModelState.IsValid)
        {
            UseFriendlyAssignmentMessages(ModelState);

            return View(nameof(Details), BuildModel(project, employees, model.NewAssignment));
        }

        try
        {
            await _projects.AssignAsync(id, model.NewAssignment, ct);
        }
        catch (ConflictException ex)
        {
            TempData["Warning"] = ex.Message;

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (NotFoundException)
        {
            // The employee was removed between the read above and the write, or the project was
            // closed under the same id. Either way there is nothing left to show for this route.
            return NotFound();
        }

        // The option text is "Full name (EmployeeNumber)", already resolved above, so the
        // confirmation names the person without a second query.
        var employeeLabel = employees
            .FirstOrDefault(option => option.Value == model.NewAssignment.EmployeeId)
            ?.Text
            ?? $"Employee {model.NewAssignment.EmployeeId}";

        TempData["Success"] =
            $"{employeeLabel} was added to {project.ProjectCode} as "
            + $"{model.NewAssignment.RoleInProject.Trim()} at "
            + $"{model.NewAssignment.AllocationPercentage}%.";

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Composes the screen: the stored project, the employee select items, and the submitted
    /// (or empty) assignment.
    /// </summary>
    private static ProjectDetailsViewModel BuildModel(
        ProjectDetailDto project,
        IReadOnlyList<SelectOptionDto> employees,
        AssignEmployeeRequest assignment)
    {
        return new ProjectDetailsViewModel
        {
            Project = project,
            Employees = ToSelectList(employees, assignment.EmployeeId),
            NewAssignment = assignment,
        };
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
    /// Replaces the three attribute messages with the wording the other forms use.
    /// </summary>
    /// <remarks>
    /// <see cref="AssignEmployeeRequest"/> is a shared contract, so its messages are the
    /// framework defaults ("The field EmployeeId must be between 1 and 2147483647.") and editing
    /// them would change what the API returns. The screen owns its own copy of the sentence and
    /// keeps the field keys, so the messages still land on the right controls.
    /// </remarks>
    private static void UseFriendlyAssignmentMessages(ModelStateDictionary modelState)
    {
        (string Field, string Message)[] replacements =
        [
            ($"{nameof(ProjectDetailsViewModel.NewAssignment)}.{nameof(AssignEmployeeRequest.EmployeeId)}",
                "Select an employee."),
            ($"{nameof(ProjectDetailsViewModel.NewAssignment)}.{nameof(AssignEmployeeRequest.RoleInProject)}",
                "Enter the role this person fills on the project."),
            ($"{nameof(ProjectDetailsViewModel.NewAssignment)}.{nameof(AssignEmployeeRequest.AllocationPercentage)}",
                "Enter an allocation between 1% and 100%."),
        ];

        foreach (var (field, message) in replacements)
        {
            if (!modelState.TryGetValue(field, out var entry) || entry.Errors.Count == 0)
            {
                continue;
            }

            entry.Errors.Clear();
            modelState.AddModelError(field, message);
        }
    }
}
