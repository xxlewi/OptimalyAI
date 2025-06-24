using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Programming;
using OAI.ServiceLayer.Services.Programming;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// MVC Controller pro správu webových aplikací
    /// </summary>
    [Route("Programming")]
    public class ProgrammingController : Controller
    {
        private readonly IWebApplicationService _webApplicationService;
        private readonly ILogger<ProgrammingController> _logger;

        public ProgrammingController(
            IWebApplicationService webApplicationService,
            ILogger<ProgrammingController> logger)
        {
            _webApplicationService = webApplicationService ?? throw new ArgumentNullException(nameof(webApplicationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Seznam všech webových aplikací
        /// </summary>
        [HttpGet("Applications")]
        public async Task<IActionResult> Applications()
        {
            try
            {
                var applications = await _webApplicationService.GetAllWebApplicationsAsync();
                return View(applications.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading web applications");
                TempData["Error"] = "Chyba při načítání aplikací: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// Detail webové aplikace
        /// </summary>
        [HttpGet("Applications/{id:guid}")]
        public async Task<IActionResult> ApplicationDetail(Guid id)
        {
            try
            {
                var application = await _webApplicationService.GetWebApplicationByIdAsync(id);
                if (application == null)
                {
                    return NotFound("Aplikace nebyla nalezena");
                }

                return View(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading web application {Id}", id);
                TempData["Error"] = "Chyba při načítání aplikace: " + ex.Message;
                return RedirectToAction(nameof(Applications));
            }
        }

        /// <summary>
        /// Formulář pro vytvoření nové aplikace
        /// </summary>
        [HttpGet("Applications/Create")]
        public IActionResult CreateApplication()
        {
            var model = new CreateWebApplicationDto
            {
                Version = "1.0.0",
                Status = "Development",
                Priority = "Medium",
                IsActive = true
            };
            
            return View(model);
        }

        /// <summary>
        /// Zpracování vytvoření nové aplikace
        /// </summary>
        [HttpPost("Applications/Create")]
        public async Task<IActionResult> CreateApplication(CreateWebApplicationDto model)
        {
            _logger.LogInformation("CreateApplication POST called with model: {Name}, {ProjectPath}", model?.Name, model?.ProjectPath);
            
            // Check if request is AJAX
            bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid. Errors: {Errors}", 
                    string.Join(", ", ModelState.SelectMany(x => x.Value.Errors).Select(e => e.ErrorMessage)));
                
                if (isAjaxRequest)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                        );
                    
                    return BadRequest(new { message = "Validace selhala", errors });
                }
                
                return View(model);
            }

            try
            {
                _logger.LogInformation("Processing application creation for: {Name}", model.Name);
                
                // Konvertovat LastDeployment na UTC pokud má hodnotu
                if (model.LastDeployment.HasValue)
                {
                    // Check if date is too old (before 1970) - PostgreSQL might have issues
                    if (model.LastDeployment.Value.Year < 1970)
                    {
                        _logger.LogWarning("LastDeployment date is too old: {Date}. Setting to null.", model.LastDeployment.Value);
                        model.LastDeployment = null;
                    }
                    else if (model.LastDeployment.Value.Kind == DateTimeKind.Unspecified)
                    {
                        _logger.LogDebug("Converting LastDeployment to UTC: {Date}", model.LastDeployment.Value);
                        model.LastDeployment = DateTime.SpecifyKind(model.LastDeployment.Value, DateTimeKind.Utc);
                    }
                }
                
                var result = await _webApplicationService.CreateWebApplicationAsync(model);
                TempData["Success"] = $"Aplikace '{result.Name}' byla úspěšně vytvořena";
                _logger.LogInformation("Application created successfully with ID: {Id}", result.Id);
                
                if (isAjaxRequest)
                {
                    return Ok(new { success = true, redirectUrl = Url.Action("ApplicationDetail", new { id = result.Id }) });
                }
                
                return RedirectToAction("ApplicationDetail", new { id = result.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating web application. Model: {@Model}", model);
                
                string errorMessage = "Chyba při vytváření aplikace: " + ex.Message;
                
                // Check for specific PostgreSQL datetime errors
                if (ex.Message.Contains("timestamp") || ex.InnerException?.Message.Contains("timestamp") == true)
                {
                    errorMessage = "Chyba s datem: Zadané datum je neplatné nebo příliš staré. Zkuste zadat novější datum nebo pole nechte prázdné.";
                }
                
                if (isAjaxRequest)
                {
                    return StatusCode(500, new { message = errorMessage });
                }
                
                ModelState.AddModelError("", errorMessage);
                return View(model);
            }
        }

        /// <summary>
        /// Formulář pro úpravu aplikace
        /// </summary>
        [HttpGet("Applications/{id:guid}/Edit")]
        public async Task<IActionResult> EditApplication(Guid id)
        {
            try
            {
                var application = await _webApplicationService.GetWebApplicationByIdAsync(id);
                if (application == null)
                {
                    return NotFound("Aplikace nebyla nalezena");
                }

                var model = new UpdateWebApplicationDto
                {
                    Id = application.Id,
                    Name = application.Name,
                    Description = application.Description,
                    ProjectPath = application.ProjectPath,
                    Url = application.Url,
                    ProgrammingLanguage = application.ProgrammingLanguage,
                    Framework = application.Framework,
                    Architecture = application.Architecture,
                    Database = application.Database,
                    Version = application.Version,
                    Status = application.Status,
                    GitRepository = application.GitRepository,
                    Notes = application.Notes,
                    Tags = application.Tags,
                    LastDeployment = application.LastDeployment,
                    IsActive = application.IsActive,
                    Priority = application.Priority
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading web application for edit {Id}", id);
                TempData["Error"] = "Chyba při načítání aplikace: " + ex.Message;
                return RedirectToAction(nameof(Applications));
            }
        }

        /// <summary>
        /// Zpracování úpravy aplikace
        /// </summary>
        [HttpPost("Applications/{id:guid}/Edit")]
        public async Task<IActionResult> EditApplication(Guid id, UpdateWebApplicationDto model)
        {
            if (id != model.Id)
            {
                return BadRequest("ID aplikace se neshoduje");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Konvertovat LastDeployment na UTC pokud má hodnotu
                if (model.LastDeployment.HasValue && model.LastDeployment.Value.Kind == DateTimeKind.Unspecified)
                {
                    model.LastDeployment = DateTime.SpecifyKind(model.LastDeployment.Value, DateTimeKind.Utc);
                }
                
                var result = await _webApplicationService.UpdateWebApplicationAsync(id, model);
                TempData["Success"] = $"Aplikace '{result.Name}' byla úspěšně aktualizována";
                return RedirectToAction("ApplicationDetail", new { id = result.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating web application {Id}", id);
                ModelState.AddModelError("", "Chyba při aktualizaci aplikace: " + ex.Message);
                return View(model);
            }
        }

        /// <summary>
        /// Smazání aplikace
        /// </summary>
        [HttpPost("Applications/{id:guid}/Delete")]
        public async Task<IActionResult> DeleteApplication(Guid id)
        {
            try
            {
                var application = await _webApplicationService.GetWebApplicationByIdAsync(id);
                if (application == null)
                {
                    return NotFound("Aplikace nebyla nalezena");
                }

                var success = await _webApplicationService.DeleteWebApplicationAsync(id);
                if (success)
                {
                    TempData["Success"] = $"Aplikace '{application.Name}' byla úspěšně smazána";
                }
                else
                {
                    TempData["Error"] = "Aplikaci se nepodařilo smazat";
                }

                return RedirectToAction(nameof(Applications));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting web application {Id}", id);
                TempData["Error"] = "Chyba při mazání aplikace: " + ex.Message;
                return RedirectToAction(nameof(Applications));
            }
        }

        /// <summary>
        /// Vyhledávání aplikací
        /// </summary>
        [HttpGet("Applications/Search")]
        public async Task<IActionResult> SearchApplications(string searchTerm = "")
        {
            try
            {
                var applications = await _webApplicationService.SearchWebApplicationsAsync(searchTerm);
                ViewBag.SearchTerm = searchTerm;
                return View("Applications", applications.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching web applications with term: {SearchTerm}", searchTerm);
                TempData["Error"] = "Chyba při vyhledávání: " + ex.Message;
                return RedirectToAction(nameof(Applications));
            }
        }
    }
}