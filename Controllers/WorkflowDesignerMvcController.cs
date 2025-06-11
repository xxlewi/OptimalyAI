using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.ServiceLayer.Services.Projects;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// MVC Controller pro workflow designer UI
    /// </summary>
    public class WorkflowDesignerMvcController : Controller
    {
        private readonly IProjectService _projectService;
        private readonly IWorkflowDesignerService _workflowService;
        private readonly IProjectStageService _stageService;
        private readonly ILogger<WorkflowDesignerMvcController> _logger;

        public WorkflowDesignerMvcController(
            IProjectService projectService,
            IWorkflowDesignerService workflowService,
            IProjectStageService stageService,
            ILogger<WorkflowDesignerMvcController> logger)
        {
            _projectService = projectService;
            _workflowService = workflowService;
            _stageService = stageService;
            _logger = logger;
        }

        /// <summary>
        /// Zobrazí workflow designer pro projekt
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(Guid projectId)
        {
            try
            {
                var project = await _projectService.GetByIdAsync(projectId);
                if (project == null)
                {
                    return NotFound();
                }

                var design = await _workflowService.GetWorkflowDesignAsync(projectId);
                var components = await _workflowService.GetAvailableComponentsAsync();

                ViewBag.Project = project;
                ViewBag.Components = components;

                return View(design);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflow designer for project {ProjectId}", projectId);
                return View("Error");
            }
        }

        /// <summary>
        /// Zobrazí detail stage
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> StageDetail(Guid stageId)
        {
            try
            {
                var stage = await _stageService.GetStageAsync(stageId);
                if (stage == null)
                {
                    return NotFound();
                }

                return PartialView("_StageDetail", stage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading stage detail {StageId}", stageId);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Zobrazí formulář pro novou stage
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CreateStage(Guid projectId)
        {
            var components = await _workflowService.GetAvailableComponentsAsync();
            ViewBag.Components = components;
            ViewBag.ProjectId = projectId;

            return PartialView("_CreateStage", new CreateProjectStageDto { ProjectId = projectId });
        }

        /// <summary>
        /// Zobrazí formulář pro editaci stage
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditStage(Guid stageId)
        {
            try
            {
                var stage = await _stageService.GetStageAsync(stageId);
                if (stage == null)
                {
                    return NotFound();
                }

                var components = await _workflowService.GetAvailableComponentsAsync();
                ViewBag.Components = components;

                return PartialView("_EditStage", stage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading stage for edit {StageId}", stageId);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Zobrazí test workflow dialog
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TestWorkflow(Guid projectId)
        {
            var project = await _projectService.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound();
            }

            ViewBag.Project = project;
            return PartialView("_TestWorkflow", new TestProjectWorkflowDto { ProjectId = projectId });
        }

        /// <summary>
        /// Zobrazí seznam šablon
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Templates()
        {
            var templates = await _workflowService.GetWorkflowTemplatesAsync();
            return View(templates);
        }

        /// <summary>
        /// Zobrazí dialog pro vytvoření projektu ze šablony
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CreateFromTemplate(Guid templateId)
        {
            var templates = await _workflowService.GetWorkflowTemplatesAsync();
            var template = templates.FirstOrDefault(t => t.Id == templateId);
            
            if (template == null)
            {
                return NotFound();
            }

            ViewBag.Template = template;
            return PartialView("_CreateFromTemplate", new CreateProjectDto());
        }
    }
}