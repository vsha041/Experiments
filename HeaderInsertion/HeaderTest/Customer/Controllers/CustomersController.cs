using Microsoft.AspNetCore.Mvc;

namespace Customer.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;

    public CustomersController(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(Customer), StatusCodes.Status200OK)]
    public async Task<ActionResult<Customer>> GetCustomers(
        CancellationToken cancellationToken)
    {
        var customers = (await _customerRepository.GetAllAsync(cancellationToken));

        return Ok(customers);
    }
}