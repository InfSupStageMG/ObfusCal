using Microsoft.EntityFrameworkCore;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Helpers;

internal static class TestDbContextFactory
{
    internal static AppDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options, new PassthroughColumnEncryptor());
    }
}

