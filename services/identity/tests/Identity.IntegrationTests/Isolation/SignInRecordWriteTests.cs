using Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.IntegrationTests.Isolation;

/// <summary>
/// Tests the data store layer alone on OpenIddict's tables: raw SQL bypasses the EF Core filters
/// and the write guard, so only RLS and the composite foreign keys stand in the way.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SignInRecordWriteTests(PostgresFixture database)
{
    private static readonly HashSet<string> Identifiers =
    [
        "openiddict_applications", "openiddict_authorizations", "openiddict_tokens", "application_id", "authorization_id",
    ];

    [Theory]
    [InlineData("openiddict_applications")]
    [InlineData("openiddict_authorizations")]
    [InlineData("openiddict_tokens")]
    public async Task Insert_WhenRowBelongsToAnotherTenant_ShouldBeRejected(string table)
    {
        // Arrange
        await using var context = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantA);

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(context, table, Guid.NewGuid(), TestDb.TenantB));

        // Assert
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Theory]
    [InlineData("openiddict_applications")]
    [InlineData("openiddict_authorizations")]
    [InlineData("openiddict_tokens")]
    public async Task UpdateAndDelete_WhenRowBelongsToAnotherTenant_ShouldAffectNothing(string table)
    {
        // Arrange
        var id = Guid.NewGuid();
        await using (var tenantB = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantB))
        {
            await InsertAsync(tenantB, table, id, TestDb.TenantB);
        }

        await using var tenantA = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantA);

        // Act
        var updated = await tenantA.Database.ExecuteSqlRawAsync(
            Sql("UPDATE {table} SET concurrency_token = 'changed' WHERE id = {0}", table), id);
        var deleted = await tenantA.Database.ExecuteSqlRawAsync(Sql("DELETE FROM {table} WHERE id = {0}", table), id);

        // Assert
        Assert.Equal(0, updated);
        Assert.Equal(0, deleted);

        await using var owner = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantB);
        var token = await owner.Database
            .SqlQueryRaw<string?>(Sql("SELECT concurrency_token AS \"Value\" FROM {table} WHERE id = {0}", table), id)
            .SingleAsync();
        Assert.Null(token);
    }

    [Theory]
    [InlineData("openiddict_authorizations", "application_id", "openiddict_applications")]
    [InlineData("openiddict_tokens", "application_id", "openiddict_applications")]
    [InlineData("openiddict_tokens", "authorization_id", "openiddict_authorizations")]
    public async Task Reference_WhenParentBelongsToAnotherTenant_ShouldBeRejected(string table, string column, string parentTable)
    {
        // Arrange
        var parentId = Guid.NewGuid();
        await using (var tenantB = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantB))
        {
            await InsertAsync(tenantB, parentTable, parentId, TestDb.TenantB);
        }

        await using var tenantA = TestDb.CreateContext(database.AppConnectionString, TestDb.TenantA);

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => tenantA.Database.ExecuteSqlRawAsync(
            Sql("INSERT INTO {table} (id, tenant_id, {column}) VALUES ({0}, {1}, {2})", table, column),
            Guid.NewGuid(), TestDb.TenantA, parentId));

        // Assert
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    /// <summary>Fills in table and column names, which SQL parameters cannot carry, from a fixed list.</summary>
    private static string Sql(string template, string table, string? column = null)
    {
        Assert.Contains(table, Identifiers);
        Assert.True(column is null || Identifiers.Contains(column));
        return template.Replace("{table}", table).Replace("{column}", column);
    }

    // Applications are inserted without a client id, so the client directory key does not apply.
    private static Task<int> InsertAsync(Identity.Infrastructure.Persistence.IdentityDbContext context, string table, Guid id, Guid tenantId) =>
        context.Database.ExecuteSqlRawAsync(Sql("INSERT INTO {table} (id, tenant_id) VALUES ({0}, {1})", table), id, tenantId);
}
