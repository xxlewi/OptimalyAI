using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs.Customers;
using OAI.ServiceLayer.Services.Customers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Simple JSON controller for customer operations
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersJsonController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersJsonController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Get all customers for select dropdown
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var customers = await _customerService.GetAllAsync();
                var result = customers.Select(c => new 
                {
                    id = c.Id,
                    name = c.Name,
                    companyName = c.CompanyName,
                    email = c.Email,
                    phone = c.Phone
                });
                
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Nepodařilo se načíst zákazníky: " + ex.Message });
            }
        }

        /// <summary>
        /// Create new customer
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateCustomerDto dto)
        {
            try
            {
                var customer = await _customerService.CreateAsync(dto);
                return Ok(new { 
                    success = true, 
                    data = new {
                        id = customer.Id,
                        name = customer.Name,
                        companyName = customer.CompanyName,
                        email = customer.Email,
                        phone = customer.Phone
                    },
                    message = "Zákazník byl vytvořen"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Nepodařilo se vytvořit zákazníka: " + ex.Message });
            }
        }
    }
}