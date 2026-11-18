using EnterpriseHR.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseHR.Api.Controllers;

/// <summary>
/// Base for every API controller: it owns the <c>api/[controller]</c> route template and the
/// <see cref="ApiControllerAttribute"/> behaviour (automatic 400 on an invalid model), and the
/// two helpers that wrap a payload in <see cref="ApiResponse{T}"/>. A derived controller
/// declares only its actions and their verbs, so every endpoint answers with the same shape.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> OkResponse<T>(T data)
    {
        return Ok(ApiResponse<T>.Ok(data));
    }

    protected ActionResult<ApiResponse<T>> CreatedResponse<T>(
        string actionName,
        object? routeValues,
        T data)
    {
        return CreatedAtAction(actionName, routeValues, ApiResponse<T>.Ok(data, "Created."));
    }
}
