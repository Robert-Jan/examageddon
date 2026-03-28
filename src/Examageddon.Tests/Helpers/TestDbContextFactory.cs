using Examageddon.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Examageddon.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ExamageddonDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ExamageddonDbContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new ExamageddonDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
