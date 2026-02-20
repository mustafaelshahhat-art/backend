using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Respawn;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace IntegrationTests.Base;

public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:latest")
        .Build();

    private Respawner? _respawner;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "SuperSecretTestKeyForIntegrationTestsThatIsLongEnough123!",
                ["JwtSettings:Issuer"] = "KoraZone365Api",
                ["JwtSettings:Audience"] = "KoraZone365App",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
                ["AdminSettings:Password"] = "TestAdmin123!"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real DB
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));

            // Add Testcontainers DB
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(_dbContainer.GetConnectionString()));

            // Remove real Redis IConnectionMultiplexer
            services.RemoveAll(typeof(StackExchange.Redis.IConnectionMultiplexer));
            
            // Add Testcontainers Redis
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
                StackExchange.Redis.ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString()));
        });
    }

    /// <summary>
    /// Resets the database to a clean state (preserving schema), re-seeding only the admin user.
    /// Call at the start of tests that need a fresh DB.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = new[] { "dbo" },
            TablesToIgnore = new Respawn.Graph.Table[] { "__EFMigrationsHistory" }
        });

        await _respawner.ResetAsync(connection);
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _redisContainer.StopAsync();
    }
}

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFactory>
{
}
