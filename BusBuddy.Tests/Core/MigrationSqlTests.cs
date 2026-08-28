using BusBuddy.Core.Data;
using BusBuddy.Core.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class MigrationSqlTests
{
    [Test]
    public void BoolType_IsBoolean_OnNpgsql()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.That(MigrationSql.IsNpgsql(builder), Is.True);
        Assert.That(MigrationSql.BoolType(builder), Is.EqualTo("boolean"));
        Assert.That(MigrationSql.DateTimeType(builder), Is.EqualTo("timestamp with time zone"));
        Assert.That(MigrationSql.UtcNow(builder), Is.EqualTo("CURRENT_TIMESTAMP"));
        Assert.That(MigrationSql.StringType(builder, 100), Is.EqualTo("character varying(100)"));
    }

    [Test]
    public void BoolType_IsBit_OnSqlServer()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        Assert.That(MigrationSql.IsNpgsql(builder), Is.False);
        Assert.That(MigrationSql.BoolType(builder), Is.EqualTo("bit"));
        Assert.That(MigrationSql.DateTimeType(builder), Is.EqualTo("datetime2"));
        Assert.That(MigrationSql.UtcNow(builder), Is.EqualTo("GETUTCDATE()"));
        Assert.That(MigrationSql.StringType(builder, 100), Is.EqualTo("nvarchar(100)"));
    }

    [Test]
    public void Apply_OnInMemory_DoesNotThrow()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase("schema-applier-" + Guid.NewGuid().ToString("N"))
            .Options;
        var previous = BusBuddyDbContext.SkipGlobalSeedData;
        BusBuddyDbContext.SkipGlobalSeedData = true;
        try
        {
            using var ctx = new BusBuddyDbContext(options);
            RelationalSchemaApplier.Apply(ctx.Database);
        }
        finally
        {
            BusBuddyDbContext.SkipGlobalSeedData = previous;
        }
    }
}
