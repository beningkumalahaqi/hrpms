using EnterpriseHR.Api.Models;
using EnterpriseHR.Domain.Dtos;
using EnterpriseHR.Domain.Exceptions;
using EnterpriseHR.Domain.Interfaces;
using EnterpriseHR.Domain.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseHR.Api.Controllers;

/// <summary>
/// Project endpoints: one project with its member list, and the assignment of an employee to a
/// project.
/// </summary>
/// <remarks>
/// The assignment rules live in the repository, not here: inactive employee, a project that is
/// not <c>Active</c>/<c>Planning</c> and an out-of-range percentage are
/// <see cref="BusinessRuleException"/>; a duplicate or an allocation that would exceed 100% is
/// <see cref="ConflictException"/>; an unknown project or employee is
/// <see cref="NotFoundException"/>. The controller lets all three escape and
/// <c>ExceptionHandlingMiddleware</c> turns them into 400/409/404 envelopes.
/// </remarks>
public sealed class ProjectsController : ApiControllerBase
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    /// <summary>
    /// Gets one project with every employee assigned to it, including former staff.
    /// </summary>
    /// <returns>
    /// The <see cref="ProjectDetailDto"/>, whose <c>members</c> array carries each assignment's
    /// role, allocation and <c>isEmployeeActive</c> flag. A member who has since been
    /// soft-deleted is still listed, with <c>isEmployeeActive: false</c>, so the project history
    /// stays complete.
    /// </returns>
    /// <response code="200">The project and its members.</response>
    /// <response code="404">No project with that id; <c>message</c> is <c>"Project not found."</c>.</response>
    /// <response code="500">An unexpected failure; the message is generic and carries no internals.</response>
    [HttpGet("{id:int}/details")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ProjectDetailDto>>> GetDetails(
        int id,
        CancellationToken ct)
    {
        var project = await _projects.GetDetailsAsync(id, ct);

        // Thrown rather than returned as NotFound(...) so the 404 body is built in the same
        // place as every other error envelope instead of being assembled per endpoint.
        return project is null
            ? throw new NotFoundException("Project not found.")
            : OkResponse(project);
    }

    /// <summary>
    /// Assigns an employee to a project.
    /// </summary>
    /// <param name="request">
    /// <c>employeeId</c>, <c>roleInProject</c> (100 characters max) and
    /// <c>allocationPercentage</c> (1-100).
    /// </param>
    /// <returns>
    /// 201 with the new assignment's id and the values it was stored with, and a
    /// <c>Location</c> header pointing at this project's details.
    /// </returns>
    /// <remarks>
    /// The percentage is a <em>share of the employee's total workload</em>, not of this project:
    /// an employee at 60% elsewhere cannot take another 50%. A rejected request writes nothing;
    /// the employee row is locked before the workload is summed, and the insert is the last
    /// statement in the transaction, so every rule fires before a row exists.
    /// </remarks>
    /// <response code="201">Assignment created.</response>
    /// <response code="400">
    /// The body failed validation (missing role, percentage outside 1-100), or the rule failed:
    /// the employee is inactive, or the project is not <c>Active</c>/<c>Planning</c>.
    /// </response>
    /// <response code="404">No project or no employee with that id.</response>
    /// <response code="409">
    /// The employee is already on this project, or the assignment would take their total
    /// allocation above 100%.
    /// </response>
    /// <response code="500">An unexpected failure; the message is generic and carries no internals.</response>
    [HttpPost("{id:int}/assign")]
    [ProducesResponseType(typeof(ApiResponse<CreatedAssignmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<CreatedAssignmentDto>>> Assign(
        int id,
        [FromBody] AssignEmployeeRequest request,
        CancellationToken ct)
    {
        var assignmentId = await _projects.AssignAsync(id, request, ct);

        // Built from the id the insert returned plus the values just persisted, so the client
        // gets the assignment it can quote back without a second round trip. The project id is
        // the route value: the request body deliberately carries no project.
        var created = new CreatedAssignmentDto(
            assignmentId,
            id,
            request.EmployeeId,
            request.AllocationPercentage);

        return CreatedResponse(nameof(GetDetails), new { id }, created);
    }

    /// <summary>
    /// The assignment id plus the values it was created with.
    /// </summary>
    /// <remarks>
    /// Nested here rather than in <c>Domain/Dtos</c>: it is the response shape of this one
    /// endpoint, and nothing else in the application reads it. A record so the four values
    /// cannot drift from the insert that produced them.
    /// </remarks>
    public sealed record CreatedAssignmentDto(
        int AssignmentId,
        int ProjectId,
        int EmployeeId,
        int AllocationPercentage);
}
