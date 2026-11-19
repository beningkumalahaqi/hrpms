using EnterpriseHR.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseHR.WebApp.Controllers;

/// <summary>
/// The project screens. The list reads through the same <see cref="IProjectService"/> that
/// answers <c>/api/projects/{id}/details</c>, so the portfolio and the API cannot drift apart.
/// </summary>
public class ProjectsController : Controller
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    /// <summary>GET /Projects: the whole portfolio with its active team size.</summary>
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
}
