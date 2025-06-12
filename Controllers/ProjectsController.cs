using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Tools;
using OptimalyAI.Controllers.Base;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller pro správu projektů a workflow - produkční verze s clean architekturou
    /// </summary>
    public class ProjectsController : BaseApiController
    {
        private readonly IProjectService _projectService;
        private readonly IToolRegistry _toolRegistry;

        public ProjectsController(IProjectService projectService, IToolRegistry toolRegistry)
        {
            _projectService = projectService;
            _toolRegistry = toolRegistry;
        }

        /// <summary>
        /// Zobrazí hlavní stránku s přehledem projektů
        /// </summary>
        /// <returns>View s přehledem projektů</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? status = null, string? workflowType = null, string? search = null)
        {
            // Získat projekty z databáze
            var (projects, totalCount) = await _projectService.GetProjectsAsync(page, pageSize, status, workflowType, search);
            
            // Získat statistiky
            var summary = await _projectService.GetSummaryAsync();
            
            // Získat dostupné nástroje
            var tools = await _toolRegistry.GetAllToolsAsync();
            ViewBag.AvailableTools = tools.Select(t => new { 
                Id = t.Id, 
                Name = t.Name, 
                Category = t.Category,
                Description = t.Description 
            }).ToList();
            
            // Získat workflow typy
            var workflowTypes = await _projectService.GetWorkflowTypesAsync();
            ViewBag.WorkflowTypes = workflowTypes;
            
            // Předat statistiky do ViewBag
            ViewBag.Summary = summary;
            ViewBag.TotalCount = totalCount;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentWorkflowType = workflowType;
            ViewBag.CurrentSearch = search;
            
            return View(projects);
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
                
                return Ok(new {
                    projects,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }, "Projects retrieved successfully");
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
                return Ok(summary, "Summary retrieved successfully");
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
                var executions = await _projectService.GetProjectExecutionsAsync(id, 10);
                var files = await _projectService.GetProjectFilesAsync(id);
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                
                ViewBag.Executions = executions;
                ViewBag.Files = files;
                ViewBag.WorkflowTypes = workflowTypes;
                
                return View(project);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving project: {ex.Message}");
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
            
            return View(new CreateProjectDto());
        }

        /// <summary>
        /// Vytvoření nového projektu
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] CreateProjectDto createDto, string? nextAction = null)
        {
            if (!ModelState.IsValid)
            {
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                ViewBag.WorkflowTypes = workflowTypes;
                return View(createDto);
            }

            try
            {
                var project = await _projectService.CreateProjectAsync(createDto);
                
                // Handle next action based on form selection
                return nextAction switch
                {
                    "designer" => RedirectToAction("Index", "WorkflowDesigner", new { projectId = project.Id }),
                    "list" => RedirectToAction(nameof(Index)),
                    "detail" or _ => RedirectToAction(nameof(Details), new { id = project.Id })
                };
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Chyba při vytváření projektu: {ex.Message}");
                var workflowTypes = await _projectService.GetWorkflowTypesAsync();
                ViewBag.WorkflowTypes = workflowTypes;
                return View(createDto);
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
                return Ok(project, "Project created successfully");
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
                return Ok(project, "Project updated successfully");
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
        /// API endpoint pro smazání projektu
        /// </summary>
        [HttpDelete("api/{id}")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            try
            {
                var deleted = await _projectService.DeleteAsync(id);
                if (!deleted)
                {
                    return NotFound("Project not found");
                }
                
                return Ok("Project deleted successfully");
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
                return Ok(execution, "Workflow execution started");
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
                return Ok(executions, "Executions retrieved successfully");
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
                return Ok(project, "Workflow updated successfully");
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
                return Ok(project, "Orchestrator settings updated successfully");
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
                return Ok(project, "I/O configuration updated successfully");
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
                return Ok(new { isValid, errors }, isValid ? "Workflow is valid" : "Workflow validation failed");
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
                return Ok(workflowTypes, "Workflow types retrieved successfully");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving workflow types: {ex.Message}");
            }
        }
    }
}