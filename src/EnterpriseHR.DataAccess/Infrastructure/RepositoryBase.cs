using System.Data;
using System.Data.Common;
using EnterpriseHR.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace EnterpriseHR.DataAccess.Infrastructure;

/// <summary>
/// Base class for every Dapper repository. It hands out open connections, owns the only
/// transaction runner in the data-access layer, and turns SQL constraint violations into
/// the domain exceptions the hosts already map to 409 / 400.
/// </summary>
public abstract class RepositoryBase(IDbConnectionFactory factory)
{
    /// <summary>Opens a connection the caller owns and disposes with <c>await using</c>.</summary>
    protected Task<DbConnection> OpenAsync(CancellationToken ct = default)
    {
        return factory.CreateOpenConnectionAsync(ct);
    }

    /// <summary>
    /// Runs <paramref name="work"/> on one connection inside a single ReadCommitted
    /// transaction: commits when the work completes, rolls back and rethrows untouched
    /// when it throws.
    /// </summary>
    protected async Task<T> ExecuteInTransactionAsync<T>(
        Func<DbConnection, DbTransaction, Task<T>> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var conn = await factory.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            var result = await work(conn, tx);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            try
            {
                // CancellationToken.None: a cancelled token must not be able to skip the rollback.
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Rollback can fail on a transaction SQL Server already aborted, or on a dead
                // connection. The `await using` above closes the connection either way, which
                // rolls the work back server-side; keeping this failure out of the way is what
                // lets the original exception reach the caller intact.
            }

            throw;
        }
    }

    /// <summary>Void counterpart of <see cref="ExecuteInTransactionAsync{T}"/>.</summary>
    protected Task ExecuteInTransactionAsync(
        Func<DbConnection, DbTransaction, Task> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        return ExecuteInTransactionAsync<bool>(
            async (conn, tx) =>
            {
                await work(conn, tx);
                return true;
            },
            ct);
    }

    /// <summary>
    /// Maps the SQL Server constraint errors a repository can provoke onto domain
    /// exceptions; any other error is returned unchanged so the caller still sees the
    /// original <see cref="SqlException"/>.
    /// </summary>
    protected static Exception TranslateSql(SqlException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex.Number switch
        {
            2627 or 2601 => new ConflictException(
                "Duplicate value violates a unique constraint.", ex),
            547 => new BusinessRuleException(
                "A referenced record does not exist or is still in use.", ex),
            _ => ex,
        };
    }

    /// <summary>
    /// Runs a Dapper call and converts the three constraint error numbers covered by
    /// <see cref="TranslateSql"/> into domain exceptions; everything else propagates as-is.
    /// </summary>
    protected static async Task<T> ExecuteSqlAsync<T>(Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        try
        {
            return await work();
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601 or 547)
        {
            throw TranslateSql(ex);
        }
    }

    /// <summary>Void counterpart of <see cref="ExecuteSqlAsync{T}"/>.</summary>
    protected static Task ExecuteSqlAsync(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        return ExecuteSqlAsync<bool>(async () =>
        {
            await work();
            return true;
        });
    }
}
