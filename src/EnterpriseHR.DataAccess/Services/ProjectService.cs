using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.DataAccess.Services;

/// <summary>
/// Orchestrates the project features: reads pass straight through, and the assignment path
/// gets its role trimmed so the workload rules see the value the user meant to send.
/// The 100% cap, the status checks and the duplicate guard all stay in
/// <see cref="IProjectRepository"/>.
/// </summary>
public sealed class ProjectService(
    IProjectRepository projects,
    ILookupRepository lookups) : IProjectService
{
    public Task<IReadOnlyList<ProjectListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        return projects.GetAllProjectsAsync(ct);
    }

    public Task<ProjectDetailDto?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        return projects.GetProjectDetailsAsync(id, ct);
    }

    public Task<int> AssignAsync(
        int projectId,
        AssignEmployeeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.RoleInProject = request.RoleInProject?.Trim() ?? string.Empty;

        return projects.AssignEmployeeToProjectAsync(projectId, request, ct);
    }

    /// <summary>
    /// The assignment dropdown lists active employees only, and never excludes anyone: the
    /// employee being considered for the project is exactly who the caller is picking.
    /// </summary>
    public Task<IReadOnlyList<SelectOptionDto>> GetAssignableEmployeeOptionsAsync(
        CancellationToken ct = default)
    {
        return lookups.GetEmployeeOptionsAsync(activeOnly: true, excludeEmployeeId: null, ct);
    }
}
