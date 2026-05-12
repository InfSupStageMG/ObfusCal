using Microsoft.EntityFrameworkCore;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Helpers;

/// <summary>
/// Lightweight test infrastructure shared across unit and integration test namespaces.
/// </summary>
internal static class TestDbContextFactory
{
    /// <summary>Creates a fresh in-memory <see cref="AppDbContext"/> isolated to a single test.</summary>
    internal static AppDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options, new PassthroughColumnEncryptor());
    }
}

