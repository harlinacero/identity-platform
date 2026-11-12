using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.IntegrationTests.Isolation;

/// <summary>The directory resolves the tenant before sign-in, so the database keeps it immutable and consistent.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ClientDirectoryTests(PostgresFixture database)
{
    [Theory]
    [InlineData("UPDATE client_directory SET tenant_id = @other WHERE client_id = @client")]
    [InlineData("DELETE FROM client_directory WHERE client_id = @client")]
    public async Task ClientDirectory_WhenRuntimeRoleChangesEntry_ShouldBeRejected(string sql)
    {
        // Arrange
        var clientId = $"directory-{Guid.NewGuid():N}";
        await using var context = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantA);
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO client_directory (client_id, tenant_id) VALUES ({clientId}, {TestDb.TenantA})");

        await using var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("client", clientId);
        command.Parameters.AddWithValue("other", TestDb.TenantB);

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        // Assert
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Application_WhenDirectoryNamesAnotherTenant_ShouldBeRejected()
    {
        // Arrange
        var clientId = $"directory-{Guid.NewGuid():N}";
        await using var context = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantA);
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO client_directory (client_id, tenant_id) VALUES ({clientId}, {TestDb.TenantB})");

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlAsync(
            $"INSERT INTO openiddict_applications (id, tenant_id, client_id) VALUES ({Guid.NewGuid()}, {TestDb.TenantA}, {clientId})"));

        // Assert
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task Application_WhenDirectoryNamesSameTenant_ShouldBeAccepted()
    {
        // Arrange
        var clientId = $"directory-{Guid.NewGuid():N}";
        await using var context = TestDb.CreateContext(database.AppConnectionString, SeedData.TenantAId);
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO client_directory (client_id, tenant_id) VALUES ({clientId}, {SeedData.TenantAId})");

        // Act
        var inserted = await context.Database.ExecuteSqlAsync(
            $"INSERT INTO openiddict_applications (id, tenant_id, client_id) VALUES ({Guid.NewGuid()}, {SeedData.TenantAId}, {clientId})");

        // Assert
        Assert.Equal(1, inserted);
    }
}
