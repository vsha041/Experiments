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
        object? parameters = null,
        CancellationToken cancellationToken = default)
        where TRow : HeaderMetadata
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedProcedure);
        //ArgumentNullException.ThrowIfNull(metadataContext);

        TRow row = await connection.QuerySingleAsync<TRow>(
            new CommandDefinition(
                storedProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        context.Items ??= new Dictionary<object, object?>();
        context.Items.Add("ApiResponseHeaders", new List<TRow> {row});
        return row;
    }

    public static async Task<IReadOnlyList<TRow>> QueryWithMetadataAsync<TRow>(
        this DbConnection connection,
        string storedProcedure,
        HttpContext context,
        object? parameters = null,
        CancellationToken cancellationToken = default)
        where TRow : HeaderMetadata
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedProcedure);

        var rows = (await connection.QueryAsync<TRow>(
            new CommandDefinition(
                storedProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).AsList();

        context.Items ??= new Dictionary<object, object?>();
        context.Items.Add("ApiResponseHeaders", rows);
        return rows;
    }
}
