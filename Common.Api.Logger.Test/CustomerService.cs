namespace Common.Api.Logger.Test;

public sealed class CustomerService
{
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(ILogger<CustomerService> logger)
    {
        _logger = logger;
    }

    public async Task<CustomerResponse> GetCustomerAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting customer lookup for a customer");

        // Simulates an asynchronous database or downstream API operation.
        await Task.Delay(
            TimeSpan.FromMilliseconds(100),
            cancellationToken);

        //_logger.LogInformation(
        //    "Completed customer lookup for {CustomerId}",
        //    customerId);

        return new CustomerResponse(
            customerId,
            "Sample Customer");
    }
}

public sealed record CustomerResponse(
    string CustomerId,
    string DisplayName);