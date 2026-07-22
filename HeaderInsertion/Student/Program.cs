using static Student.StudentRepository;
using Shared.ResponseHeaders;

namespace Student
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddDatabaseResponseMetadata();
            builder.Services.AddScoped<IStudentRepository, StudentRepository>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseDatabaseResponseMetadata();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
