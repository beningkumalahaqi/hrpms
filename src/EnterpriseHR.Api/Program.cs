using System.Reflection;
using System.Text.Json;
using EnterpriseHR.Api.Models;
using EnterpriseHR.DataAccess;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers()
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

    // Only picked up when the project emits its XML documentation file; the project does not
    // enable <GenerateDocumentationFile> yet, so this is a no-op until it does.
    var xmlFile = Path.Combine(
        AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

builder.Services.AddEnterpriseHrDataAccess(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

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
