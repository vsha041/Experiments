namespace Customer
{
    public interface ICustomerRepository
    {
        Task<CustomerViewModel> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
