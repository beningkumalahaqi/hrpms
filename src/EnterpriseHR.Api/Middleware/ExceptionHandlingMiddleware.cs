using EnterpriseHR.Api.Models;
using EnterpriseHR.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace EnterpriseHR.Api.Middleware;

/// <summary>
/// Turns a failed <c>/api/**</c> request into the same envelope every successful one uses, so a
/// client can branch on <c>success</c> without special-casing status codes. Only API paths are
/// handled: anything else is rethrown untouched, which is what lets the MVC pipeline keep its
/// own error handling (the developer exception page in Development, <c>/Home/Error</c> in
/// Production) instead of receiving a JSON body it cannot render.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Not an API request: let the exception travel to the MVC error handling above this
        // middleware. Catching it here would replace a rendered HTML error page with JSON.
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await _next(ctx);
            return;
        }

        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // The client hung up. There is nobody left to send a body to, and writing one would
            // throw again; the aborted connection is the expected outcome, not an error.
            _logger.LogDebug(
                "Request aborted by the client: {Method} {Path}",
                ctx.Request.Method,
                ctx.Request.Path);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(ctx, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext ctx, Exception ex)
    {
        var (status, message) = Map(ex);

        if (status == StatusCodes.Status500InternalServerError)
        {
            // The only place the full exception is recorded. The response below carries a fixed
            // sentence, so neither the stack trace nor the exception message reaches the client.
            _logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}",
                ctx.Request.Method,
                ctx.Request.Path);
        }
        else if (status == StatusCodes.Status409Conflict && ex is SqlException)
        {
            // A constraint error that reached the host means a repository did not translate it.
            // The response is still a clean 409, but the leak is a bug worth an error log.
            _logger.LogError(
                ex,
                "Untranslated SQL constraint error for {Method} {Path}",
                ctx.Request.Method,
                ctx.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Request failed with {Status}: {Method} {Path} - {Message}",
                status,
                ctx.Request.Method,
                ctx.Request.Path,
                ex.Message);
        }

        // A response that already started cannot be repaired: its headers and part of its body
        // are on the wire. Rethrowing lets the host abort the connection rather than pretend the
        // truncated response was complete.
        if (ctx.Response.HasStarted)
        {
            _logger.LogError(
                "Response had already started; the error response could not be written for {Path}",
                ctx.Request.Path);
            throw ex;
        }

        // Drop anything the failed action left behind (Content-Type, Content-Length, cookies)
        // before replacing the whole response with the envelope.
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";

        await ctx.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message));
    }

    /// <summary>
    /// Maps an exception to a status code and the sentence the client sees. Domain exceptions
    /// carry a message written for the user; every other failure gets the same generic sentence
    /// so internals never leak.
    /// </summary>
    private static (int Status, string Message) Map(Exception ex)
    {
        return ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            BusinessRuleException => (StatusCodes.Status400BadRequest, ex.Message),
            ConflictException => (StatusCodes.Status409Conflict, ex.Message),

            // Safety net: repositories translate these already, so reaching here means one
            // escaped. Same status codes the data layer would have produced.
            SqlException { Number: 2627 or 2601 } => (
                StatusCodes.Status409Conflict,
                "Duplicate value violates a unique constraint."),
            SqlException { Number: 547 } => (
                StatusCodes.Status400BadRequest,
                "A referenced record does not exist or is still in use."),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };
    }
}
