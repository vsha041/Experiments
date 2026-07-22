using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace Shared.ResponseHeaders;

public static class StoredProcedureExtensions
{
    public static async Task<TRow> QuerySingleWithMetadataAsync<TRow>(
        this DbConnection connection,
        string storedProcedure,
        HttpContext context,
        //IDatabaseResponseMetadataContext metadataContext,
        object? parameters = null,
        CancellationToken cancellationToken = default)
        where TRow : HeaderMetadata
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedProcedure);
        //ArgumentNullException.ThrowIfNull(metadataContext);

        var row = await connection.QuerySingleAsync<TRow>(
            new CommandDefinition(
                storedProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        context.Items ??= new Dictionary<object, object?>();
        context.Items.Add("ApiResponseHeaders", row);
        //metadataContext.Capture(row);
        return row;
    }
}
