using System.Data.Common;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Shared.ResponseHeaders;

namespace Shared.ResponseHeaders.NUnit.Tests;

[TestFixture]
public class StoredProcedureExtensionsTests
{
    [Test]
    public void QuerySingleWithMetadataAsync_NullConnection_Throws()
    {
        DbConnection connection = null!;

        Assert.That(
            async () => await connection.QuerySingleWithMetadataAsync<HeaderMetadata>(
                "sp_test",
                new DefaultHttpContext()),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void QuerySingleWithMetadataAsync_NullStoredProcedure_Throws()
    {
        using var connection = new FakeDbConnection();

        Assert.That(
            async () => await connection.QuerySingleWithMetadataAsync<HeaderMetadata>(
                null!,
                new DefaultHttpContext()),
            Throws.TypeOf<ArgumentNullException>());
    }

    [TestCase("")]
    [TestCase(" ")]
    public void QuerySingleWithMetadataAsync_EmptyOrWhitespaceStoredProcedure_Throws(
        string storedProcedure)
    {
        using var connection = new FakeDbConnection();

        Assert.That(
            async () => await connection.QuerySingleWithMetadataAsync<HeaderMetadata>(
                storedProcedure,
                new DefaultHttpContext()),
            Throws.TypeOf<ArgumentException>());
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
