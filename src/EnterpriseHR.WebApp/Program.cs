using System.Text.Json;
using EnterpriseHR.Api.Controllers;
using EnterpriseHR.Api.Middleware;
using EnterpriseHR.Api.Models;
using EnterpriseHR.DataAccess;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// One pipeline serves both halves of the application: the Razor views in this assembly and the
// attribute-routed API controllers in EnterpriseHR.Api. The API assembly is registered as an
// MVC application part so its controllers are discovered where they live, without their source
// moving into this project.
builder.Services
    .AddControllersWithViews()
    .AddApplicationPart(typeof(ApiControllerBase).Assembly)
    .AddJsonOptions(options =>
    {
        // camelCase is System.Text.Json's default, but the response envelope is a contract, so
        // it is pinned here instead of inherited from whatever the default happens to be.
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// [ApiController] turns a model-binding failure into a 400 before the action runs. Without
// this, the body is the ProblemDetails shape; with it, every failure looks like every
// success. Data carries the per-field messages, message says what went wrong overall.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => ToCamelCasePath(entry.Key),
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The value is invalid."
                        : error.ErrorMessage)
                    .ToArray());

        return new BadRequestObjectResult(
            ApiResponse<IDictionary<string, string[]>>.Fail("Validation failed.", errors));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "EnterpriseHR API v1",
        Version = "v1",
        Description = "Employee, project, assignment and reporting endpoints for EnterpriseHRSystem.",
    });

    // The XML documentation belongs to the API assembly, which is where the controllers and
    // response models live. Only picked up when that project emits its XML file; it does not
    // enable <GenerateDocumentationFile> yet, so this is a no-op until it does.
    var xmlFile = Path.Combine(
        AppContext.BaseDirectory,
        $"{typeof(ApiControllerBase).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

// Registered once for the whole host: MVC screens and API endpoints resolve the same
// repositories and services from the same connection string.
builder.Services.AddEnterpriseHrDataAccess(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    // Swagger documents the API controllers served by this same host, so it lives at
    // http://localhost:5100/swagger next to the MVC pages.
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Registered ahead of routing so it can also catch what routing throws, and before the MVC
// exception handler above so an API failure is answered with JSON instead of reaching that
// HTML page. The middleware itself rethrows any non-/api request, which is what keeps MVC
// pages on their own error handling.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Body-less MVC statuses (an unknown page, a 405 on a page route) get the branded error view
// instead of an empty response. It is registered outside the /api branch below, so the JSON
// envelope for an unknown API route is written first and this middleware then sees a started
// response and leaves it alone. The re-executed action puts the original status back, so a
// 404 page still answers 404.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

// Framework-generated /api/** responses that carry no body (404 for an unknown route, 405 for
// a wrong verb) get the standard envelope here. Wrapping the status, rather than the request,
// means MVC pages are untouched: a 404 there goes through the status-code pages above, and a
// 405 still follows the MVC pipeline.
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    api => api.Use(async (ctx, next) =>
    {
        await next();

        if (ctx.Response.HasStarted || ctx.Response.ContentLength > 0)
        {
            return;
        }

        // Leave /api/** paths that Swagger or a future static endpoint answered alone.
        if (ctx.Response.ContentType is not null)
        {
            return;
        }

        if (ctx.Response.StatusCode is StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed)
        {
            var message = ctx.Response.StatusCode == StatusCodes.Status405MethodNotAllowed
                ? "The HTTP method is not supported for this resource."
                : "The requested resource was not found.";

            ctx.Response.ContentType = "application/json; charset=utf-8";

            await ctx.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message));
        }
    }));

app.UseRouting();

app.UseAuthorization();

// Attribute routing wins over the conventional route, so /api/** reaches the API controllers
// and every other path falls through to {controller=Home}/{action=Index}/{id?}.
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ModelState keys are C# property names ("NewSalary", and nested paths like "Body.Name"). The
// request fields are serialized camelCase, so an error key is converted to match what the
// client actually sent. A key that is not a property path (the "body" entry for unparseable
// JSON, "$.name" for a JSON syntax error) is passed through untouched.
static string ToCamelCasePath(string key)
{
    if (key.Length == 0 || !char.IsUpper(key[0]))
    {
        return key;
    }

    return string.Join('.', key.Split('.').Select(segment =>
        segment.Length == 0 || !char.IsUpper(segment[0])
            ? segment
            : char.ToLowerInvariant(segment[0]) + segment[1..]));
}
