using LayoutManagementAPI.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagement.Tests;

/// <summary>
/// A disposable in-memory SQLite database for exercising the real
/// <see cref="RealEstateDbContext"/> schema (not the EF in-memory provider,
/// so unique indexes and column behavior match production).
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public RealEstateDbContext Db { get; }

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Db = NewContext();
        Db.Database.EnsureCreated();
    }

    /// <summary>A fresh context over the same connection, to read back persisted state.</summary>
    public RealEstateDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<RealEstateDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new RealEstateDbContext(options);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
