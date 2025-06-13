using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Customers;
using OAI.ServiceLayer.Services.Customers;
using System;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// API Controller pro správu zákaznických požadavků (ticketů)
    /// </summary>
    [Route("api/customer-requests")]
    [ApiController]
    public class CustomerRequestsController : BaseApiController
    {
        private readonly ICustomerService _customerService;

        public CustomerRequestsController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Konvertuje zákaznický požadavek na projekt
        /// </summary>
        /// <param name="requestId">ID požadavku</param>
        /// <returns>Vytvořený projekt</returns>
        /// <response code="200">Projekt byl úspěšně vytvořen</response>
        /// <response code="400">Požadavek není ve správném stavu nebo již byl konvertován</response>
        /// <response code="404">Požadavek nebyl nalezen</response>
        [HttpPost("{requestId}/convert-to-project")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> ConvertToProject(Guid requestId)
        {
            var project = await _customerService.ConvertRequestToProjectAsync(requestId);
            return Ok(project, $"Požadavek byl úspěšně konvertován na projekt '{project.Name}'");
        }
    }
}