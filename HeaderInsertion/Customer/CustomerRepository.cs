using Microsoft.Data.SqlClient;

using Shared.ResponseHeaders;

namespace Customer
{
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly IDatabaseResponseMetadataContext _metadataContext;

        public CustomerRepository(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = configuration.GetConnectionString("CustomerDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'CustomerDatabase' was not found.");
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CustomerViewModel> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var row = await connection.QuerySingleWithMetadataAsync<CustomerStoredProcedureRow>(
                "dbo.GetCustomer",
                _httpContextAccessor.HttpContext,
                cancellationToken: cancellationToken);

            return new CustomerViewModel
            {
                Id = row.Id,
                FirstName = row.FirstName,
                LastName = row.LastName
            };
        }

        private sealed class CustomerStoredProcedureRow : HeaderMetadata
        {
            public int Id { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
        }
    }
}
