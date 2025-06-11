using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.ServiceLayer.Services.Projects;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// MVC controller pro workflow designer UI
    /// </summary>
    public class ProjectWorkflowsController : Controller
    {
        private readonly IWorkflowDesignerService _workflowService;
        private readonly IProjectService _projectService;
        private readonly IProjectStageService _stageService;
        private readonly ILogger<ProjectWorkflowsController> _logger;

        public ProjectWorkflowsController(
            IWorkflowDesignerService workflowService,
            IProjectService projectService,
            IProjectStageService stageService,
            ILogger<ProjectWorkflowsController> logger)
        {
            _workflowService = workflowService;
            _projectService = projectService;
            _stageService = stageService;
            _logger = logger;
        }

        /// <summary>
        /// Zobrazí workflow designer pro projekt
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Designer(Guid projectId)
        {
            _logger.LogInformation("Opening workflow designer for project {ProjectId}", projectId);

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

        /// <summary>
        /// Zobrazí seznam workflow šablon
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Templates()
        {
            _logger.LogInformation("Viewing workflow templates");

            var templates = await _workflowService.GetWorkflowTemplatesAsync();
            return View(templates);
        }

        /// <summary>
        /// Zobrazí detail workflow šablony
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TemplateDetails(Guid templateId)
        {
            _logger.LogInformation("Viewing workflow template {TemplateId}", templateId);

            var template = await _projectService.GetByIdAsync(templateId);
            if (template == null || !template.IsTemplate)
            {
                return NotFound();
            }

            var design = await _workflowService.GetWorkflowDesignAsync(templateId);
            
            ViewBag.Template = template;
            return View(design);
        }

        /// <summary>
        /// Zobrazí stránku pro vytvoření projektu ze šablony
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CreateFromTemplate(Guid templateId)
        {
            _logger.LogInformation("Creating project from template {TemplateId}", templateId);

            var template = await _projectService.GetByIdAsync(templateId);
            if (template == null || !template.IsTemplate)
            {
                return NotFound();
            }

            ViewBag.Template = template;
            return View();
        }

        /// <summary>
        /// Zobrazí testovací rozhraní pro workflow
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Test(Guid projectId)
        {
            _logger.LogInformation("Opening workflow test interface for project {ProjectId}", projectId);

            var project = await _projectService.GetByIdAsync(projectId);
            if (project == null)
            {
                return NotFound();
            }

            var design = await _workflowService.GetWorkflowDesignAsync(projectId);
            
            ViewBag.Project = project;
            return View(design);
        }

        /// <summary>
        /// Zobrazí výsledky testů workflow
        /// </summary>
        [HttpGet]
        public IActionResult TestResults(Guid executionId)
        {
            _logger.LogInformation("Viewing test results for execution {ExecutionId}", executionId);

            // TODO: Implement test results retrieval
            ViewBag.ExecutionId = executionId;
            return View();
        }

        /// <summary>
        /// Zobrazí editor pro jednotlivou stage
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditStage(Guid stageId)
        {
            _logger.LogInformation("Editing stage {StageId}", stageId);

            var stage = await _stageService.GetStageAsync(stageId);
            if (stage == null)
            {
                return NotFound();
            }

            var components = await _workflowService.GetAvailableComponentsAsync();
            
            ViewBag.Components = components;
            return View(stage);
        }

        /// <summary>
        /// Zobrazí monitor běžících workflow
        /// </summary>
        [HttpGet]
        public IActionResult Monitor()
        {
            _logger.LogInformation("Opening workflow monitor");
            
            // Get executionId or projectId from query string
            var executionId = Request.Query["executionId"].ToString();
            var projectId = Request.Query["projectId"].ToString();
            
            ViewBag.ExecutionId = executionId;
            ViewBag.ProjectId = projectId;
            
            return View();
        }
    }
}