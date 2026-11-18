using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.Domain.Interfaces;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectListItemDto>> GetAllProjectsAsync(CancellationToken ct = default);

    Task<ProjectDetailDto?> GetProjectDetailsAsync(int id, CancellationToken ct = default);

    // Returns the new assignment id.
    Task<int> AssignEmployeeToProjectAsync(
        int projectId,
        AssignEmployeeRequest request,
        CancellationToken ct = default);
}
