using EnterpriseHR.DataAccess.Infrastructure;
using EnterpriseHR.DataAccess.Repositories;
using EnterpriseHR.DataAccess.Services;
using EnterpriseHR.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseHR.DataAccess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseHrDataAccess(
        this IServiceCollection services,
        IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        var connectionString = config.GetConnectionString("EnterpriseHR");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'EnterpriseHR' is not configured. Set ConnectionStrings:EnterpriseHR "
                + "in appsettings.Development.json, or override it with the ConnectionStrings__EnterpriseHR "
                + "environment variable.");
        }

        // Singleton: the factory holds only the connection string and hands out a new
        // connection per call, which is what makes it safe to share across requests.
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        // Scoped: a repository owns no state between calls, but scoping keeps one instance per
        // request so a future change (a shared transaction, a unit of work) does not have to
        // re-register anything, and it matches the lifetime of the services built on top.
        services.AddScoped<ILookupRepository, LookupRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
