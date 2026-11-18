using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.Domain.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectListItemDto>> GetAllAsync(CancellationToken ct = default);

    Task<ProjectDetailDto?> GetDetailsAsync(int id, CancellationToken ct = default);

    Task<int> AssignAsync(int projectId, AssignEmployeeRequest request, CancellationToken ct = default);

    // Active employees only.
    Task<IReadOnlyList<SelectOptionDto>> GetAssignableEmployeeOptionsAsync(CancellationToken ct = default);
}
