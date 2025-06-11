using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Mapping.Projects;
using OAI.ServiceLayer.Services.Tools;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IWorkflowDesignerService
    {
        Task<ProjectWorkflowDesignDto> GetWorkflowDesignAsync(Guid projectId);
        Task<ProjectWorkflowDesignDto> SaveWorkflowDesignAsync(SaveProjectWorkflowDto dto);
        Task<TestWorkflowResultDto> TestWorkflowAsync(TestProjectWorkflowDto dto);
        Task<ProjectDto> ConvertToTemplateAsync(Guid projectId);
        Task<ProjectDto> CreateFromTemplateAsync(Guid templateId, CreateProjectDto projectDto);
        Task<IEnumerable<ProjectDto>> GetWorkflowTemplatesAsync();
        Task<bool> ValidateWorkflowAsync(Guid projectId);
        Task<Dictionary<string, List<string>>> GetAvailableComponentsAsync();
    }

    public class WorkflowDesignerService : IWorkflowDesignerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectMapper _projectMapper;
        private readonly IProjectStageMapper _stageMapper;
        private readonly IProjectStageService _stageService;
        private readonly IToolRegistry _toolRegistry;
        private readonly ILogger<WorkflowDesignerService> _logger;

        public WorkflowDesignerService(
            IUnitOfWork unitOfWork,
            IProjectMapper projectMapper,
            IProjectStageMapper stageMapper,
            IProjectStageService stageService,
            IToolRegistry toolRegistry,
            ILogger<WorkflowDesignerService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _projectMapper = projectMapper ?? throw new ArgumentNullException(nameof(projectMapper));
            _stageMapper = stageMapper ?? throw new ArgumentNullException(nameof(stageMapper));
            _stageService = stageService ?? throw new ArgumentNullException(nameof(stageService));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProjectWorkflowDesignDto> GetWorkflowDesignAsync(Guid projectId)
        {
            _logger.LogInformation("Getting workflow design for project {ProjectId}", projectId);

            var project = await _unitOfWork.GetGuidRepository<Project>()
                .GetByIdAsync(projectId, q => q.Include(p => p.Stages));

            if (project == null)
            {
                throw new NotFoundException(nameof(Project), projectId);
            }

            var stages = await _stageService.GetProjectStagesAsync(projectId);

            return new ProjectWorkflowDesignDto
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                WorkflowVersion = project.WorkflowVersion,
                TriggerType = project.TriggerType,
                Schedule = project.Schedule,
                Stages = stages.ToList(),
                LastModified = project.UpdatedAt,
                ModifiedBy = "System" // TODO: Add user tracking
            };
        }

        public async Task<ProjectWorkflowDesignDto> SaveWorkflowDesignAsync(SaveProjectWorkflowDto dto)
        {
            _logger.LogInformation("Saving workflow design for project {ProjectId}", dto.ProjectId);

            var project = await _unitOfWork.GetGuidRepository<Project>()
                .GetByIdAsync(dto.ProjectId, q => q.Include(p => p.Stages).ThenInclude(s => s.StageTools));

            if (project == null)
            {
                throw new NotFoundException(nameof(Project), dto.ProjectId);
            }

            // Update project workflow settings
            project.TriggerType = dto.TriggerType;
            project.Schedule = dto.Schedule;
            project.WorkflowVersion++;
            project.UpdatedAt = DateTime.UtcNow;

            // Process stages
            var existingStageIds = project.Stages.Select(s => s.Id).ToList();
            var dtoStageIds = dto.Stages.Where(s => s.Id.HasValue).Select(s => s.Id.Value).ToList();

            // Delete removed stages
            var stagesToDelete = existingStageIds.Except(dtoStageIds).ToList();
            foreach (var stageId in stagesToDelete)
            {
                await _stageService.DeleteStageAsync(stageId);
            }

            // Update or create stages
            var order = 1;
            foreach (var stageDto in dto.Stages.OrderBy(s => s.Order))
            {
                if (stageDto.Id.HasValue && existingStageIds.Contains(stageDto.Id.Value))
                {
                    // Update existing stage
                    var updateDto = new UpdateProjectStageDto
                    {
                        Name = stageDto.Name,
                        Description = stageDto.Description,
                        Type = stageDto.Type,
                        OrchestratorType = stageDto.OrchestratorType,
                        OrchestratorConfiguration = stageDto.OrchestratorConfiguration,
                        ReActAgentType = stageDto.ReActAgentType,
                        ReActAgentConfiguration = stageDto.ReActAgentConfiguration,
                        ExecutionStrategy = stageDto.ExecutionStrategy
                    };
                    await _stageService.UpdateStageAsync(stageDto.Id.Value, updateDto);
                }
                else
                {
                    // Create new stage
                    var createDto = new CreateProjectStageDto
                    {
                        ProjectId = project.Id,
                        Name = stageDto.Name,
                        Description = stageDto.Description,
                        Type = stageDto.Type,
                        OrchestratorType = stageDto.OrchestratorType,
                        OrchestratorConfiguration = stageDto.OrchestratorConfiguration,
                        ReActAgentType = stageDto.ReActAgentType,
                        ReActAgentConfiguration = stageDto.ReActAgentConfiguration,
                        ExecutionStrategy = stageDto.ExecutionStrategy,
                        Tools = stageDto.Tools.Select(t => new CreateProjectStageToolDto
                        {
                            ToolId = t.ToolId,
                            ToolName = t.ToolName,
                            Configuration = t.Configuration,
                            InputMapping = t.InputMapping,
                            OutputMapping = t.OutputMapping,
                            IsRequired = t.IsRequired
                        }).ToList()
                    };
                    await _stageService.CreateStageAsync(createDto);
                }
                order++;
            }

            _unitOfWork.GetGuidRepository<Project>().Update(project);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Saved workflow design for project {ProjectId} with version {Version}", 
                dto.ProjectId, project.WorkflowVersion);

            return await GetWorkflowDesignAsync(dto.ProjectId);
        }

        public async Task<TestWorkflowResultDto> TestWorkflowAsync(TestProjectWorkflowDto dto)
        {
            _logger.LogInformation("Testing workflow for project {ProjectId}", dto.ProjectId);

            var project = await _unitOfWork.GetGuidRepository<Project>()
                .GetByIdAsync(dto.ProjectId, q => q.Include(p => p.Stages));

            if (project == null)
            {
                throw new NotFoundException(nameof(Project), dto.ProjectId);
            }

            // TODO: Implement actual workflow testing with orchestrators
            // For now, return mock result
            var result = new TestWorkflowResultDto
            {
                ExecutionId = Guid.NewGuid(),
                Success = true,
                Duration = TimeSpan.FromSeconds(5),
                StageResults = new List<StageExecutionResultDto>()
            };

            var stages = await _stageService.GetProjectStagesAsync(dto.ProjectId);
            
            foreach (var stage in stages)
            {
                if (dto.StageId.HasValue && stage.Id != dto.StageId.Value)
                    continue;

                var stageResult = new StageExecutionResultDto
                {
                    StageId = stage.Id,
                    StageName = stage.Name,
                    Success = true,
                    Duration = TimeSpan.FromSeconds(1),
                    ToolResults = new List<ToolExecutionResultDto>()
                };

                foreach (var tool in stage.Tools)
                {
                    stageResult.ToolResults.Add(new ToolExecutionResultDto
                    {
                        ToolId = tool.ToolId,
                        ToolName = tool.ToolName,
                        Success = true,
                        Duration = TimeSpan.FromMilliseconds(200),
                        OutputData = new Dictionary<string, object> { { "test", "data" } }
                    });
                }

                result.StageResults.Add(stageResult);
            }

            _logger.LogInformation("Completed workflow test for project {ProjectId}", dto.ProjectId);

            return result;
        }

        public async Task<ProjectDto> ConvertToTemplateAsync(Guid projectId)
        {
            _logger.LogInformation("Converting project {ProjectId} to template", projectId);

            var project = await _unitOfWork.GetGuidRepository<Project>()
                .GetByIdAsync(projectId);

            if (project == null)
            {
                throw new NotFoundException(nameof(Project), projectId);
            }

            project.IsTemplate = true;
            project.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.GetGuidRepository<Project>().Update(project);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Converted project {ProjectId} to template", projectId);

            return _projectMapper.ToDto(project);
        }

        public async Task<ProjectDto> CreateFromTemplateAsync(Guid templateId, CreateProjectDto projectDto)
        {
            _logger.LogInformation("Creating project from template {TemplateId}", templateId);

            var template = await _unitOfWork.GetGuidRepository<Project>()
                .GetByIdAsync(templateId, q => q.Include(p => p.Stages));

            if (template == null || !template.IsTemplate)
            {
                throw new NotFoundException("Project template", templateId);
            }

            // Create new project from template
            var newProject = _projectMapper.ToEntity(projectDto);
            newProject.TemplateId = templateId;
            newProject.TriggerType = template.TriggerType;
            newProject.Schedule = template.Schedule;
            newProject.WorkflowVersion = 1;

            await _unitOfWork.GetGuidRepository<Project>().AddAsync(newProject);
            await _unitOfWork.CommitAsync();

            // Copy stages from template
            var templateStages = await _stageService.GetProjectStagesAsync(templateId);
            
            foreach (var templateStage in templateStages.OrderBy(s => s.Order))
            {
                var createStageDto = new CreateProjectStageDto
                {
                    ProjectId = newProject.Id,
                    Name = templateStage.Name,
                    Description = templateStage.Description,
                    Type = templateStage.Type,
                    OrchestratorType = templateStage.OrchestratorType,
                    OrchestratorConfiguration = templateStage.OrchestratorConfiguration,
                    ReActAgentType = templateStage.ReActAgentType,
                    ReActAgentConfiguration = templateStage.ReActAgentConfiguration,
                    ExecutionStrategy = templateStage.ExecutionStrategy,
                    Tools = templateStage.Tools.Select(t => new CreateProjectStageToolDto
                    {
                        ToolId = t.ToolId,
                        ToolName = t.ToolName,
                        Configuration = t.Configuration,
                        InputMapping = t.InputMapping,
                        OutputMapping = t.OutputMapping,
                        IsRequired = t.IsRequired,
                        ExecutionCondition = t.ExecutionCondition,
                        MaxRetries = t.MaxRetries,
                        TimeoutSeconds = t.TimeoutSeconds,
                        IsActive = t.IsActive,
                        ExpectedOutputFormat = t.ExpectedOutputFormat,
                        Metadata = t.Metadata
                    }).ToList()
                };

                await _stageService.CreateStageAsync(createStageDto);
            }

            _logger.LogInformation("Created project {ProjectId} from template {TemplateId}", 
                newProject.Id, templateId);

            return _projectMapper.ToDto(newProject);
        }

        public async Task<IEnumerable<ProjectDto>> GetWorkflowTemplatesAsync()
        {
            _logger.LogInformation("Getting workflow templates");

            var templates = await _unitOfWork.GetGuidRepository<Project>()
                .GetAsync(
                    filter: p => p.IsTemplate,
                    orderBy: q => q.OrderBy(p => p.Name)
                );

            return templates.Select(_projectMapper.ToDto);
        }

        public async Task<bool> ValidateWorkflowAsync(Guid projectId)
        {
            _logger.LogInformation("Validating workflow for project {ProjectId}", projectId);

            var stages = await _stageService.GetProjectStagesAsync(projectId);

            if (!stages.Any())
            {
                _logger.LogWarning("Project {ProjectId} has no stages", projectId);
                return false;
            }

            foreach (var stage in stages)
            {
                // Validate orchestrator
                if (string.IsNullOrWhiteSpace(stage.OrchestratorType))
                {
                    _logger.LogWarning("Stage {StageId} has no orchestrator", stage.Id);
                    return false;
                }

                // Validate tools if ReAct agent is not set
                if (string.IsNullOrWhiteSpace(stage.ReActAgentType) && !stage.Tools.Any())
                {
                    _logger.LogWarning("Stage {StageId} has no ReAct agent and no tools", stage.Id);
                    return false;
                }

                // Validate tool availability
                foreach (var tool in stage.Tools)
                {
                    var toolDefinition = await _toolRegistry.GetToolAsync(tool.ToolId);
                    if (toolDefinition == null)
                    {
                        _logger.LogWarning("Tool {ToolId} not found in registry", tool.ToolId);
                        return false;
                    }
                }
            }

            _logger.LogInformation("Workflow validation passed for project {ProjectId}", projectId);
            return true;
        }

        public async Task<Dictionary<string, List<string>>> GetAvailableComponentsAsync()
        {
            _logger.LogInformation("Getting available workflow components");

            var components = new Dictionary<string, List<string>>();

            // Get available orchestrators
            components["orchestrators"] = new List<string>
            {
                "ConversationOrchestrator",
                "ToolChainOrchestrator",
                "CustomOrchestrator"
            };

            // Get available ReAct agents
            components["reactAgents"] = new List<string>
            {
                "ConversationReActAgent",
                "AnalysisReActAgent",
                "ProcessingReActAgent"
            };

            // Get execution strategies
            components["executionStrategies"] = Enum.GetNames(typeof(ExecutionStrategy)).ToList();

            // Get available tools
            var tools = await _toolRegistry.GetAllToolsAsync();
            components["tools"] = tools.Select(t => t.Id).ToList();

            // Get stage types
            components["stageTypes"] = Enum.GetNames(typeof(StageType)).ToList();

            _logger.LogInformation("Retrieved {Count} component types", components.Count);

            return components;
        }
    }
}