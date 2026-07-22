using Shared.ResponseHeaders;

namespace Customer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddDatabaseResponseMetadata();
            builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
            builder.Services.AddHttpContextAccessor();
            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseDatabaseResponseMetadata();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
