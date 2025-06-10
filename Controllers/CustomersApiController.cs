using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Customers;
using OAI.ServiceLayer.Services.Customers;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// API controller for customer management
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersApiController : BaseApiController
    {
        private readonly ICustomerService _customerService;

        public CustomersApiController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Get all customers
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<CustomerDto>>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var customers = await _customerService.GetAllAsync();
            return Ok(customers, "Customers retrieved successfully");
        }

        /// <summary>
        /// Get customer by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<CustomerDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            return Ok(customer, "Customer retrieved successfully");
        }

        /// <summary>
        /// Create new customer
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CustomerDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] CreateCustomerDto createDto)
        {
            var customer = await _customerService.CreateAsync(createDto);
            
            return CreatedAtAction(nameof(GetById), new { id = customer.Id }, 
                ApiResponse<CustomerDto>.SuccessResponse(customer, "Customer created successfully"));
        }

        /// <summary>
        /// Update customer
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<CustomerDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerDto updateDto)
        {
            var customer = await _customerService.UpdateAsync(id, updateDto);
            return Ok(customer, "Customer updated successfully");
        }

        /// <summary>
        /// Delete customer
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _customerService.DeleteAsync(id);
            return NoContent();
        }
    }
}