using Identity.IntegrationTests.Infrastructure;
using Npgsql;

namespace Identity.IntegrationTests.Isolation;

[Collection(PostgresCollection.Name)]
public sealed class RlsCatalogTests(PostgresFixture database)
{
    // Holds only public values and must be readable before a tenant is known (D-02).
    private static readonly string[] AllowedWithoutRls = ["client_directory"];

    private static readonly string[] TenantOwnedTables =
        ["local_users", "openiddict_applications", "openiddict_authorizations", "openiddict_tokens"];

    private const string TenantExpression = "(tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)";

    [Fact]
    public async Task Catalog_WhenInspected_ShouldHaveRlsAndForceOnEveryTenantOwnedTable()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync();
        const string sql = """
            SELECT c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
            WHERE n.nspname = 'public'
              AND c.relkind = 'r'
              AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity
                   OR NOT EXISTS (SELECT 1 FROM pg_policy p WHERE p.polrelid = c.oid AND p.polname = 'tenant_isolation'))
            """;

        // Act
        var unprotected = new List<string>();
        await using (var command = new NpgsqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                unprotected.Add(reader.GetString(0));
            }
        }

        // Assert
        Assert.Empty(unprotected.Except(AllowedWithoutRls));
    }

    [Fact]
    public async Task Catalog_WhenInspected_ShouldHaveOnlyTheTenantPolicy()
    {
        // Arrange: permissive policies are combined with OR, so any extra or weaker policy would open the table.
        await using var connection = new NpgsqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync();
        const string sql = """
            SELECT c.relname, p.polname, p.polpermissive, p.polcmd::text,
                   pg_get_expr(p.polqual, p.polrelid), pg_get_expr(p.polwithcheck, p.polrelid)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_policy p ON p.polrelid = c.oid
            WHERE n.nspname = 'public'
            ORDER BY c.relname, p.polname
            """;

        // Act
        var policies = new List<(string Table, string Name, bool Permissive, string Command, string Using, string Check)>();
        await using (var command = new NpgsqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                policies.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
            }
        }

        // Assert
        Assert.Equal(TenantOwnedTables, policies.Select(policy => policy.Table));
        Assert.All(policies, policy =>
        {
            Assert.Equal("tenant_isolation", policy.Name);
            Assert.True(policy.Permissive);
            Assert.Equal("*", policy.Command);
            Assert.Equal(TenantExpression, policy.Using);
            Assert.Equal(TenantExpression, policy.Check);
        });
    }

    [Theory]
    [InlineData("local_users")]
    [InlineData("openiddict_applications")]
    [InlineData("openiddict_authorizations")]
    [InlineData("openiddict_tokens")]
    public async Task Catalog_WhenInspected_ShouldProtectExpectedTable(string table)
    {
        // Arrange
        await using var connection = new NpgsqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE relname = @table AND relkind = 'r'",
            connection);
        command.Parameters.AddWithValue("table", table);

        // Act
        var protectedTable = (bool?)await command.ExecuteScalarAsync();

        // Assert
        Assert.True(protectedTable, $"{table} is missing or has no enforced RLS.");
    }
}
