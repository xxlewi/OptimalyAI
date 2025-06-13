using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Projects;
using OAI.ServiceLayer.Mapping.Projects;

namespace OAI.ServiceLayer.Services.Projects
{
    public class ProjectStageService : IProjectStageService
    {
        private readonly IGuidRepository<ProjectStage> _stageRepository;
        private readonly IGuidRepository<ProjectStageTool> _stageToolRepository;
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectStageMapper _stageMapper;
        private readonly ILogger<ProjectStageService> _logger;

        public ProjectStageService(
            IGuidRepository<ProjectStage> stageRepository,
            IGuidRepository<ProjectStageTool> stageToolRepository,
            IGuidRepository<Project> projectRepository,
            IUnitOfWork unitOfWork,
            IProjectStageMapper stageMapper,
            ILogger<ProjectStageService> logger)
        {
            _stageRepository = stageRepository;
            _stageToolRepository = stageToolRepository;
            _projectRepository = projectRepository;
            _unitOfWork = unitOfWork;
            _stageMapper = stageMapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ProjectStageDto>> GetProjectStagesAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var stages = await _stageRepository.GetAsync(
                filter: s => s.ProjectId == projectId,
                orderBy: q => q.OrderBy(s => s.Order),
                include: query => query.Include(s => s.StageTools)
            );

            var stageList = stages.ToList();
            return stageList.Select(s => _stageMapper.ToDto(s));
        }

        public async Task<ProjectStageDto?> GetStageByIdAsync(Guid stageId, CancellationToken cancellationToken = default)
        {
            var stages = await _stageRepository.GetAsync(
                filter: s => s.Id == stageId,
                include: query => query.Include(s => s.StageTools)
            );
            var stage = stages.FirstOrDefault();

            return stage != null ? _stageMapper.ToDto(stage) : null;
        }

        public async Task<ProjectStageDto> CreateStageAsync(Guid projectId, CreateProjectStageDto createDto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating new stage for project: {ProjectId}", projectId);

            // Verify project exists
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                throw new NotFoundException("Project", projectId);

            // Get current max order
            var existingStages = await _stageRepository.GetAsync(s => s.ProjectId == projectId);
            var maxOrder = existingStages.Any() ? existingStages.Max(s => s.Order) : 0;

            var stage = _stageMapper.ToEntity(createDto);
            stage.ProjectId = projectId;
            stage.Order = maxOrder + 1;
            stage.CreatedAt = DateTime.UtcNow;
            stage.UpdatedAt = DateTime.UtcNow;

            await _stageRepository.CreateAsync(stage);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Stage created successfully with ID: {Id}", stage.Id);
            return _stageMapper.ToDto(stage);
        }

        public async Task<ProjectStageDto> UpdateStageAsync(Guid stageId, UpdateProjectStageDto updateDto, CancellationToken cancellationToken = default)
        {
            var stages = await _stageRepository.GetAsync(
                filter: s => s.Id == stageId,
                include: query => query.Include(s => s.StageTools));
            var stage = stages.FirstOrDefault();

            if (stage == null)
                throw new NotFoundException("ProjectStage", stageId);

            _stageMapper.UpdateEntity(stage, updateDto);
            stage.UpdatedAt = DateTime.UtcNow;

            await _stageRepository.UpdateAsync(stage);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Stage {Id} updated successfully", stageId);
            return _stageMapper.ToDto(stage);
        }

        public async Task<bool> DeleteStageAsync(Guid stageId, CancellationToken cancellationToken = default)
        {
            var stage = await _stageRepository.GetByIdAsync(stageId);
            if (stage == null)
                return false;

            // Reorder remaining stages
            var remainingStages = await _stageRepository.GetAsync(
                filter: s => s.ProjectId == stage.ProjectId && s.Order > stage.Order,
                orderBy: q => q.OrderBy(s => s.Order)
            );

            foreach (var remainingStage in remainingStages)
            {
                remainingStage.Order--;
                await _stageRepository.UpdateAsync(remainingStage);
            }

            await _stageRepository.DeleteAsync(stage.Id);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ReorderStagesAsync(Guid projectId, IEnumerable<Guid> orderedStageIds, CancellationToken cancellationToken = default)
        {
            var stageIds = orderedStageIds.ToList();
            var stages = await _stageRepository.GetAsync(s => s.ProjectId == projectId);
            var stagesList = stages.ToList();

            if (stagesList.Count != stageIds.Count)
                throw new BusinessException("Stage count mismatch");

            int order = 1;
            foreach (var stageId in stageIds)
            {
                var stage = stagesList.FirstOrDefault(s => s.Id == stageId);
                if (stage == null)
                    throw new NotFoundException("ProjectStage", stageId);

                stage.Order = order++;
                stage.UpdatedAt = DateTime.UtcNow;
                await _stageRepository.UpdateAsync(stage);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AddToolToStageAsync(Guid stageId, string toolId, object? configuration = null, CancellationToken cancellationToken = default)
        {
            var stage = await _stageRepository.GetByIdAsync(stageId);
            if (stage == null)
                throw new NotFoundException("ProjectStage", stageId);

            // Check if tool already exists in stage
            var existingTools = await _stageToolRepository.GetAsync(
                st => st.ProjectStageId == stageId && st.ToolId == toolId
            );
            var existingTool = existingTools.FirstOrDefault();

            if (existingTool != null)
                throw new BusinessException($"Tool {toolId} already exists in stage");

            var stageTool = new ProjectStageTool
            {
                ProjectStageId = stageId,
                ToolId = toolId,
                Configuration = configuration != null ? JsonSerializer.Serialize(configuration) : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _stageToolRepository.CreateAsync(stageTool);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RemoveToolFromStageAsync(Guid stageId, string toolId, CancellationToken cancellationToken = default)
        {
            var stageTools = await _stageToolRepository.GetAsync(
                st => st.ProjectStageId == stageId && st.ToolId == toolId
            );
            var stageTool = stageTools.FirstOrDefault();

            if (stageTool == null)
                return false;

            await _stageToolRepository.DeleteAsync(stageTool.Id);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateStageToolConfigurationAsync(Guid stageId, string toolId, object configuration, CancellationToken cancellationToken = default)
        {
            var stageTools = await _stageToolRepository.GetAsync(
                st => st.ProjectStageId == stageId && st.ToolId == toolId
            );
            var stageTool = stageTools.FirstOrDefault();

            if (stageTool == null)
                throw new NotFoundException($"Tool {toolId} not found in stage {stageId}");

            stageTool.Configuration = JsonSerializer.Serialize(configuration);
            stageTool.UpdatedAt = DateTime.UtcNow;

            await _stageToolRepository.UpdateAsync(stageTool);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }
}