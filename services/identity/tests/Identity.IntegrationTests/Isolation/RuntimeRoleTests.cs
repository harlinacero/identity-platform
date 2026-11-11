using Identity.IntegrationTests.Infrastructure;
using Npgsql;

namespace Identity.IntegrationTests.Isolation;

[Collection(PostgresCollection.Name)]
public sealed class RuntimeRoleTests(PostgresFixture database)
{
    [Fact]
    public async Task RuntimeRole_WhenInspected_ShouldNotBypassRlsOrOwnTables()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();

        // Act
        await using var roleCommand = new NpgsqlCommand(
            "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);
        await using var reader = await roleCommand.ExecuteReaderAsync();
        await reader.ReadAsync();
        var isSuperuser = reader.GetBoolean(0);
        var bypassesRls = reader.GetBoolean(1);
        await reader.CloseAsync();

        await using var ownedCommand = new NpgsqlCommand(
            "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tableowner = current_user", connection);
        var ownedTables = (long)(await ownedCommand.ExecuteScalarAsync())!;

        await using var ddlCommand = new NpgsqlCommand("CREATE TABLE not_allowed (id int)", connection);
        var ddlError = await Assert.ThrowsAsync<PostgresException>(() => ddlCommand.ExecuteNonQueryAsync());

        // Assert
        Assert.False(isSuperuser);
        Assert.False(bypassesRls);
        Assert.Equal(0, ownedTables);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ddlError.SqlState);
    }

    [Fact]
    public async Task RuntimeRole_WhenConnectingToAnotherServiceDatabase_ShouldBeRejected()
    {
        // Arrange
        var connectionString = database.AppConnectionStringWith(builder => builder.Database = "tenants_users");
        await using var connection = new NpgsqlConnection(connectionString);

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => connection.OpenAsync());

        // Assert
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }
}
