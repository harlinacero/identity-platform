namespace Identity.Infrastructure.Persistence.Migrations;

internal static class RowLevelSecuritySql
{
    private const string CurrentTenant = "NULLIF(current_setting('app.tenant_id', true), '')::uuid";

    public static string Enable(string table) => $"""
        ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON {table}
            USING (tenant_id = {CurrentTenant})
            WITH CHECK (tenant_id = {CurrentTenant});
        """;
}
