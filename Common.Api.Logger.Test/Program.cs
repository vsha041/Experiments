using Common.Api.LoggerV2;

namespace Common.Api.Logger.Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // This one registration automatically adds the correlation middleware.
            builder.Services.AddRequestCorrelationLogging();

            builder.Services.AddScoped<CustomerService>();
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            });
            var app = builder.Build();

            app.MapGet(
                "/customers/{customerId}",
                async (
                    string customerId,
                    CustomerService customerService,
                    ILogger<Program> logger,
                    CancellationToken cancellationToken) =>
                {
                    //logger.LogInformation(
                    //    "Received request for customer {CustomerId}",
                    //    customerId);

                    var customer = await customerService.GetCustomerAsync(
                        customerId,
                        cancellationToken);

                    //logger.LogInformation(
                    //    "Returning customer {CustomerId}",
                    //    customerId);

                    return Results.Ok(new
                    {
                        Customer = customer,

                        // Included for demonstration only.
                        // A production endpoint normally wouldn't need this because
                        // the ID is already returned in the response header.
                        //CorrelationId = correlationAccessor.CorrelationId
                    });
                });

            app.MapGet(
                "/test-error",
                (ILogger<Program> logger) =>
                {
                    logger.LogWarning(
                        "The sample error endpoint was requested");

                    throw new InvalidOperationException(
                        "This is a sample exception");
                });

            app.Run();
        }
    }
}
