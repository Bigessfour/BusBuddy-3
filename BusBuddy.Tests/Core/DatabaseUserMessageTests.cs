using System;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
