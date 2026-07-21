using System.Data;
using System.Data.Common;
using Dapper;

namespace Shared.ResponseHeaders;

public static class StoredProcedureExtensions
{
    public static async Task<TRow> QuerySingleWithMetadataAsync<TRow>(
        this DbConnection connection,
        string storedProcedure,
        IDatabaseResponseMetadataContext metadataContext,
        object? parameters = null,
        CancellationToken cancellationToken = default)
        where TRow : HeaderMetadata
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedProcedure);
        ArgumentNullException.ThrowIfNull(metadataContext);

        var row = await connection.QuerySingleAsync<TRow>(
            new CommandDefinition(
                storedProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        metadataContext.Capture(row);
        return row;
    }
}
