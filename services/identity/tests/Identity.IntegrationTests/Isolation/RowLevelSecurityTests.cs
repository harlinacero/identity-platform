using Identity.Domain.Users;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.IntegrationTests.Isolation;

/// <summary>
/// Tests the data store layer alone: queries leave out the EF Core filter or use raw SQL,
/// and the tests run on data they create under unique usernames.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RowLevelSecurityTests(PostgresFixture database) : IAsyncLifetime
{
    private readonly string _username = $"rls-{Guid.NewGuid():N}";

    public async ValueTask InitializeAsync()
    {
        foreach (var tenant in new[] { TestDb.TenantA, TestDb.TenantB })
        {
            await using var context = TestDb.CreateContext(database.AppConnectionString, tenant);
            context.LocalUsers.Add(LocalUser.Create(_username, "hash"));
            await context.SaveChangesAsync();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Query_WhenFilterIgnored_ShouldReturnOnlyCurrentTenantRows()
    {
        // Arrange
        await using var context = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantA);

        // Act
        var visible = await context.LocalUsers.IgnoreQueryFilters()
            .Where(user => user.Username == _username)
            .ToListAsync();
        var updated = await context.LocalUsers.IgnoreQueryFilters()
            .Where(user => user.Username == _username)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, "changed"));

        // Assert
        Assert.Equal(TestDb.TenantA, Assert.Single(visible).TenantId);
        Assert.Equal(1, updated);

        await using var tenantB = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantB);
        var tenantBUser = await tenantB.LocalUsers.SingleAsync(user => user.Username == _username);
        Assert.Equal("hash", tenantBUser.PasswordHash);
    }

    [Fact]
    public async Task Query_WhenNoTenantEstablished_ShouldReturnNothing()
    {
        // Arrange
        await using var context = TestDb.CreateContext(database.AppConnectionString, tenantId: null);

        // Act
        var count = await context.Database
            .SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM local_users")
            .SingleAsync();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Insert_WhenNoTenantEstablished_ShouldBeRejected()
    {
        // Arrange
        await using var context = TestDb.CreateContext(database.AppConnectionString, tenantId: null);

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(context, TestDb.TenantA));

        // Assert
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Insert_WhenRowBelongsToAnotherTenant_ShouldBeRejected()
    {
        // Arrange
        await using var context = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantA);

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(context, TestDb.TenantB));

        // Assert
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task NextOperation_WhenConnectionIsReused_ShouldNotSeePreviousTenant()
    {
        // Arrange: a dedicated pool of one, so that every operation reuses the same physical connection.
        var connectionString = database.AppConnectionStringWith(builder =>
        {
            builder.MaxPoolSize = 1;
            builder.ApplicationName = $"pool-{Guid.NewGuid():N}";
        });

        int firstBackend;
        await using (var tenantA = TestDb.CreateContext(connectionString, TestDb.TenantA))
        {
            Assert.NotEmpty(await tenantA.LocalUsers.Where(user => user.Username == _username).ToListAsync());
            firstBackend = await tenantA.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        }

        // Act: a plain connection has no interceptor, so only Npgsql's reset can clear the setting.
        await using var plain = new NpgsqlConnection(connectionString);
        await plain.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT pg_backend_pid(), current_setting('app.tenant_id', true), (SELECT count(*) FROM local_users)", plain);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var secondBackend = reader.GetInt32(0);
        var setting = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var visibleRows = reader.GetInt64(2);

        // Assert
        Assert.Equal(firstBackend, secondBackend);
        Assert.Equal(string.Empty, setting);
        Assert.Equal(0, visibleRows);
    }

    private Task<int> InsertRawAsync(Identity.Infrastructure.Persistence.IdentityDbContext context, Guid tenantId)
    {
        var username = $"{_username}-raw";
        return context.Database.ExecuteSqlAsync(
            $"INSERT INTO local_users (id, tenant_id, username, normalized_username, password_hash, created_at) VALUES ({Guid.NewGuid()}, {tenantId}, {username}, {username.ToUpperInvariant()}, 'hash', now())");
    }
}
