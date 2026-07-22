using Microsoft.Data.SqlClient;

using Shared.ResponseHeaders;

namespace Student;

public interface IStudentRepository
{
    Task<StudentViewModel> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class StudentRepository : IStudentRepository
{
    private readonly string _connectionString;
    private readonly IDatabaseResponseMetadataContext _metadataContext;

    public StudentRepository(
        IConfiguration configuration,
        IDatabaseResponseMetadataContext metadataContext)
    {
        _connectionString = configuration.GetConnectionString("CustomerDatabase")
                            ?? throw new InvalidOperationException(
                                "Connection string 'CustomerDatabase' was not found.");
        _metadataContext = metadataContext;
    }

    public async Task<StudentViewModel> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        var row = await connection.QuerySingleWithMetadataAsync<StudentStoredProcedureRow>(
            "dbo.GetStudent",
            _metadataContext,
            cancellationToken: cancellationToken);

        return new StudentViewModel
        {
            Id = row.Id,
            FullName = row.FullName,
            Address = row.Address
        };
    }

    private sealed class StudentStoredProcedureRow : HeaderMetadata
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }
}
