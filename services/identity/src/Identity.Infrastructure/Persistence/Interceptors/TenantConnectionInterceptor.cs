using System.Data.Common;
using Identity.Application.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Identity.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Applies the tenant to every connection that opens, for Row-Level Security. Always overwrites
/// the value; Npgsql also resets it when the connection returns to the pool (ADR-0006).
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private const string Sql = "SELECT set_config('app.tenant_id', @tenant, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenantContext.TenantId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);

        return command;
    }
}
