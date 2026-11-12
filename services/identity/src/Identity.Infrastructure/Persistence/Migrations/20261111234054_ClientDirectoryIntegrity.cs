using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientDirectoryIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An application can only exist in the tenant its directory entry names. The entry comes first,
            // and the runtime role can never update or delete it, so the mapping cannot drift.
            // Foreign key checks bypass RLS, so the check works whatever tenant is set.
            migrationBuilder.Sql("""
                ALTER TABLE client_directory ADD CONSTRAINT uq_client_directory_tenant_client UNIQUE (tenant_id, client_id);
                ALTER TABLE openiddict_applications ADD CONSTRAINT fk_applications_client_directory
                    FOREIGN KEY (tenant_id, client_id) REFERENCES client_directory (tenant_id, client_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE openiddict_applications DROP CONSTRAINT fk_applications_client_directory;
                ALTER TABLE client_directory DROP CONSTRAINT uq_client_directory_tenant_client;
                """);
        }
    }
}
