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
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Mapping.Projects;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IProjectStageService : IBaseGuidService<ProjectStage>
    {
        Task<ProjectStageDto> GetStageAsync(Guid stageId);
        Task<IEnumerable<ProjectStageDto>> GetProjectStagesAsync(Guid projectId);
        Task<ProjectStageDto> CreateStageAsync(CreateProjectStageDto dto);
        Task<ProjectStageDto> UpdateStageAsync(Guid stageId, UpdateProjectStageDto dto);
        Task DeleteStageAsync(Guid stageId);
        Task<bool> ReorderStagesAsync(Guid projectId, List<Guid> stageIds);
        Task<ProjectStageDto> DuplicateStageAsync(Guid stageId);
        Task<bool> AddToolToStageAsync(Guid stageId, CreateProjectStageToolDto toolDto);
        Task<bool> RemoveToolFromStageAsync(Guid stageId, Guid toolId);
        Task<bool> UpdateStageToolAsync(Guid stageId, Guid toolId, UpdateProjectStageToolDto dto);
        Task<bool> ReorderStageToolsAsync(Guid stageId, List<Guid> toolIds);
    }

    public class ProjectStageService : BaseGuidService<ProjectStage>, IProjectStageService
    {
        private readonly IProjectStageMapper _stageMapper;
        private readonly IProjectStageToolMapper _toolMapper;
        private readonly ILogger<ProjectStageService> _logger;

        public ProjectStageService(
            IUnitOfWork unitOfWork, 
            IProjectStageMapper stageMapper,
            IProjectStageToolMapper toolMapper,
            ILogger<ProjectStageService> logger) 
            : base(unitOfWork)
        {
            _stageMapper = stageMapper ?? throw new ArgumentNullException(nameof(stageMapper));
            _toolMapper = toolMapper ?? throw new ArgumentNullException(nameof(toolMapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProjectStageDto> GetStageAsync(Guid stageId)
        {
            _logger.LogInformation("Getting stage with ID {StageId}", stageId);
            
            var stage = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetByIdAsync(stageId, q => q.Include(s => s.StageTools));
                
            if (stage == null)
            {
                throw new NotFoundException(nameof(ProjectStage), stageId);
            }

            return _stageMapper.ToDto(stage);
        }

        public async Task<IEnumerable<ProjectStageDto>> GetProjectStagesAsync(Guid projectId)
        {
            _logger.LogInformation("Getting stages for project {ProjectId}", projectId);
            
            var stages = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetAsync(
                    filter: s => s.ProjectId == projectId,
                    orderBy: q => q.OrderBy(s => s.Order),
                    include: q => q.Include(s => s.StageTools)
                );

            return stages.Select(_stageMapper.ToDto);
        }

        public async Task<ProjectStageDto> CreateStageAsync(CreateProjectStageDto dto)
        {
            _logger.LogInformation("Creating new stage for project {ProjectId}", dto.ProjectId);
            
            // Verify project exists
            var projectExists = await _unitOfWork.GetGuidRepository<Project>()
                .ExistsAsync(p => p.Id == dto.ProjectId);
                
            if (!projectExists)
            {
                throw new NotFoundException(nameof(Project), dto.ProjectId);
            }

            // Get next order number
            var existingStages = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetAsync(filter: s => s.ProjectId == dto.ProjectId);
                
            var maxOrder = existingStages.Any() ? existingStages.Max(s => s.Order) : 0;
            
            var stage = _stageMapper.ToEntity(dto);
            stage.Order = maxOrder + 1;
            
            await _unitOfWork.GetGuidRepository<ProjectStage>().AddAsync(stage);
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Created stage {StageId} for project {ProjectId}", stage.Id, dto.ProjectId);
            
            return _stageMapper.ToDto(stage);
        }

        public async Task<ProjectStageDto> UpdateStageAsync(Guid stageId, UpdateProjectStageDto dto)
        {
            _logger.LogInformation("Updating stage {StageId}", stageId);
            
            var stage = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetByIdAsync(stageId, q => q.Include(s => s.StageTools));
                
            if (stage == null)
            {
                throw new NotFoundException(nameof(ProjectStage), stageId);
            }

            _stageMapper.UpdateEntity(stage, dto);
            
            _unitOfWork.GetGuidRepository<ProjectStage>().Update(stage);
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Updated stage {StageId}", stageId);
            
            return _stageMapper.ToDto(stage);
        }

        public async Task DeleteStageAsync(Guid stageId)
        {
            _logger.LogInformation("Deleting stage {StageId}", stageId);
            
            var stage = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetByIdAsync(stageId, q => q.Include(s => s.StageTools));
                
            if (stage == null)
            {
                throw new NotFoundException(nameof(ProjectStage), stageId);
            }

            var projectId = stage.ProjectId;
            var deletedOrder = stage.Order;
            
            // Delete stage and its tools (cascade delete)
            _unitOfWork.GetGuidRepository<ProjectStage>().Delete(stage);
            
            // Reorder remaining stages
            var remainingStages = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetAsync(filter: s => s.ProjectId == projectId && s.Order > deletedOrder);
                
            foreach (var remainingStage in remainingStages)
            {
                remainingStage.Order--;
                _unitOfWork.GetGuidRepository<ProjectStage>().Update(remainingStage);
            }
            
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Deleted stage {StageId} and reordered remaining stages", stageId);
        }

        public async Task<bool> ReorderStagesAsync(Guid projectId, List<Guid> stageIds)
        {
            _logger.LogInformation("Reordering stages for project {ProjectId}", projectId);
            
            var stages = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetAsync(filter: s => s.ProjectId == projectId);
                
            var stageDict = stages.ToDictionary(s => s.Id);
            
            // Verify all stage IDs belong to the project
            if (!stageIds.All(id => stageDict.ContainsKey(id)))
            {
                throw new BusinessException("Některé stage ID nepatří k projektu");
            }
            
            // Update order
            for (int i = 0; i < stageIds.Count; i++)
            {
                var stage = stageDict[stageIds[i]];
                stage.Order = i + 1;
                _unitOfWork.GetGuidRepository<ProjectStage>().Update(stage);
            }
            
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Reordered {Count} stages for project {ProjectId}", stageIds.Count, projectId);
            
            return true;
        }

        public async Task<ProjectStageDto> DuplicateStageAsync(Guid stageId)
        {
            _logger.LogInformation("Duplicating stage {StageId}", stageId);
            
            var originalStage = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetByIdAsync(stageId, q => q.Include(s => s.StageTools));
                
            if (originalStage == null)
            {
                throw new NotFoundException(nameof(ProjectStage), stageId);
            }
            
            // Create new stage
            var newStage = new ProjectStage
            {
                Id = Guid.NewGuid(),
                ProjectId = originalStage.ProjectId,
                Name = $"{originalStage.Name} (kopie)",
                Description = originalStage.Description,
                Type = originalStage.Type,
                OrchestratorType = originalStage.OrchestratorType,
                OrchestratorConfiguration = originalStage.OrchestratorConfiguration,
                ReActAgentType = originalStage.ReActAgentType,
                ReActAgentConfiguration = originalStage.ReActAgentConfiguration,
                ExecutionStrategy = originalStage.ExecutionStrategy,
                ContinueCondition = originalStage.ContinueCondition,
                ErrorHandling = originalStage.ErrorHandling,
                MaxRetries = originalStage.MaxRetries,
                TimeoutSeconds = originalStage.TimeoutSeconds,
                IsActive = originalStage.IsActive,
                Metadata = originalStage.Metadata,
                Order = originalStage.Order + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Duplicate tools
            foreach (var tool in originalStage.StageTools)
            {
                var newTool = new ProjectStageTool
                {
                    Id = Guid.NewGuid(),
                    ProjectStageId = newStage.Id,
                    ToolId = tool.ToolId,
                    ToolName = tool.ToolName,
                    Order = tool.Order,
                    Configuration = tool.Configuration,
                    InputMapping = tool.InputMapping,
                    OutputMapping = tool.OutputMapping,
                    IsRequired = tool.IsRequired,
                    ExecutionCondition = tool.ExecutionCondition,
                    MaxRetries = tool.MaxRetries,
                    TimeoutSeconds = tool.TimeoutSeconds,
                    IsActive = tool.IsActive,
                    ExpectedOutputFormat = tool.ExpectedOutputFormat,
                    Metadata = tool.Metadata,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                newStage.StageTools.Add(newTool);
            }
            
            // Reorder stages after the insertion point
            var stagesToReorder = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetAsync(filter: s => s.ProjectId == originalStage.ProjectId && s.Order > originalStage.Order);
                
            foreach (var stage in stagesToReorder)
            {
                stage.Order++;
                _unitOfWork.GetGuidRepository<ProjectStage>().Update(stage);
            }
            
            await _unitOfWork.GetGuidRepository<ProjectStage>().AddAsync(newStage);
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Duplicated stage {OriginalId} to {NewId}", stageId, newStage.Id);
            
            return _stageMapper.ToDto(newStage);
        }

        public async Task<bool> AddToolToStageAsync(Guid stageId, CreateProjectStageToolDto toolDto)
        {
            _logger.LogInformation("Adding tool {ToolId} to stage {StageId}", toolDto.ToolId, stageId);
            
            var stage = await _unitOfWork.GetGuidRepository<ProjectStage>()
                .GetByIdAsync(stageId, q => q.Include(s => s.StageTools));
                
            if (stage == null)
            {
                throw new NotFoundException(nameof(ProjectStage), stageId);
            }
            
            // Check if tool already exists in stage
            if (stage.StageTools.Any(t => t.ToolId == toolDto.ToolId))
            {
                throw new BusinessException($"Tool {toolDto.ToolId} již existuje v této stage");
            }
            
            var tool = _toolMapper.ToEntity(toolDto, stageId);
            tool.Order = stage.StageTools.Count + 1;
            
            await _unitOfWork.GetGuidRepository<ProjectStageTool>().AddAsync(tool);
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Added tool {ToolId} to stage {StageId}", toolDto.ToolId, stageId);
            
            return true;
        }

        public async Task<bool> RemoveToolFromStageAsync(Guid stageId, Guid toolId)
        {
            _logger.LogInformation("Removing tool {ToolId} from stage {StageId}", toolId, stageId);
            
            var tool = await _unitOfWork.GetGuidRepository<ProjectStageTool>()
                .GetByIdAsync(toolId);
                
            if (tool == null || tool.ProjectStageId != stageId)
            {
                throw new NotFoundException(nameof(ProjectStageTool), toolId);
            }
            
            var deletedOrder = tool.Order;
            
            _unitOfWork.GetGuidRepository<ProjectStageTool>().Delete(tool);
            
            // Reorder remaining tools
            var remainingTools = await _unitOfWork.GetGuidRepository<ProjectStageTool>()
                .GetAsync(filter: t => t.ProjectStageId == stageId && t.Order > deletedOrder);
                
            foreach (var remainingTool in remainingTools)
            {
                remainingTool.Order--;
                _unitOfWork.GetGuidRepository<ProjectStageTool>().Update(remainingTool);
            }
            
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Removed tool {ToolId} from stage {StageId}", toolId, stageId);
            
            return true;
        }

        public async Task<bool> UpdateStageToolAsync(Guid stageId, Guid toolId, UpdateProjectStageToolDto dto)
        {
            _logger.LogInformation("Updating tool {ToolId} in stage {StageId}", toolId, stageId);
            
            var tool = await _unitOfWork.GetGuidRepository<ProjectStageTool>()
                .GetByIdAsync(toolId);
                
            if (tool == null || tool.ProjectStageId != stageId)
            {
                throw new NotFoundException(nameof(ProjectStageTool), toolId);
            }
            
            _toolMapper.UpdateEntity(tool, dto);
            
            _unitOfWork.GetGuidRepository<ProjectStageTool>().Update(tool);
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Updated tool {ToolId} in stage {StageId}", toolId, stageId);
            
            return true;
        }

        public async Task<bool> ReorderStageToolsAsync(Guid stageId, List<Guid> toolIds)
        {
            _logger.LogInformation("Reordering tools in stage {StageId}", stageId);
            
            var tools = await _unitOfWork.GetGuidRepository<ProjectStageTool>()
                .GetAsync(filter: t => t.ProjectStageId == stageId);
                
            var toolDict = tools.ToDictionary(t => t.Id);
            
            // Verify all tool IDs belong to the stage
            if (!toolIds.All(id => toolDict.ContainsKey(id)))
            {
                throw new BusinessException("Některé tool ID nepatří k této stage");
            }
            
            // Update order
            for (int i = 0; i < toolIds.Count; i++)
            {
                var tool = toolDict[toolIds[i]];
                tool.Order = i + 1;
                _unitOfWork.GetGuidRepository<ProjectStageTool>().Update(tool);
            }
            
            await _unitOfWork.CommitAsync();
            
            _logger.LogInformation("Reordered {Count} tools in stage {StageId}", toolIds.Count, stageId);
            
            return true;
        }
    }
}