using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Customers;
using OAI.Core.Entities.Customers;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Customers;
using OptimalyAI.ViewModels;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller pro správu projektů - produkční verze s clean architekturou
    /// </summary>
    [Route("[controller]")]
    public class ProjectsController : Controller
    {
        private readonly IProjectService _projectService;
        private readonly IToolRegistry _toolRegistry;
        private readonly ILogger<ProjectsController> _logger;
        private readonly ICustomerService _customerService;

        public ProjectsController(
            IProjectService projectService,
            IToolRegistry toolRegistry,
            ICustomerService customerService,
            ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _toolRegistry = toolRegistry;
            _customerService = customerService;
            _logger = logger;
        }

        /// <summary>
        /// Zobrazí hlavní stránku s přehledem projektů
        /// </summary>
        /// <returns>View s přehledem projektů</returns>
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? status = null, string? workflowType = null, string? search = null, int page = 1, int pageSize = 10)
        {
            try
            {
                // Získat projekty z databáze - pro view načíst všechny včetně archivovaných
                var (projects, totalCount) = await _projectService.GetProjectsAsync(page, pageSize, "all", workflowType, search);
                
                // Získat statistiky
                var summary = await _projectService.GetSummaryAsync();
                
                // Získat dostupné nástroje pro filter
                var tools = await _toolRegistry.GetAllToolsAsync();
                var availableTools = tools.Select(t => new { 
                    Id = t.Id, 
                    Name = t.Name, 
                    Category = t.Category,
                    Description = t.Description 
                }).ToList();
                
                // Předat data do ViewBag
                ViewBag.Projects = projects;
                ViewBag.TotalCount = totalCount;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentWorkflowType = workflowType;
                ViewBag.CurrentSearch = search;
                
                // Statistiky
                ViewBag.TotalProjects = summary.TotalProjects;
                ViewBag.ActiveProjects = summary.ActiveProjects;
                ViewBag.DraftProjects = summary.DraftProjects;
                ViewBag.CompletedProjects = summary.CompletedProjects;
                ViewBag.FailedProjects = summary.FailedProjects;
                
                // Dostupné nástroje
                ViewBag.AvailableTools = availableTools;
                
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects list");
                TempData["Error"] = "Nepodařilo se načíst seznam projektů.";
                return View();
            }
        }


        /// <summary>
        /// API endpoint pro získání seznamu projektů s filtrováním
        /// </summary>
        [HttpGet("api")]
        public async Task<IActionResult> GetProjects(string? status = null, string? workflowType = null, string? search = null, int page = 1, int pageSize = 10)
        {
            try
            {
                var (projects, totalCount) = await _projectService.GetProjectsAsync(page, pageSize, status, workflowType, search);
                
                return Json(new {
                    success = true,
                    data = new {
                        projects,
                        totalCount,
                        page,
                        pageSize,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    },
                    message = "Projects retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving projects: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro získání statistik projektů
        /// </summary>
        [HttpGet("api/summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var summary = await _projectService.GetSummaryAsync();
                return Json(new { success = true, data = summary, message = "Summary retrieved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Detail projektu
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var project = await _projectService.GetByIdAsync(id);
                if (project == null)
                {
                    return NotFound();
                }
                
                var executions = await _projectService.GetProjectExecutionsAsync(id, 10);
                var files = await _projectService.GetProjectFilesAsync(id);
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                
                ViewBag.Executions = executions;
                ViewBag.Files = files;
                ViewBag.WorkflowTypes = workflowTypes;
                
                return View(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project details for ID: {Id}", id);
                TempData["Error"] = "Nepodařilo se načíst detail projektu.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Formulář pro vytvoření nového projektu
        /// </summary>
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var workflowTypes = await _projectService.GetWorkflowTypesAsync();
            ViewBag.WorkflowTypes = workflowTypes;
            
            // Load customers for dropdown
            var customers = await _customerService.GetAllListAsync();
            ViewBag.Customers = customers.OrderBy(c => c.Name).ToList();
            
            return View(new CreateProjectDto());
        }

        /// <summary>
        /// Vytvoření nového projektu
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] CreateProjectDto createDto, string? nextAction = null, bool internalProject = false)
        {
            // Handle internal project before validation
            if (internalProject || Request.Form["InternalProject"] == "true")
            {
                createDto.CustomerId = null;
                createDto.CustomerName = "Interní projekt";
                createDto.CustomerEmail = "";
                
                // Remove CustomerName validation error if it exists
                ModelState.Remove("CustomerName");
            }
            
            if (!ModelState.IsValid)
            {
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                ViewBag.WorkflowTypes = workflowTypes;
                
                var customers = await _customerService.GetAllListAsync();
                ViewBag.Customers = customers.OrderBy(c => c.Name).ToList();
                
                return View(createDto);
            }

            try
            {
                // Handle new customer creation - only if NOT internal project
                if (createDto.CustomerId == null && !string.IsNullOrEmpty(createDto.CustomerName) && createDto.CustomerName != "Interní projekt")
                {
                    // Create new customer first
                    var newCustomer = await _customerService.CreateAsync(new OAI.Core.DTOs.Customers.CreateCustomerDto
                    {
                        Name = createDto.CustomerName,
                        Email = createDto.CustomerEmail ?? "",
                        Type = CustomerType.Company
                    });
                    
                    // Assign the new customer ID to the project
                    createDto.CustomerId = newCustomer.Id;
                }
                
                var project = await _projectService.CreateProjectAsync(createDto);
                
                // Always redirect to detail
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Chyba při vytváření projektu: {ex.Message}");
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                ViewBag.WorkflowTypes = workflowTypes;
                
                var customers = await _customerService.GetAllListAsync();
                ViewBag.Customers = customers.OrderBy(c => c.Name).ToList();
                
                return View(createDto);
            }
        }

        /// <summary>
        /// Editace projektu - GET
        /// </summary>
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            try
            {
                var project = await _projectService.GetByIdAsync(id);
                if (project == null)
                {
                    return NotFound();
                }

                var updateDto = new UpdateProjectDto
                {
                    Name = project.Name,
                    Description = project.Description,
                    CustomerId = project.CustomerId,
                    CustomerName = project.CustomerName,
                    CustomerEmail = project.CustomerEmail,
                    Status = project.Status,
                    Priority = project.Priority,
                    WorkflowType = project.WorkflowType
                };

                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                ViewBag.WorkflowTypes = workflowTypes;
                
                var customers = await _customerService.GetAllListAsync();
                ViewBag.Customers = customers.OrderBy(c => c.Name).ToList();

                ViewBag.ProjectId = id;

                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project for edit");
                TempData["Error"] = "Nepodařilo se načíst projekt.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Editace projektu - POST
        /// </summary>
        [HttpPost("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromForm] UpdateProjectDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                ViewBag.WorkflowTypes = workflowTypes;
                
                var customers = await _customerService.GetAllListAsync();
                ViewBag.Customers = customers.OrderBy(c => c.Name).ToList();
                
                ViewBag.ProjectId = id;
                
                return View(updateDto);
            }

            try
            {
                await _projectService.UpdateProjectAsync(id, updateDto);
                TempData["Success"] = "Projekt byl úspěšně aktualizován.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project");
                ModelState.AddModelError("", $"Chyba při aktualizaci projektu: {ex.Message}");
                
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                ViewBag.WorkflowTypes = workflowTypes;
                
                var customers = await _customerService.GetAllListAsync();
                ViewBag.Customers = customers.OrderBy(c => c.Name).ToList();
                
                ViewBag.ProjectId = id;
                
                return View(updateDto);
            }
        }

        /// <summary>
        /// API endpoint pro vytvoření projektu
        /// </summary>
        [HttpPost("api")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var project = await _projectService.CreateProjectAsync(createDto);
                return Json(new { success = true, data = project, message = "Project created successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error creating project: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro aktualizaci projektu
        /// </summary>
        [HttpPut("api/{id}")]
        public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var project = await _projectService.UpdateProjectAsync(id, updateDto);
                return Json(new { success = true, data = project, message = "Project updated successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Project not found");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating project: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro archivaci projektu
        /// </summary>
        [HttpPut("api/{id}/archive")]
        public async Task<IActionResult> ArchiveProject(Guid id)
        {
            try
            {
                var archived = await _projectService.ArchiveProjectAsync(id);
                if (!archived)
                {
                    return NotFound("Project not found");
                }
                
                return Json(new { success = true, message = "Project archived successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error archiving project: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro smazání projektu
        /// </summary>
        [HttpDelete("api/{id}/delete")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            try
            {
                var deleted = await _projectService.DeleteProjectAsync(id);
                if (!deleted)
                {
                    return NotFound("Project not found");
                }
                
                return Json(new { success = true, message = "Project deleted permanently" });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting project: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro spuštění workflow
        /// </summary>
        [HttpPost("api/{id}/execute")]
        public async Task<IActionResult> ExecuteWorkflow(Guid id, [FromBody] CreateProjectExecutionDto executionDto)
        {
            if (id != executionDto.ProjectId)
            {
                return BadRequest("Project ID mismatch");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var execution = await _projectService.ExecuteProjectAsync(executionDto);
                return Json(new { success = true, data = execution, message = "Workflow execution started" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Project not found");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error executing workflow: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro získání historie spuštění
        /// </summary>
        [HttpGet("api/{id}/executions")]
        public async Task<IActionResult> GetExecutions(Guid id, int limit = 10)
        {
            try
            {
                var executions = await _projectService.GetProjectExecutionsAsync(id, limit);
                return Json(new { success = true, data = executions, message = "Executions retrieved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving executions: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro aktualizaci workflow definice
        /// </summary>
        [HttpPut("api/{id}/workflow")]
        public async Task<IActionResult> UpdateWorkflow(Guid id, [FromBody] object workflowDefinition)
        {
            try
            {
                var project = await _projectService.UpdateWorkflowDefinitionAsync(id, workflowDefinition);
                return Json(new { success = true, data = project, message = "Workflow updated successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Project not found");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating workflow: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro aktualizaci orchestrátor nastavení
        /// </summary>
        [HttpPut("api/{id}/orchestrator")]
        public async Task<IActionResult> UpdateOrchestratorSettings(Guid id, [FromBody] object orchestratorSettings)
        {
            try
            {
                var project = await _projectService.UpdateOrchestratorSettingsAsync(id, orchestratorSettings);
                return Json(new { success = true, data = project, message = "Orchestrator settings updated successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Project not found");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating orchestrator settings: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro aktualizaci I/O konfigurace
        /// </summary>
        [HttpPut("api/{id}/io-configuration")]
        public async Task<IActionResult> UpdateIOConfiguration(Guid id, [FromBody] object ioConfiguration)
        {
            try
            {
                var project = await _projectService.UpdateIOConfigurationAsync(id, ioConfiguration);
                return Json(new { success = true, data = project, message = "I/O configuration updated successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Project not found");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating I/O configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro validaci workflow
        /// </summary>
        [HttpPost("api/{id}/validate")]
        public async Task<IActionResult> ValidateWorkflow(Guid id)
        {
            try
            {
                var (isValid, errors) = await _projectService.ValidateWorkflowAsync(id);
                return Json(new { success = true, data = new { isValid, errors }, message = isValid ? "Workflow is valid" : "Workflow validation failed" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Project not found");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error validating workflow: {ex.Message}");
            }
        }

        /// <summary>
        /// API endpoint pro získání workflow typů
        /// </summary>
        [HttpGet("api/workflow-types")]
        public async Task<IActionResult> GetWorkflowTypes()
        {
            try
            {
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                return Json(new { success = true, data = workflowTypes, message = "Workflow types retrieved successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving workflow types: {ex.Message}");
            }
        }
    }
}