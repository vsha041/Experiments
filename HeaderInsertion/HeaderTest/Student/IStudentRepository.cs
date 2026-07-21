using Microsoft.Data.SqlClient;

namespace Student;

public interface IStudentRepository
{
    Task<StudentViewModel> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class StudentRepository : IStudentRepository
{
    private readonly string _connectionString;

    public StudentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CustomerDatabase")
                            ?? throw new InvalidOperationException(
                                "Connection string 'CustomerDatabase' was not found.");
    }

    public async Task<StudentViewModel> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           select top 1 * from student;
                           """;

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(sql, connection);

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("Id");
        var fullNameOrdinal = reader.GetOrdinal("FullName");
        var correlationIdOrdinal = reader.GetOrdinal("CorrelationId"); // this needs to go to response header but not the API caller
        var dateTimeOrdinal = reader.GetOrdinal("DateTime"); // this needs to go to response header but not the API caller
        var addressOrdinal = reader.GetOrdinal("Address");

        while (await reader.ReadAsync(cancellationToken))
        {
            return new StudentViewModel
            {
                Id = reader.GetInt32(idOrdinal),
                FullName = reader.GetString(fullNameOrdinal),
                Address = reader.GetString(addressOrdinal)
            };
        }

        return null;
    }
}