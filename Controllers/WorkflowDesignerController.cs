using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Projects;
using OAI.ServiceLayer.Services.Projects;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// API controller pro workflow designer
    /// </summary>
    [Route("api/workflow")]
    [ApiController]
    public class WorkflowDesignerController : BaseApiController
    {
        private readonly IWorkflowDesignerService _workflowService;
        private readonly IProjectStageService _stageService;
        private readonly IWorkflowExecutionService _executionService;
        private readonly ILogger<WorkflowDesignerController> _logger;

        public WorkflowDesignerController(
            IWorkflowDesignerService workflowService,
            IProjectStageService stageService,
            IWorkflowExecutionService executionService,
            ILogger<WorkflowDesignerController> logger)
        {
            _workflowService = workflowService;
            _stageService = stageService;
            _executionService = executionService;
            _logger = logger;
        }

        /// <summary>
        /// Získá workflow design projektu
        /// </summary>
        /// <param name="projectId">ID projektu</param>
        /// <returns>Workflow design</returns>
        [HttpGet("{projectId}/design")]
        [ProducesResponseType(typeof(ApiResponse<ProjectWorkflowDesignDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetWorkflowDesign(Guid projectId)
        {
            var design = await _workflowService.GetWorkflowDesignAsync(projectId);
            return Ok(design, "Workflow design načten");
        }

        /// <summary>
        /// Uloží workflow design
        /// </summary>
        /// <param name="dto">Workflow design data</param>
        /// <returns>Uložený design</returns>
        [HttpPost("design")]
        [ProducesResponseType(typeof(ApiResponse<ProjectWorkflowDesignDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SaveWorkflowDesign([FromBody] SaveProjectWorkflowDto dto)
        {
            var design = await _workflowService.SaveWorkflowDesignAsync(dto);
            return Ok(design, "Workflow design uložen");
        }

        /// <summary>
        /// Testuje workflow
        /// </summary>
        /// <param name="dto">Test parametry</param>
        /// <returns>Výsledek testu</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(ApiResponse<TestWorkflowResultDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> TestWorkflow([FromBody] TestProjectWorkflowDto dto)
        {
            var result = await _workflowService.TestWorkflowAsync(dto);
            return Ok(result, "Test workflow dokončen");
        }

        /// <summary>
        /// Validuje workflow
        /// </summary>
        /// <param name="projectId">ID projektu</param>
        /// <returns>Výsledek validace</returns>
        [HttpGet("{projectId}/validate")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ValidateWorkflow(Guid projectId)
        {
            var isValid = await _workflowService.ValidateWorkflowAsync(projectId);
            return Ok(isValid, isValid ? "Workflow je validní" : "Workflow obsahuje chyby");
        }

        /// <summary>
        /// Získá dostupné komponenty pro workflow
        /// </summary>
        /// <returns>Seznam komponent</returns>
        [HttpGet("components")]
        [ProducesResponseType(typeof(ApiResponse<Dictionary<string, List<string>>>), 200)]
        public async Task<IActionResult> GetAvailableComponents()
        {
            var components = await _workflowService.GetAvailableComponentsAsync();
            return Ok(components, "Komponenty načteny");
        }

        /// <summary>
        /// Získá workflow šablony
        /// </summary>
        /// <returns>Seznam šablon</returns>
        [HttpGet("templates")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectDto>>), 200)]
        public async Task<IActionResult> GetWorkflowTemplates()
        {
            var templates = await _workflowService.GetWorkflowTemplatesAsync();
            return Ok(templates, "Šablony načteny");
        }

        /// <summary>
        /// Převede projekt na šablonu
        /// </summary>
        /// <param name="projectId">ID projektu</param>
        /// <returns>Projekt jako šablona</returns>
        [HttpPost("{projectId}/convert-to-template")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ConvertToTemplate(Guid projectId)
        {
            var template = await _workflowService.ConvertToTemplateAsync(projectId);
            return Ok(template, "Projekt převeden na šablonu");
        }

        /// <summary>
        /// Vytvoří projekt ze šablony
        /// </summary>
        /// <param name="templateId">ID šablony</param>
        /// <param name="dto">Data pro nový projekt</param>
        /// <returns>Nový projekt</returns>
        [HttpPost("templates/{templateId}/create-project")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CreateFromTemplate(Guid templateId, [FromBody] CreateProjectDto dto)
        {
            var project = await _workflowService.CreateFromTemplateAsync(templateId, dto);
            return CreatedAtAction(nameof(GetWorkflowDesign), new { projectId = project.Id }, project);
        }

        // Stage management endpoints

        /// <summary>
        /// Získá stages projektu
        /// </summary>
        /// <param name="projectId">ID projektu</param>
        /// <returns>Seznam stages</returns>
        [HttpGet("{projectId}/stages")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectStageDto>>), 200)]
        public async Task<IActionResult> GetProjectStages(Guid projectId)
        {
            var stages = await _stageService.GetProjectStagesAsync(projectId);
            return Ok(stages, "Stages načteny");
        }

        /// <summary>
        /// Získá detail stage
        /// </summary>
        /// <param name="stageId">ID stage</param>
        /// <returns>Stage detail</returns>
        [HttpGet("stages/{stageId}")]
        [ProducesResponseType(typeof(ApiResponse<ProjectStageDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetStage(Guid stageId)
        {
            var stage = await _stageService.GetStageAsync(stageId);
            return Ok(stage, "Stage načtena");
        }

        /// <summary>
        /// Vytvoří novou stage
        /// </summary>
        /// <param name="dto">Stage data</param>
        /// <returns>Vytvořená stage</returns>
        [HttpPost("stages")]
        [ProducesResponseType(typeof(ApiResponse<ProjectStageDto>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CreateStage([FromBody] CreateProjectStageDto dto)
        {
            var stage = await _stageService.CreateStageAsync(dto);
            return CreatedAtAction(nameof(GetStage), new { stageId = stage.Id }, stage);
        }

        /// <summary>
        /// Aktualizuje stage
        /// </summary>
        /// <param name="stageId">ID stage</param>
        /// <param name="dto">Aktualizovaná data</param>
        /// <returns>Aktualizovaná stage</returns>
        [HttpPut("stages/{stageId}")]
        [ProducesResponseType(typeof(ApiResponse<ProjectStageDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateStage(Guid stageId, [FromBody] UpdateProjectStageDto dto)
        {
            var stage = await _stageService.UpdateStageAsync(stageId, dto);
            return Ok(stage, "Stage aktualizována");
        }

        /// <summary>
        /// Smaže stage
        /// </summary>
        /// <param name="stageId">ID stage</param>
        /// <returns>Potvrzení smazání</returns>
        [HttpDelete("stages/{stageId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteStage(Guid stageId)
        {
            await _stageService.DeleteStageAsync(stageId);
            return NoContent();
        }

        /// <summary>
        /// Přeuspořádá stages
        /// </summary>
        /// <param name="projectId">ID projektu</param>
        /// <param name="stageIds">Seřazená ID stages</param>
        /// <returns>Potvrzení</returns>
        [HttpPost("{projectId}/stages/reorder")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> ReorderStages(Guid projectId, [FromBody] List<Guid> stageIds)
        {
            var result = await _stageService.ReorderStagesAsync(projectId, stageIds);
            return Ok(result, "Stages přeuspořádány");
        }

        /// <summary>
        /// Duplikuje stage
        /// </summary>
        /// <param name="stageId">ID stage k duplikování</param>
        /// <returns>Nová stage</returns>
        [HttpPost("stages/{stageId}/duplicate")]
        [ProducesResponseType(typeof(ApiResponse<ProjectStageDto>), 201)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DuplicateStage(Guid stageId)
        {
            var stage = await _stageService.DuplicateStageAsync(stageId);
            return CreatedAtAction(nameof(GetStage), new { stageId = stage.Id }, stage);
        }

        // Stage tools management

        /// <summary>
        /// Přidá tool do stage
        /// </summary>
        /// <param name="stageId">ID stage</param>
        /// <param name="dto">Tool data</param>
        /// <returns>Potvrzení</returns>
        [HttpPost("stages/{stageId}/tools")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AddToolToStage(Guid stageId, [FromBody] CreateProjectStageToolDto dto)
        {
            var result = await _stageService.AddToolToStageAsync(stageId, dto);
            return Ok(result, "Tool přidán do stage");
        }

        /// <summary>
        /// Odebere tool ze stage
        /// </summary>
        /// <param name="stageId">ID stage</param>
        /// <param name="toolId">ID tool</param>
        /// <returns>Potvrzení</returns>
        [HttpDelete("stages/{stageId}/tools/{toolId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RemoveToolFromStage(Guid stageId, Guid toolId)
        {
            await _stageService.RemoveToolFromStageAsync(stageId, toolId);
            return NoContent();
        }

        /// <summary>
        /// Aktualizuje tool ve stage
        /// </summary>
        /// <param name="stageId">ID stage</param>
        /// <param name="toolId">ID tool</param>
        /// <param name="dto">Aktualizovaná data</param>
        /// <returns>Potvrzení</returns>
        [HttpPut("stages/{stageId}/tools/{toolId}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateStageTool(Guid stageId, Guid toolId, [FromBody] UpdateProjectStageToolDto dto)
        {
            var result = await _stageService.UpdateStageToolAsync(stageId, toolId, dto);
            return Ok(result, "Tool aktualizován");
        }

        /// <summary>
        /// Přeuspořádá tools ve stage
        /// </summary>
        /// <param name="stageId">ID stage</param>
        /// <param name="toolIds">Seřazená ID tools</param>
        /// <returns>Potvrzení</returns>
        [HttpPost("stages/{stageId}/tools/reorder")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> ReorderStageTools(Guid stageId, [FromBody] List<Guid> toolIds)
        {
            var result = await _stageService.ReorderStageToolsAsync(stageId, toolIds);
            return Ok(result, "Tools přeuspořádány");
        }

        // Workflow execution endpoints

        /// <summary>
        /// Spustí workflow projektu
        /// </summary>
        /// <param name="projectId">ID projektu</param>
        /// <param name="dto">Parametry spuštění</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Výsledek spuštění</returns>
        [HttpPost("{projectId}/execute")]
        [ProducesResponseType(typeof(ApiResponse<WorkflowExecutionResultDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ExecuteWorkflow(
            Guid projectId, 
            [FromBody] ExecuteWorkflowDto dto,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing workflow for project {ProjectId}", projectId);
            
            var result = await _executionService.ExecuteWorkflowAsync(
                projectId, 
                dto.Parameters ?? new Dictionary<string, object>(),
                dto.InitiatedBy ?? "api",
                cancellationToken);
                
            return Ok(result, "Workflow spuštěn");
        }

        /// <summary>
        /// Získá status běžícího workflow
        /// </summary>
        /// <param name="executionId">ID spuštění</param>
        /// <returns>Status spuštění</returns>
        [HttpGet("executions/{executionId}/status")]
        [ProducesResponseType(typeof(ApiResponse<WorkflowExecutionStatusDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetExecutionStatus(Guid executionId)
        {
            var status = await _executionService.GetExecutionStatusAsync(executionId);
            return Ok(status, "Status načten");
        }

        /// <summary>
        /// Zruší běžící workflow
        /// </summary>
        /// <param name="executionId">ID spuštění</param>
        /// <returns>Potvrzení zrušení</returns>
        [HttpPost("executions/{executionId}/cancel")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CancelExecution(Guid executionId)
        {
            await _executionService.CancelExecutionAsync(executionId);
            return NoContent();
        }

        /// <summary>
        /// Získá výsledky jednotlivých stages
        /// </summary>
        /// <param name="executionId">ID spuštění</param>
        /// <returns>Výsledky stages</returns>
        [HttpGet("executions/{executionId}/stages")]
        [ProducesResponseType(typeof(ApiResponse<List<WorkflowStageExecutionResultDto>>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetStageResults(Guid executionId)
        {
            var results = await _executionService.GetStageResultsAsync(executionId);
            return Ok(results, "Výsledky stages načteny");
        }
    }

    /// <summary>
    /// DTO pro spuštění workflow
    /// </summary>
    public class ExecuteWorkflowDto
    {
        /// <summary>
        /// Vstupní parametry pro workflow
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }
        
        /// <summary>
        /// Kdo inicioval spuštění
        /// </summary>
        public string InitiatedBy { get; set; }
    }
}