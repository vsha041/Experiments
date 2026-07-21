namespace Customer
{
    public interface ICustomerRepository
    {
        Task<Customer> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
