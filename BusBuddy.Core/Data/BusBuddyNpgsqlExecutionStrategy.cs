using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace BusBuddy.Core.Data;

/// <summary>
/// Retries transient Postgres failures. A refused TCP connection means nothing is listening on 5432;
/// retrying it only freezes the clerk for the full backoff (guest log: 33s metrics, 101s dashboard).
/// </summary>
public sealed class BusBuddyNpgsqlExecutionStrategy : NpgsqlRetryingExecutionStrategy
{
    public BusBuddyNpgsqlExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null)
    {
    }

    public static bool IsConnectionRefused(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is SocketException socket && socket.SocketErrorCode == SocketError.ConnectionRefused)
            {
                return true;
            }

            if (ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected override bool ShouldRetryOn(Exception? exception)
    {
        if (exception is null || IsConnectionRefused(exception))
        {
            return false;
        }

        return base.ShouldRetryOn(exception);
    }
}
