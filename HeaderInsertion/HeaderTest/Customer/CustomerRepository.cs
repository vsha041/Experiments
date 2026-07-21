using Microsoft.Data.SqlClient;

namespace Customer
{
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly string _connectionString;

        public CustomerRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("CustomerDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'CustomerDatabase' was not found.");
        }

        public async Task<Customer> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            const string sql = """
            SELECT TOP 1
                [Id],
                [FirstName],
                [CorrelationId],
                [DateTime],
                [LastName]
            FROM [dbo].[Customer]
            ORDER BY [Id];
            """;

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);

            await connection.OpenAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var idOrdinal = reader.GetOrdinal("Id");
            var firstNameOrdinal = reader.GetOrdinal("FirstName");
            var correlationIdOrdinal = reader.GetOrdinal("CorrelationId");
            var dateTimeOrdinal = reader.GetOrdinal("DateTime");
            var lastNameOrdinal = reader.GetOrdinal("LastName");

            while (await reader.ReadAsync(cancellationToken))
            {
                return new Customer
                {
                    Id = reader.GetInt32(idOrdinal),
                    FirstName = reader.GetString(firstNameOrdinal),
                    CorrelationId = reader.GetGuid(correlationIdOrdinal),
                    DateTime = reader.GetDateTime(dateTimeOrdinal),
                    LastName = reader.GetString(lastNameOrdinal)
                };
            }

            return null;
        }
    }
}
