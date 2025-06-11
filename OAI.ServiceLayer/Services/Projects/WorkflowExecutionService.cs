using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Services.Orchestration;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Extensions;
using System.Text.Json;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IWorkflowExecutionService
    {
        Task<WorkflowExecutionResultDto> ExecuteWorkflowAsync(Guid projectId, Dictionary<string, object> parameters, string initiatedBy, CancellationToken cancellationToken = default);
        Task<WorkflowExecutionStatusDto> GetExecutionStatusAsync(Guid executionId);
        Task CancelExecutionAsync(Guid executionId);
        Task<List<WorkflowStageExecutionResultDto>> GetStageResultsAsync(Guid executionId);
    }

    public interface IWorkflowExecutionNotificationHandler
    {
        Task OnWorkflowStarted(string executionId, string projectId, string projectName, string initiatedBy, int totalStages);
        Task OnStageStarted(string executionId, string projectId, string stageId, string stageName, int stageOrder);
        Task OnStageCompleted(string executionId, string projectId, string stageId, string stageName, int stageOrder, TimeSpan duration, Dictionary<string, object> outputData);
        Task OnStageFailed(string executionId, string projectId, string stageId, string stageName, int stageOrder, string errorMessage);
        Task OnWorkflowCompleted(string executionId, string projectId, TimeSpan duration, int successfulStages, int failedStages);
        Task OnWorkflowFailed(string executionId, string projectId, string errorMessage);
        Task OnLogAdded(string executionId, string projectId, ProjectExecutionLogDto log);
    }

    public class WorkflowExecutionService : IWorkflowExecutionService
    {
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectStage> _stageRepository;
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IOrchestrator<ProjectStageOrchestratorRequest, ProjectStageOrchestratorResponse> _stageOrchestrator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WorkflowExecutionService> _logger;
        private readonly IWorkflowExecutionNotificationHandler _notificationHandler;

        public WorkflowExecutionService(
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectStage> stageRepository,
            IGuidRepository<ProjectExecution> executionRepository,
            IOrchestrator<ProjectStageOrchestratorRequest, ProjectStageOrchestratorResponse> stageOrchestrator,
            IUnitOfWork unitOfWork,
            ILogger<WorkflowExecutionService> logger,
            IWorkflowExecutionNotificationHandler notificationHandler = null)
        {
            _projectRepository = projectRepository;
            _stageRepository = stageRepository;
            _executionRepository = executionRepository;
            _stageOrchestrator = stageOrchestrator;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _notificationHandler = notificationHandler;
        }

        public async Task<WorkflowExecutionResultDto> ExecuteWorkflowAsync(
            Guid projectId, 
            Dictionary<string, object> parameters, 
            string initiatedBy,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting workflow execution for project {ProjectId}", projectId);

            // Load project with stages
            var projects = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId,
                include: q => q.Include(p => p.Stages).ThenInclude(s => s.StageTools));
            var project = projects.FirstOrDefault();

            if (project == null)
                throw new NotFoundException("Project", projectId);

            if (!project.Stages.Any())
                throw new BusinessException("Project has no workflow stages configured");

            // Create execution record
            var execution = new ProjectExecution
            {
                ProjectId = projectId,
                ExecutionType = "WorkflowExecution",
                Status = ExecutionStatus.Running,
                StartedAt = DateTime.UtcNow,
                InputParameters = JsonSerializer.Serialize(parameters),
                OutputData = "{}",  // Initialize as empty JSON object
                ErrorMessage = "",  // Initialize as empty string
                ErrorStackTrace = "",  // Initialize as empty string
                InitiatedBy = initiatedBy,
                ExecutionLog = JsonSerializer.Serialize(new List<ProjectExecutionLogDto>())
            };

            await _executionRepository.CreateAsync(execution);
            await _unitOfWork.CommitAsync();

            var result = new WorkflowExecutionResultDto
            {
                ExecutionId = execution.Id,
                ProjectId = projectId,
                StartedAt = execution.StartedAt,
                Status = ExecutionStatus.Running,
                StageResults = new List<StageExecutionResultDto>()
            };

            // Notify workflow started
            if (_notificationHandler != null)
            {
                await _notificationHandler.OnWorkflowStarted(
                    execution.Id.ToString(),
                    projectId.ToString(),
                    project.Name,
                    initiatedBy,
                    project.Stages.Count);
            }

            try
            {
                // Execute stages in order
                var stages = project.Stages.OrderBy(s => s.Order).ToList();
                var workflowContext = new Dictionary<string, object>(parameters);
                workflowContext["executionId"] = execution.Id.ToString();

                foreach (var stage in stages)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        await UpdateExecutionStatus(execution.Id, ExecutionStatus.Cancelled, "Execution cancelled by user");
                        result.Status = ExecutionStatus.Cancelled;
                        
                        // Notify workflow cancelled
                        if (_notificationHandler != null)
                        {
                            await _notificationHandler.OnWorkflowFailed(
                                execution.Id.ToString(),
                                projectId.ToString(),
                                "Workflow cancelled by user");
                        }
                        
                        break;
                    }

                    var stageResult = await ExecuteStageAsync(stage, workflowContext, execution.Id, initiatedBy, cancellationToken);
                    result.StageResults.Add(stageResult);

                    // Check if stage failed
                    if (!stageResult.Success)
                    {
                        var errorHandling = stage.ErrorHandling;
                        
                        if (errorHandling == ErrorHandlingStrategy.StopOnError)
                        {
                            await UpdateExecutionStatus(execution.Id, ExecutionStatus.Failed, $"Stage '{stage.Name}' failed");
                            result.Status = ExecutionStatus.Failed;
                            result.Error = stageResult.ErrorMessage;
                            
                            // Notify workflow failed due to stage failure
                            if (_notificationHandler != null)
                            {
                                await _notificationHandler.OnWorkflowFailed(
                                    execution.Id.ToString(),
                                    projectId.ToString(),
                                    $"Workflow stopped: Stage '{stage.Name}' failed");
                            }
                            
                            break;
                        }
                        else if (errorHandling == ErrorHandlingStrategy.SkipOnError)
                        {
                            _logger.LogWarning("Stage {StageName} failed but continuing due to SkipOnError strategy", stage.Name);
                            continue;
                        }
                        // ContinueOnError - just log and continue
                        _logger.LogError("Stage {StageName} failed: {Error}", stage.Name, stageResult.ErrorMessage);
                    }

                    // Update workflow context with stage outputs
                    if (stageResult.OutputData != null)
                    {
                        foreach (var output in stageResult.OutputData)
                        {
                            workflowContext[$"{stage.Name}.{output.Key}"] = output.Value;
                        }
                    }

                    // Check continue condition for conditional execution
                    if (stage.ExecutionStrategy == ExecutionStrategy.Conditional && !string.IsNullOrEmpty(stage.ContinueCondition))
                    {
                        if (!EvaluateCondition(stage.ContinueCondition, workflowContext))
                        {
                            _logger.LogInformation("Stage {StageName} continue condition not met, stopping workflow", stage.Name);
                            break;
                        }
                    }
                }

                // Update final execution status
                if (result.Status == ExecutionStatus.Running)
                {
                    var allSuccess = result.StageResults.All(r => r.Success);
                    result.Status = allSuccess ? ExecutionStatus.Completed : ExecutionStatus.Failed;
                    await UpdateExecutionStatus(execution.Id, result.Status, 
                        allSuccess ? "Workflow completed successfully" : "Workflow completed with errors");
                }

                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt.Value - result.StartedAt;

                // Notify workflow completed or failed
                if (_notificationHandler != null)
                {
                    var successfulStages = result.StageResults.Count(r => r.Success);
                    var failedStages = result.StageResults.Count(r => !r.Success);
                    
                    if (result.Status == ExecutionStatus.Completed)
                    {
                        await _notificationHandler.OnWorkflowCompleted(
                            execution.Id.ToString(),
                            projectId.ToString(),
                            result.Duration.Value,
                            successfulStages,
                            failedStages);
                    }
                    else if (result.Status == ExecutionStatus.Failed && failedStages > 0)
                    {
                        // This handles the case where workflow completed with errors
                        await _notificationHandler.OnWorkflowFailed(
                            execution.Id.ToString(),
                            projectId.ToString(),
                            "Workflow completed with errors");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow for project {ProjectId}", projectId);
                result.Status = ExecutionStatus.Failed;
                result.Error = ex.Message;
                await UpdateExecutionStatus(execution.Id, ExecutionStatus.Failed, ex.Message);

                // Notify workflow failed
                if (_notificationHandler != null)
                {
                    await _notificationHandler.OnWorkflowFailed(
                        execution.Id.ToString(),
                        projectId.ToString(),
                        ex.Message);
                }
            }

            return result;
        }

        private async Task<WorkflowStageExecutionResultDto> ExecuteStageAsync(
            ProjectStage stage,
            Dictionary<string, object> workflowContext,
            Guid executionId,
            string initiatedBy,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing stage {StageName} (Order: {Order})", stage.Name, stage.Order);

            var stageResult = new WorkflowStageExecutionResultDto
            {
                StageId = stage.Id,
                StageName = stage.Name,
                Order = stage.Order,
                StartedAt = DateTime.UtcNow
            };

            // Notify stage started
            if (_notificationHandler != null)
            {
                await _notificationHandler.OnStageStarted(
                    executionId.ToString(),
                    stage.ProjectId.ToString(),
                    stage.Id.ToString(),
                    stage.Name,
                    stage.Order);
            }

            try
            {
                // Create orchestrator context
                var orchestratorContext = new OrchestratorContext(initiatedBy, executionId.ToString())
                {
                    ConversationId = Guid.NewGuid().ToString()
                };

                // Add workflow context to orchestrator context
                foreach (var kvp in workflowContext)
                {
                    orchestratorContext.Variables[kvp.Key] = kvp.Value?.ToString() ?? "";
                }

                // Create stage request
                var stageRequest = new ProjectStageOrchestratorRequest
                {
                    UserId = initiatedBy,
                    SessionId = executionId.ToString(),
                    Stage = stage,
                    StageParameters = workflowContext,
                    ExecutionId = executionId.ToString(),
                    UseReActMode = !string.IsNullOrEmpty(stage.ReActAgentType)
                };

                // Execute stage through orchestrator
                var orchestratorResult = await _stageOrchestrator.ExecuteAsync(
                    stageRequest, orchestratorContext, cancellationToken);

                if (orchestratorResult.IsSuccess && orchestratorResult.Data != null)
                {
                    stageResult.Success = true;
                    stageResult.OutputData = orchestratorResult.Data.OutputData;
                    // Convert StageToolExecutionResultDto to ToolExecutionResultDto
                    stageResult.ToolResults = orchestratorResult.Data.StageToolResults
                        .Select(t => new ToolExecutionResultDto
                        {
                            ToolId = t.ToolId,
                            ToolName = t.ToolName,
                            Success = t.Success,
                            Duration = TimeSpan.FromSeconds(t.ExecutionTime),
                            OutputData = t.Result
                        })
                        .ToList();
                    stageResult.Message = orchestratorResult.Data.Message ?? "Stage completed";
                    
                    if (!string.IsNullOrEmpty(orchestratorResult.Data.ReActSummary))
                    {
                        stageResult.ReActSummary = orchestratorResult.Data.ReActSummary;
                    }
                }
                else
                {
                    stageResult.Success = false;
                    stageResult.ErrorMessage = orchestratorResult.Error?.Message ?? "Stage execution failed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing stage {StageName}", stage.Name);
                stageResult.Success = false;
                stageResult.ErrorMessage = ex.Message;
            }

            stageResult.CompletedAt = DateTime.UtcNow;
            stageResult.Duration = stageResult.CompletedAt.Value - stageResult.StartedAt;

            // Log stage execution
            await LogStageExecution(executionId, stageResult);

            // Notify stage completed or failed
            if (_notificationHandler != null)
            {
                if (stageResult.Success)
                {
                    await _notificationHandler.OnStageCompleted(
                        executionId.ToString(),
                        stage.ProjectId.ToString(),
                        stage.Id.ToString(),
                        stage.Name,
                        stage.Order,
                        stageResult.Duration,
                        stageResult.OutputData ?? new Dictionary<string, object>());
                }
                else
                {
                    await _notificationHandler.OnStageFailed(
                        executionId.ToString(),
                        stage.ProjectId.ToString(),
                        stage.Id.ToString(),
                        stage.Name,
                        stage.Order,
                        stageResult.ErrorMessage ?? "Unknown error");
                }
            }

            return stageResult;
        }

        private async Task UpdateExecutionStatus(Guid executionId, ExecutionStatus status, string message)
        {
            var execution = await _executionRepository.GetByIdAsync(executionId);
            if (execution != null)
            {
                execution.Status = status;
                execution.UpdatedAt = DateTime.UtcNow;
                
                if (status == ExecutionStatus.Completed || status == ExecutionStatus.Failed || status == ExecutionStatus.Cancelled)
                {
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.DurationSeconds = (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds;
                }

                // Add to execution log
                var logs = JsonSerializer.Deserialize<List<ProjectExecutionLogDto>>(execution.ExecutionLog ?? "[]");
                var newLog = new ProjectExecutionLogDto
                {
                    Timestamp = DateTime.UtcNow,
                    Level = status == ExecutionStatus.Failed ? "Error" : "Info",
                    Message = message,
                    Source = "WorkflowExecutionService"
                };
                logs.Add(newLog);
                execution.ExecutionLog = JsonSerializer.Serialize(logs);

                await _executionRepository.UpdateAsync(execution);
                await _unitOfWork.CommitAsync();

                // Notify log added
                if (_notificationHandler != null)
                {
                    await _notificationHandler.OnLogAdded(
                        executionId.ToString(),
                        execution.ProjectId.ToString(),
                        newLog);
                }
            }
        }

        private async Task LogStageExecution(Guid executionId, WorkflowStageExecutionResultDto stageResult)
        {
            var execution = await _executionRepository.GetByIdAsync(executionId);
            if (execution != null)
            {
                var logs = JsonSerializer.Deserialize<List<ProjectExecutionLogDto>>(execution.ExecutionLog ?? "[]");
                
                var newLog = new ProjectExecutionLogDto
                {
                    Timestamp = DateTime.UtcNow,
                    Level = stageResult.Success ? "Info" : "Error",
                    Message = $"Stage '{stageResult.StageName}' {(stageResult.Success ? "completed" : "failed")}: {stageResult.Message ?? stageResult.ErrorMessage}",
                    Source = "WorkflowExecutionService"
                };
                logs.Add(newLog);

                execution.ExecutionLog = JsonSerializer.Serialize(logs);
                await _executionRepository.UpdateAsync(execution);
                await _unitOfWork.CommitAsync();

                // Notify log added
                if (_notificationHandler != null)
                {
                    await _notificationHandler.OnLogAdded(
                        executionId.ToString(),
                        execution.ProjectId.ToString(),
                        newLog);
                }
            }
        }

        private bool EvaluateCondition(string condition, Dictionary<string, object> context)
        {
            // Simple condition evaluation - can be enhanced with expression evaluator
            // For now, support basic comparisons like "variable == value"
            try
            {
                // Example: "previousStage.success == true"
                var parts = condition.Split(' ');
                if (parts.Length != 3) return true; // Invalid condition, continue

                var variable = parts[0];
                var op = parts[1];
                var value = parts[2];

                if (context.TryGetValue(variable, out var actualValue))
                {
                    return op switch
                    {
                        "==" => actualValue?.ToString() == value,
                        "!=" => actualValue?.ToString() != value,
                        _ => true
                    };
                }

                return true; // Variable not found, continue
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate condition: {Condition}", condition);
                return true; // On error, continue execution
            }
        }

        public async Task<WorkflowExecutionStatusDto> GetExecutionStatusAsync(Guid executionId)
        {
            var execution = await _executionRepository.GetAsync(
                filter: e => e.Id == executionId,
                include: q => q.Include(e => e.Project))
                .FirstOrDefaultAsync();

            if (execution == null)
                throw new NotFoundException("Execution", executionId);

            var logs = JsonSerializer.Deserialize<List<ProjectExecutionLogDto>>(execution.ExecutionLog ?? "[]");
            
            return new WorkflowExecutionStatusDto
            {
                ExecutionId = execution.Id,
                ProjectId = execution.ProjectId,
                ProjectName = execution.Project.Name,
                Status = execution.Status,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                DurationSeconds = execution.DurationSeconds,
                InitiatedBy = execution.InitiatedBy,
                CurrentStage = GetCurrentStageFromLogs(logs),
                CompletedStages = GetCompletedStagesFromLogs(logs),
                Logs = logs
            };
        }

        private string GetCurrentStageFromLogs(List<ProjectExecutionLogDto> logs)
        {
            var lastStageLog = logs
                .Where(l => l.Message.Contains("Stage") && l.Message.Contains("completed"))
                .LastOrDefault();

            if (lastStageLog != null)
            {
                // Extract stage name from message
                var startIndex = lastStageLog.Message.IndexOf("'") + 1;
                var endIndex = lastStageLog.Message.IndexOf("'", startIndex);
                if (startIndex > 0 && endIndex > startIndex)
                {
                    return lastStageLog.Message.Substring(startIndex, endIndex - startIndex);
                }
            }

            return "Initializing";
        }

        private int GetCompletedStagesFromLogs(List<ProjectExecutionLogDto> logs)
        {
            return logs.Count(l => l.Message.Contains("Stage") && l.Message.Contains("completed"));
        }

        public async Task CancelExecutionAsync(Guid executionId)
        {
            await UpdateExecutionStatus(executionId, ExecutionStatus.Cancelled, "Execution cancelled by user");
        }

        public async Task<List<WorkflowStageExecutionResultDto>> GetStageResultsAsync(Guid executionId)
        {
            var execution = await _executionRepository.GetByIdAsync(executionId);
            if (execution == null)
                throw new NotFoundException("Execution", executionId);

            var logs = JsonSerializer.Deserialize<List<ProjectExecutionLogDto>>(execution.ExecutionLog ?? "[]");
            var stageResults = new List<WorkflowStageExecutionResultDto>();

            // Parse stage results from logs
            foreach (var log in logs.Where(l => l.Message.Contains("Stage") && (l.Message.Contains("completed") || l.Message.Contains("failed"))))
            {
                // Extract stage information from log
                // This is a simplified version - in production, store stage results separately
                var stageName = ExtractStageName(log.Message);
                var success = log.Message.Contains("completed");
                
                stageResults.Add(new WorkflowStageExecutionResultDto
                {
                    StageName = stageName,
                    Success = success,
                    Message = log.Message,
                    StartedAt = log.Timestamp
                });
            }

            return stageResults;
        }

        private string ExtractStageName(string message)
        {
            var startIndex = message.IndexOf("'") + 1;
            var endIndex = message.IndexOf("'", startIndex);
            if (startIndex > 0 && endIndex > startIndex)
            {
                return message.Substring(startIndex, endIndex - startIndex);
            }
            return "Unknown";
        }
    }

    // DTOs for workflow execution
    public class WorkflowExecutionResultDto
    {
        public Guid ExecutionId { get; set; }
        public Guid ProjectId { get; set; }
        public ExecutionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public List<StageExecutionResultDto> StageResults { get; set; } = new();
        public string Error { get; set; }
    }

    public class WorkflowStageExecutionResultDto : StageExecutionResultDto
    {
        public int Order { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Message { get; set; }
        public string ReActSummary { get; set; }
    }

    public class WorkflowExecutionStatusDto
    {
        public Guid ExecutionId { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ExecutionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? DurationSeconds { get; set; }
        public string InitiatedBy { get; set; }
        public string CurrentStage { get; set; }
        public int CompletedStages { get; set; }
        public List<ProjectExecutionLogDto> Logs { get; set; }
    }
}