using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

namespace TimeTracker.Api.IntegrationTests.Assistant;

/// <summary>Ejecuta una sentencia SQL desde otra conexión justo antes de que EF confirme: simula a otro Manager que se adelanta.</summary>
internal sealed class RacingSqlInterceptor(string connectionString, string sql) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return result;
    }
}
