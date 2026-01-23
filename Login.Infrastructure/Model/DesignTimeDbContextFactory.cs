using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Login.Infrastructure.Model;

public class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        // Intenta leer appsettings del WebApi (startup project)
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Login.WebApi");

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("loginConection");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("No se encontró ConnectionStrings:loginConection");

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(cs, npgsql => npgsql.MigrationsAssembly("Login.Infrastructure"))
            .Options;

        return new DataContext(options);
    }
}
