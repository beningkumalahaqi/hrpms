using Dapper;
using EnterpriseHR.DataAccess.Infrastructure;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Exceptions;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;

namespace EnterpriseHR.DataAccess.Repositories;

/// <summary>
/// Project reads and the assignment write path: the list with its active-member count, the
/// details view with every assignment, and assigning an employee under the 100% workload rule.
/// </summary>
public sealed class ProjectRepository(IDbConnectionFactory factory)
    : RepositoryBase(factory), IProjectRepository
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

    // UPDLOCK/ROWLOCK on the employee is what serializes concurrent assignments of the same
    // person: the second request waits here, then sees the first one's row when it reads the
    // workload further down.
    private const string SelectEmployeeForAssignmentSql = """
        SELECT Id, IsActive, FullName
        FROM dbo.Employees WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @EmployeeId;
        """;

    private const string SelectProjectForAssignmentSql = """
        SELECT Id, Status
        FROM dbo.Projects
        WHERE Id = @ProjectId;
        """;

    // UQ_ProjectAssignments_Project_Employee makes this an index seek on the unique key.
    private const string SelectAssignmentExistsSql = """
        SELECT CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM dbo.ProjectAssignments
                   WHERE ProjectId = @ProjectId AND EmployeeId = @EmployeeId)
               THEN 1 ELSE 0 END AS BIT);
        """;

    // A4: only Active and Planning projects count toward the workload. Completed and
    // Cancelled assignments belong to finished work and are ignored.
    private const string SelectCurrentAllocationSql = """
        SELECT ISNULL(SUM(pa.AllocationPercentage), 0)
        FROM dbo.ProjectAssignments pa
        INNER JOIN dbo.Projects p ON p.Id = pa.ProjectId
        WHERE pa.EmployeeId = @EmployeeId
          AND p.Status IN ('Active', 'Planning');
        """;

    // AssignedDate is left to DF_ProjectAssignments_AssignedDate.
    private const string InsertAssignmentSql = """
        INSERT INTO dbo.ProjectAssignments
            (ProjectId, EmployeeId, RoleInProject, AllocationPercentage)
        VALUES
            (@ProjectId, @EmployeeId, @RoleInProject, @AllocationPercentage);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
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

    public async Task<int> AssignEmployeeToProjectAsync(
        int projectId,
        AssignEmployeeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Cheap input checks first: they need no database round trip and cannot depend on
        // the transaction's view of the data.
        if (request.AllocationPercentage is < 1 or > 100)
        {
            throw new BusinessRuleException(
                "Allocation percentage must be between 1 and 100.");
        }

        if (string.IsNullOrWhiteSpace(request.RoleInProject))
        {
            throw new BusinessRuleException("Role in project is required.");
        }

        return await ExecuteInTransactionAsync(
            async (conn, tx) =>
            {
                // Locking the employee serializes every concurrent assignment that would
                // change this person's workload, so the 100% check below cannot be raced.
                var employee = await conn.QuerySingleOrDefaultAsync<EmployeeAssignmentState>(
                    new CommandDefinition(SelectEmployeeForAssignmentSql,
                        new { request.EmployeeId }, transaction: tx, cancellationToken: ct));

                if (employee is null)
                {
                    throw new NotFoundException($"Employee {request.EmployeeId} was not found.");
                }

                if (!employee.IsActive)
                {
                    throw new BusinessRuleException("Employee is not active.");
                }

                var project = await conn.QuerySingleOrDefaultAsync<ProjectAssignmentState>(
                    new CommandDefinition(SelectProjectForAssignmentSql,
                        new { projectId }, transaction: tx, cancellationToken: ct));

                if (project is null)
                {
                    throw new NotFoundException($"Project {projectId} was not found.");
                }

                if (project.Status is not ("Active" or "Planning"))
                {
                    throw new BusinessRuleException(
                        $"Project status '{project.Status}' does not accept new assignments.");
                }

                var alreadyAssigned = await conn.ExecuteScalarAsync<bool>(
                    new CommandDefinition(SelectAssignmentExistsSql,
                        new { projectId, request.EmployeeId }, transaction: tx, cancellationToken: ct));

                if (alreadyAssigned)
                {
                    throw new ConflictException("Employee is already assigned to this project.");
                }

                var currentAllocation = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(SelectCurrentAllocationSql,
                        new { request.EmployeeId }, transaction: tx, cancellationToken: ct));

                var total = currentAllocation + request.AllocationPercentage;

                if (total > 100)
                {
                    throw new ConflictException(
                        $"Allocation would reach {total}% (current {currentAllocation}% + "
                        + $"requested {request.AllocationPercentage}%). Maximum is 100%.");
                }

                // The UQ_ProjectAssignments_Project_Employee constraint is the real guard
                // against two concurrent identical assignments; the check above only exists
                // to produce a readable message.
                return await ExecuteSqlAsync(async () =>
                    await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(InsertAssignmentSql, new
                        {
                            projectId,
                            request.EmployeeId,
                            request.RoleInProject,
                            request.AllocationPercentage,
                        }, transaction: tx, cancellationToken: ct)));
            },
            ct);
    }

    /// <summary>The employee fields the assignment rules need, read under UPDLOCK.</summary>
    private sealed class EmployeeAssignmentState
    {
        public int Id { get; set; }

        public bool IsActive { get; set; }

        public string FullName { get; set; } = string.Empty;
    }

    /// <summary>The project fields the assignment rules need.</summary>
    private sealed class ProjectAssignmentState
    {
        public int Id { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
