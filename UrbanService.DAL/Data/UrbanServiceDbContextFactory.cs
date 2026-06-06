using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UrbanService.DAL.Data;

public class UrbanServiceDbContextFactory : IDesignTimeDbContextFactory<UrbanServiceDbContext>
{
    public UrbanServiceDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile(Path.Combine("UrbanService", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine("UrbanService", $"appsettings.{environment}.json"), optional: true)
            .Build();

        var connectionString = Environment.GetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Missing connection string: ConnectionStrings:DefaultConnection.");

        var optionsBuilder = new DbContextOptionsBuilder<UrbanServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new UrbanServiceDbContext(optionsBuilder.Options);
    }
}
