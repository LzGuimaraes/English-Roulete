using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnglishRoulette.Infrastructure.Persistence;

public sealed class AdventureDbContextFactory : IDesignTimeDbContextFactory<AdventureDbContext>
{
    public AdventureDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Adventure")
            ?? "Host=localhost;Port=55432;Database=englishroulette;Username=englishroulette";
        return new(new DbContextOptionsBuilder<AdventureDbContext>().UseNpgsql(connection).Options);
    }
}
