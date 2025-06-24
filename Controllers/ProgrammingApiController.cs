using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Programming;
using OAI.ServiceLayer.Services.Programming;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// API Controller pro správu webových aplikací
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ProgrammingApiController : BaseApiController
    {
        private readonly IWebApplicationService _webApplicationService;
        private readonly ILogger<ProgrammingApiController> _logger;

        public ProgrammingApiController(
            IWebApplicationService webApplicationService,
            ILogger<ProgrammingApiController> logger)
        {
            _webApplicationService = webApplicationService ?? throw new ArgumentNullException(nameof(webApplicationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Získá seznam všech webových aplikací
        /// </summary>
        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications()
        {
            try
            {
                var applications = await _webApplicationService.GetAllWebApplicationsAsync();
                return Ok(applications, "Aplikace načteny úspěšně");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading web applications");
                return BadRequest<IEnumerable<WebApplicationDto>>("Chyba při načítání aplikací: " + ex.Message);
            }
        }

        /// <summary>
        /// Získá detail webové aplikace
        /// </summary>
        [HttpGet("applications/{id:guid}")]
        public async Task<IActionResult> GetApplication(Guid id)
        {
            try
            {
                var application = await _webApplicationService.GetWebApplicationByIdAsync(id);
                if (application == null)
                {
                    return NotFound("Aplikace nebyla nalezena");
                }

                return Ok(application, "Aplikace načtena úspěšně");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading web application {Id}", id);
                return BadRequest<WebApplicationDto>("Chyba při načítání aplikace: " + ex.Message);
            }
        }

        /// <summary>
        /// Vytvoří novou webovou aplikaci
        /// </summary>
        [HttpPost("applications")]
        public async Task<IActionResult> CreateApplication([FromBody] CreateWebApplicationDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var application = await _webApplicationService.CreateWebApplicationAsync(model);
                return CreatedAtAction(
                    nameof(GetApplication), 
                    new { id = application.Id }, 
                    ApiResponse<WebApplicationDto>.SuccessResponse(application, $"Aplikace '{application.Name}' byla úspěšně vytvořena"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating web application");
                return BadRequest<WebApplicationDto>("Chyba při vytváření aplikace: " + ex.Message);
            }
        }

        /// <summary>
        /// Aktualizuje webovou aplikaci
        /// </summary>
        [HttpPut("applications/{id:guid}")]
        public async Task<IActionResult> UpdateApplication(Guid id, [FromBody] UpdateWebApplicationDto model)
        {
            if (id != model.Id)
            {
                return BadRequest<WebApplicationDto>("ID v URL neodpovídá ID v datech");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var application = await _webApplicationService.UpdateWebApplicationAsync(id, model);
                if (application == null)
                {
                    return NotFound("Aplikace nebyla nalezena");
                }

                return Ok(application, $"Aplikace '{application.Name}' byla úspěšně aktualizována");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating web application {Id}", id);
                return BadRequest<WebApplicationDto>("Chyba při aktualizaci aplikace: " + ex.Message);
            }
        }

        /// <summary>
        /// Smaže webovou aplikaci
        /// </summary>
        [HttpDelete("applications/{id:guid}")]
        public async Task<IActionResult> DeleteApplication(Guid id)
        {
            try
            {
                var result = await _webApplicationService.DeleteWebApplicationAsync(id);
                if (!result)
                {
                    return NotFound("Aplikace nebyla nalezena");
                }

                return Ok("Aplikace byla úspěšně smazána");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting web application {Id}", id);
                return BadRequest<bool>("Chyba při mazání aplikace: " + ex.Message);
            }
        }

    }
}