using System;
using System.Net.Sockets;
using System.Reflection;
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class DatabaseUserMessageTests
{
    [Test]
    public void IsConnectivityFailure_detects_npgsql_timeout_chain()
    {
        var ex = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new NpgsqlException("Failed to connect to 192.168.1.153:5432", new TimeoutException("timed out")));

        DatabaseUserMessage.IsConnectivityFailure(ex).Should().BeTrue();
    }

    [Test]
    public void ForOperation_returns_actionable_text_for_connectivity_errors()
    {
        var ex = new InvalidOperationException(
            "transient failure",
            new NpgsqlException("Failed to connect to 192.168.1.153:5432", new TimeoutException()));

        var message = DatabaseUserMessage.ForOperation(ex, "save the student");

        message.Should().Contain("save the student");
        message.Should().Contain("Postgres");
        message.Should().NotContain("transient failure");
    }

    [Test]
    public void ForOperation_preserves_non_connectivity_message()
    {
        var ex = new InvalidOperationException("Student number already exists.");

        DatabaseUserMessage.ForOperation(ex, "save the student")
            .Should().Be("Failed to save the student: Student number already exists.");
    }

    [Test]
    public void IsConnectivityFailure_detects_connection_refused_after_retry_limit()
    {
        var refused = new SocketException((int)SocketError.ConnectionRefused);
        var npgsql = new NpgsqlException("Failed to connect to 192.168.64.1:5432", refused);
        var exhausted = new RetryLimitExceededException(
            "The maximum number of retries (5) was exceeded",
            npgsql);

        DatabaseUserMessage.IsConnectivityFailure(exhausted).Should().BeTrue();
        var failed = Result.FailureResult<int>("Error retrieving students", exhausted);
        DatabaseUserMessage.IsConnectivityFailure(failed.Exception).Should().BeTrue();
        DatabaseUserMessage.IsConnectivityFailure(Result.FailureResult<int>("Invalid routeId").Exception).Should().BeFalse();
        BusBuddyNpgsqlExecutionStrategy.IsConnectionRefused(exhausted).Should().BeTrue();
    }

    [Test]
    public void ConnectionRefused_IsNotRetried()
    {
        var builder = new DbContextOptionsBuilder<BusBuddyDbContext>();
        builder.UseBusBuddyPostgres("Host=127.0.0.1;Port=1;Database=busbuddy;Username=u;Password=p");
        using var context = new BusBuddyDbContext(builder.Options);
        var strategy = context.Database.CreateExecutionStrategy();
        strategy.Should().BeOfType<BusBuddyNpgsqlExecutionStrategy>();

        var refused = new NpgsqlException(
            "Failed to connect to 192.168.64.1:5432",
            new SocketException((int)SocketError.ConnectionRefused));
        var shouldRetry = (bool)typeof(BusBuddyNpgsqlExecutionStrategy)
            .GetMethod("ShouldRetryOn", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(strategy, new object[] { refused })!;
        shouldRetry.Should().BeFalse();
    }

    [Test]
    public void IsConnectivityFailure_detects_timeout_without_npgsql_wrapper()
    {
        DatabaseUserMessage.IsConnectivityFailure(new TimeoutException("The operation has timed out."))
            .Should()
            .BeTrue();
    }

    [Test]
    public void ForOperation_describes_postgres_foreign_key_violation()
    {
        var ex = new DbUpdateException(
            "An error occurred while saving the entity changes. See the inner exception for details.",
            new PostgresException("insert or update on table \"Students\" violates foreign key constraint", severity: string.Empty, invariantSeverity: string.Empty, sqlState: PostgresErrorCodes.ForeignKeyViolation));

        DatabaseUserMessage.ForOperation(ex, "save the student")
            .Should().Contain("related record is missing");
    }
}
