using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Shared.ResponseHeaders;
using Xunit;

namespace Shared.ResponseHeaders.Tests;

public class StoredProcedureExtensionsTests
{
    [Fact]
    public async Task QuerySingleWithMetadataAsync_NullConnection_Throws()
    {
        DbConnection connection = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connection.QuerySingleWithMetadataAsync<HeaderMetadata>(
                "sp_test",
                new DefaultHttpContext()));
    }

    [Fact]
    public async Task QuerySingleWithMetadataAsync_NullStoredProcedure_Throws()
    {
        using var connection = new FakeDbConnection();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connection.QuerySingleWithMetadataAsync<HeaderMetadata>(
                null!,
                new DefaultHttpContext()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task QuerySingleWithMetadataAsync_EmptyOrWhitespaceStoredProcedure_Throws(
        string storedProcedure)
    {
        using var connection = new FakeDbConnection();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            connection.QuerySingleWithMetadataAsync<HeaderMetadata>(
                storedProcedure,
                new DefaultHttpContext()));
    }

    private sealed class FakeDbConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(
            System.Data.IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
