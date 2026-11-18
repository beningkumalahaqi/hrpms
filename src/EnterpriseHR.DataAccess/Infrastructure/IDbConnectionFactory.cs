using System.Data.Common;

namespace EnterpriseHR.DataAccess.Infrastructure;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
