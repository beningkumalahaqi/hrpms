using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Requests;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EnterpriseHR.WebApp.Models;

/// <summary>
/// The project detail screen: one project with every assignment on it, the employees that can
/// still be assigned, and the new-assignment form.
/// </summary>
/// <remarks>
/// The shape is the one the screen needs, so the view does not have to look anything up. Only
/// <see cref="NewAssignment"/> is posted; <see cref="Project"/> and <see cref="Employees"/> are
/// display data that the controller rebuilds from the database on every render, including the
/// re-render after a rejected post, so a crafted body cannot change what the page shows.
/// </remarks>
public class ProjectDetailsViewModel
{
    public ProjectDetailDto Project { get; set; } = new();

    /// <summary>Active employees as "Full name (EmployeeNumber)".</summary>
    public IEnumerable<SelectListItem> Employees { get; set; } = Array.Empty<SelectListItem>();

    public AssignEmployeeRequest NewAssignment { get; set; } = new();
}
