using EnterpriseHR.DataAccess.Infrastructure;
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

        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        // Repositories are registered as scoped as they land (T12-T20).
        return services;
    }
}
