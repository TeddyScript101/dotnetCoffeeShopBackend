using CoffeeShopApi.Data;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that registers a SQLite in-memory database
/// instead of PostgreSQL. Program.cs skips its own DbContext registration when
/// the environment is "Testing", so this factory is the sole registrant.
/// Each test class gets its own factory instance with a unique database name,
/// preventing data from leaking across test classes.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Unique database name per factory instance ensures isolation between test classes.
    private readonly string _dbName = $"TestDb_{Guid.NewGuid():N}";

    // A keep-alive connection prevents the named in-memory SQLite database from
    // being destroyed when the last DbContext scope closes during a request.
    private SqliteConnection? _keepAliveConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // In-memory overrides — avoids a file-path look-up that would fail when
            // the working directory is the API project rather than the test project.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CoffeeShopApi",
                ["Jwt:Audience"] = "CoffeeShopApiUsers",
                ["Jwt:Key"] = "ThisIsAVerySecretKeyForJwtAuthenticationWhichShouldBeLongEnough",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Open the keep-alive connection so the named in-memory SQLite database
            // persists for the entire lifetime of this factory instance.
            _keepAliveConnection = new SqliteConnection(
                $"DataSource={_dbName};Mode=Memory;Cache=Shared");
            _keepAliveConnection.Open();

            // Program.cs skips AddDbContext when the environment is "Testing", so
            // we register the SQLite context here with no conflict.
            services.AddDbContext<CoffeeShopDbContext>(options =>
                options.UseSqlite(
                    $"DataSource={_dbName};Mode=Memory;Cache=Shared"));

            // Replace the real Stripe service with a fake so tests never hit Stripe's API.
            services.AddScoped<IStripePaymentService, FakeStripePaymentService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection?.Close();
            _keepAliveConnection?.Dispose();
        }
    }
}
