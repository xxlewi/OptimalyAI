using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.ServiceLayer.Services.Projects;
using OptimalyAI.ViewModels;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller for AI Projects management
    /// </summary>
    public class ProjectsController : Controller
    {
        private readonly IProjectService _projectService;
        private readonly IProjectWorkflowService _workflowService;
        private readonly IProjectExecutionService _executionService;
        private readonly IProjectContextService _contextService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(
            IProjectService projectService,
            IProjectWorkflowService workflowService,
            IProjectExecutionService executionService,
            IProjectContextService contextService,
            ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _workflowService = workflowService;
            _executionService = executionService;
            _contextService = contextService;
            _logger = logger;
        }

        /// <summary>
        /// List all projects
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var projectDtos = await _projectService.GetAllAsync();
            
            // Rozdělit projekty na aktivní a archivované
            var activeProjects = projectDtos.Where(p => p.Status != ProjectStatus.Archived);
            var archivedProjects = projectDtos.Where(p => p.Status == ProjectStatus.Archived);
            
            // Konverze na ViewModely pro zobrazení
            var projects = activeProjects.Select(p => new ProjectViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.CustomerRequirement,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt ?? DateTime.UtcNow,
                Schedule = p.ActiveWorkflows > 0 ? "Aktivní" : "Neaktivní",
                LastRun = p.UpdatedAt,
                NextRun = p.StartDate?.AddDays(1),
                Configuration = new ProjectConfiguration
                {
                    Sources = new List<string>(),
                    Keywords = new List<string>(),
                    CrmIntegration = "",
                    NotificationEmail = ""
                },
                Workflow = new List<WorkflowStep>(),
                Metrics = new ProjectMetrics
                {
                    TotalRuns = p.TotalExecutions,
                    SuccessfulRuns = (int)(p.TotalExecutions * (p.SuccessRate ?? 0) / 100),
                    FailedRuns = p.TotalExecutions - (int)(p.TotalExecutions * (p.SuccessRate ?? 0) / 100),
                    ItemsProcessed = 0,
                    ItemsMatched = 0,
                    AverageRunTime = TimeSpan.Zero
                }
            }).ToList();

            // Konverze archivovaných projektů
            var archived = archivedProjects.Select(p => new ProjectViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.CustomerRequirement,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt ?? DateTime.UtcNow,
                Schedule = p.ActiveWorkflows > 0 ? "Aktivní" : "Neaktivní",
                LastRun = p.UpdatedAt,
                NextRun = p.StartDate?.AddDays(1),
                Configuration = new ProjectConfiguration
                {
                    Sources = new List<string>(),
                    Keywords = new List<string>(),
                    CrmIntegration = "",
                    NotificationEmail = ""
                },
                Workflow = new List<WorkflowStep>(),
                Metrics = new ProjectMetrics
                {
                    TotalRuns = p.TotalExecutions,
                    SuccessfulRuns = (int)(p.TotalExecutions * (p.SuccessRate ?? 0) / 100),
                    FailedRuns = p.TotalExecutions - (int)(p.TotalExecutions * (p.SuccessRate ?? 0) / 100),
                    ItemsProcessed = 0,
                    ItemsMatched = 0,
                    AverageRunTime = TimeSpan.Zero
                }
            }).ToList();

            ViewBag.ArchivedProjects = archived;
            return View(projects);
        }

        /// <summary>
        /// Project details
        /// </summary>
        public async Task<IActionResult> Details(Guid id)
        {
            var projectDto = await _projectService.GetByIdAsync(id);
            var workflows = await _workflowService.GetByProjectIdAsync(id);
            var executions = await _executionService.GetByProjectIdAsync(id);

            var project = new ProjectViewModel
            {
                Id = projectDto.Id,
                Name = projectDto.Name,
                Description = projectDto.Description ?? projectDto.CustomerRequirement,
                Status = projectDto.Status,
                CreatedAt = projectDto.CreatedAt,
                UpdatedAt = projectDto.UpdatedAt ?? DateTime.UtcNow,
                Schedule = workflows.FirstOrDefault(w => w.IsActive && !string.IsNullOrEmpty(w.CronExpression))?.CronExpression ?? "Manuální",
                LastRun = executions.FirstOrDefault()?.StartedAt,
                NextRun = projectDto.DueDate,
                Configuration = new ProjectConfiguration
                {
                    Sources = new List<string>(),
                    Keywords = new List<string>(),
                    CrmIntegration = "",
                    NotificationEmail = projectDto.CustomerEmail ?? ""
                },
                Workflow = workflows.Select(w => new WorkflowStep
                {
                    Order = 1,
                    Name = w.Name,
                    Description = w.Description,
                    ToolId = w.WorkflowType,
                    Status = w.IsActive ? "active" : "inactive"
                }).ToList(),
                Metrics = new ProjectMetrics
                {
                    TotalRuns = executions.Count(),
                    SuccessfulRuns = executions.Count(e => e.Status == OAI.Core.Entities.Projects.ExecutionStatus.Completed),
                    FailedRuns = executions.Count(e => e.Status == OAI.Core.Entities.Projects.ExecutionStatus.Failed),
                    ItemsProcessed = executions.Sum(e => e.ItemsProcessedCount),
                    ItemsMatched = 0,
                    AverageRunTime = TimeSpan.FromSeconds(executions.Where(e => e.DurationSeconds.HasValue).Select(e => e.DurationSeconds.Value).DefaultIfEmpty(0).Average())
                }
            };

            return View(project);
        }

        /// <summary>
        /// Create new project
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        /// <summary>
        /// Create new project
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProjectViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var dto = new CreateProjectDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    CustomerName = model.CustomerName ?? "Interní",
                    CustomerEmail = model.CustomerEmail,
                    CustomerRequirement = model.Description ?? model.Name,
                    Status = ProjectStatus.Draft,
                    Priority = ProjectPriority.Medium,
                    ProjectType = "General",
                    EstimatedHours = model.EstimatedHours,
                    HourlyRate = model.HourlyRate
                };

                var project = await _projectService.CreateAsync(dto);
                _logger.LogInformation("Created new project: {Name} with ID: {Id}", model.Name, project.Id);

                TempData["Success"] = "Projekt byl úspěšně vytvořen.";
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                ModelState.AddModelError("", "Chyba při vytváření projektu. Zkuste to prosím znovu.");
                return View(model);
            }
        }

        /// <summary>
        /// Edit project
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var project = await _projectService.GetByIdAsync(id);
            
            var model = new EditProjectViewModel
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description ?? project.CustomerRequirement,
                CustomerName = project.CustomerName,
                CustomerEmail = project.CustomerEmail,
                EstimatedHours = project.EstimatedHours,
                HourlyRate = project.HourlyRate,
                Status = project.Status,
                Priority = project.Priority
            };

            return View(model);
        }

        /// <summary>
        /// Pause project
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Pause(Guid id)
        {
            try
            {
                await _projectService.UpdateStatusAsync(id, ProjectStatus.Paused, "Pozastaveno uživatelem");
                _logger.LogInformation("Paused project: {Id}", id);
                return Json(new { success = true, message = "Projekt byl pozastaven." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing project {Id}", id);
                return Json(new { success = false, message = "Chyba při pozastavování projektu." });
            }
        }

        /// <summary>
        /// Resume project
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Resume(Guid id)
        {
            try
            {
                await _projectService.UpdateStatusAsync(id, ProjectStatus.Active, "Obnoveno uživatelem");
                _logger.LogInformation("Resumed project: {Id}", id);
                return Json(new { success = true, message = "Projekt byl obnoven." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming project {Id}", id);
                return Json(new { success = false, message = "Chyba při obnovování projektu." });
            }
        }

        /// <summary>
        /// Delete project
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _projectService.DeleteAsync(id);
                _logger.LogInformation("Archived project: {Id}", id);
                return Json(new { success = true, message = "Projekt byl archivován." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving project {Id}", id);
                return Json(new { success = false, message = "Chyba při archivování projektu." });
            }
        }

        /// <summary>
        /// View project logs
        /// </summary>
        public async Task<IActionResult> Logs(Guid id)
        {
            var project = await _projectService.GetByIdAsync(id);
            var executions = await _executionService.GetByProjectIdAsync(id);
            var history = await _projectService.GetHistoryAsync(id);

            ViewBag.ProjectName = project.Name;
            ViewBag.ProjectId = id;
            ViewBag.Executions = executions;
            ViewBag.History = history;

            return View();
        }

        /// <summary>
        /// Execute project
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Execute(Guid id)
        {
            try
            {
                var dto = new StartProjectExecutionDto
                {
                    ProjectId = id,
                    InitiatedBy = User.Identity?.Name ?? "System",
                    Parameters = new Dictionary<string, object>()
                };

                var execution = await _executionService.StartExecutionAsync(dto);
                _logger.LogInformation("Started execution {ExecutionId} for project {ProjectId}", execution.Id, id);

                return Json(new { success = true, executionId = execution.Id, message = "Spuštění projektu zahájeno." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing project {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get project context
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Context(Guid id)
        {
            try
            {
                var context = await _contextService.GetProjectContextAsync(id);
                return Content(context, "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project context for {Id}", id);
                return NotFound();
            }
        }
    }

}