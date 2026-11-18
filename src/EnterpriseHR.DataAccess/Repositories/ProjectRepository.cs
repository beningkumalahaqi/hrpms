using Dapper;
using EnterpriseHR.DataAccess.Infrastructure;
using EnterpriseHR.Domain.Dtos;

namespace EnterpriseHR.DataAccess.Repositories;

/// <summary>
/// Project reads: the list with its active-member count, and the details view with every
/// assignment. The write path arrives in a following stage, and <c>IProjectRepository</c>
/// is added to the base list once its last member exists.
/// </summary>
public sealed class ProjectRepository(IDbConnectionFactory factory)
    : RepositoryBase(factory)
{
    // One statement, one round trip. The Employees join carries the IsActive filter, so an
    // inactive member still has an assignment row but is not counted (COUNT skips NULLs).
    private const string SelectProjectsSql = """
        SELECT p.Id,
               p.ProjectCode,
               p.ProjectName,
               d.DepartmentName,
               p.StartDate,
               p.EndDate,
               p.Status,
               COUNT(e.Id) AS ActiveMemberCount
        FROM dbo.Projects p
        INNER JOIN dbo.Departments d ON d.Id = p.DepartmentId
        LEFT JOIN dbo.ProjectAssignments pa ON pa.ProjectId = p.Id
        LEFT JOIN dbo.Employees e ON e.Id = pa.EmployeeId AND e.IsActive = 1
        GROUP BY p.Id, p.ProjectCode, p.ProjectName, d.DepartmentName, p.StartDate, p.EndDate, p.Status
        ORDER BY p.StartDate DESC, p.ProjectCode;
        """;

    // Two result sets in one batch: the project, then every assignment of that project.
    // Assignments are never filtered by employee or project status, so a completed project
    // still shows who worked on it and an inactive member is still listed with
    // IsEmployeeActive = 0.
    private const string SelectProjectDetailsSql = """
        SELECT p.Id,
               p.ProjectCode,
               p.ProjectName,
               p.DepartmentId,
               d.DepartmentName,
               p.StartDate,
               p.EndDate,
               p.Status
        FROM dbo.Projects p
        INNER JOIN dbo.Departments d ON d.Id = p.DepartmentId
        WHERE p.Id = @Id;

        SELECT pa.Id AS AssignmentId,
               pa.EmployeeId,
               e.EmployeeNumber,
               e.FullName,
               pos.Title AS PositionTitle,
               pa.RoleInProject,
               pa.AllocationPercentage,
               pa.AssignedDate,
               e.IsActive AS IsEmployeeActive
        FROM dbo.ProjectAssignments pa
        INNER JOIN dbo.Employees e ON e.Id = pa.EmployeeId
        INNER JOIN dbo.Positions pos ON pos.Id = e.PositionId
        WHERE pa.ProjectId = @Id
        ORDER BY e.FullName;
        """;

    public async Task<IReadOnlyList<ProjectListItemDto>> GetAllProjectsAsync(
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        var projects = await conn.QueryAsync<ProjectListItemDto>(
            new CommandDefinition(SelectProjectsSql, cancellationToken: ct));

        return projects.AsList();
    }

    public async Task<ProjectDetailDto?> GetProjectDetailsAsync(
        int id,
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        await using var grid = await conn.QueryMultipleAsync(
            new CommandDefinition(SelectProjectDetailsSql, new { Id = id }, cancellationToken: ct));

        var project = await grid.ReadFirstOrDefaultAsync<ProjectDetailDto>();

        // Read only the first result set when the project does not exist; the second one is
        // empty anyway, and there is nothing to attach it to.
        if (project is null)
        {
            return null;
        }

        project.Members = (await grid.ReadAsync<ProjectMemberDto>()).ToList();

        return project;
    }
}
