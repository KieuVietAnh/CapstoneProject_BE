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
        var currentDirectory = Directory.GetCurrentDirectory();
        var appSettingsDirectory = ResolveAppSettingsDirectory(currentDirectory);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(appSettingsDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
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

    private static string ResolveAppSettingsDirectory(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory != null)
        {
            var directAppSettings = Path.Combine(directory.FullName, "appsettings.json");
            if (File.Exists(directAppSettings))
            {
                return directory.FullName;
            }

            var apiAppSettings = Path.Combine(directory.FullName, "UrbanService", "appsettings.json");
            if (File.Exists(apiAppSettings))
            {
                return Path.Combine(directory.FullName, "UrbanService");
            }

            directory = directory.Parent;
        }

        return startDirectory;
    }
}
