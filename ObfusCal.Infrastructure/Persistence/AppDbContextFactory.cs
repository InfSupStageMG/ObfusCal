using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Infrastructure.Persistence;

/// <summary>
/// Allows EF Core design-time tools (dotnet ef migrations add) to instantiate
/// <see cref="AppDbContext"/> without the web host running.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=obfuscal;Username=postgres;Password=postgres");

        // Use a passthrough encryptor at design time — migrations must not require
        // the production encryption key to be available.
        return new AppDbContext(optionsBuilder.Options, new PassthroughColumnEncryptor());
    }
}

