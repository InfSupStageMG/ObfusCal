using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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

        return new AppDbContext(optionsBuilder.Options);
    }
}

