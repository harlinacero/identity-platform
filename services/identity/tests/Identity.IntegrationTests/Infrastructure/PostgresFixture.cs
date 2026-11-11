using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Real PostgreSQL with the same roles script as Compose. The schema is migrated as
/// identity_migrator, and tests connect as identity_app, the role the service uses.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Database = "identity";
    private const string MigratorPassword = "test-migrator";
    private const string AppPassword = "test-app";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase(Database)
        .WithEnvironment("IDENTITY_MIGRATOR_PASSWORD", MigratorPassword)
        .WithEnvironment("IDENTITY_APP_PASSWORD", AppPassword)
        .WithResourceMapping(new FileInfo(FindSeedFile("01-create-databases.sql")), "/docker-entrypoint-initdb.d/")
        .WithResourceMapping(new FileInfo(FindSeedFile("02-identity-roles.sh")), "/docker-entrypoint-initdb.d/")
        .Build();

    public string AppConnectionString => ConnectionStringFor("identity_app", AppPassword);

    public string MigratorConnectionString => ConnectionStringFor("identity_migrator", MigratorPassword);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(MigratorConnectionString)
            .Options;
        await using var context = new IdentityDbContext(options, new Application.Tenancy.TenantContext());
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public Task StopDatabaseAsync() => _container.StopAsync();

    public Task StartDatabaseAsync() => _container.StartAsync();

    public string AppConnectionStringWith(Action<NpgsqlConnectionStringBuilder> configure)
    {
        var builder = new NpgsqlConnectionStringBuilder(AppConnectionString);
        configure(builder);
        return builder.ConnectionString;
    }

    private string ConnectionStringFor(string username, string password) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = username,
            Password = password,
        }.ConnectionString;

    private static string FindSeedFile(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "deploy", "compose", "seed", "postgres", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find {name} above the test output directory.");
    }
}
