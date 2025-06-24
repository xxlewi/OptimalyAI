using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Orchestration;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IWorkflowExecutionService
    {
        Task<WorkflowExecutionResult> ExecuteWorkflowAsync(Guid projectId, Dictionary<string, object> parameters, string initiatedBy);
    }

    public class WorkflowExecutionResult
    {
        public Guid ExecutionId { get; set; }
        public string Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class WorkflowExecutionService : IWorkflowExecutionService
    {
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOrchestratorSettings _orchestratorSettings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WorkflowExecutionService> _logger;

        public WorkflowExecutionService(
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectExecution> executionRepository,
            IServiceProvider serviceProvider,
            IOrchestratorSettings orchestratorSettings,
            IUnitOfWork unitOfWork,
            ILogger<WorkflowExecutionService> logger)
        {
            _projectRepository = projectRepository;
            _executionRepository = executionRepository;
            _serviceProvider = serviceProvider;
            _orchestratorSettings = orchestratorSettings;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(Guid projectId, Dictionary<string, object> parameters, string initiatedBy)
        {
            _logger.LogInformation("Starting workflow execution for project {ProjectId}", projectId);

            // Načíst projekt
            var projects = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId,
                include: q => q
                    .Include(p => p.Stages)
                    .ThenInclude(s => s.StageTools));
            var project = projects.FirstOrDefault();

            if (project == null)
            {
                throw new NotFoundException("Project", projectId);
            }

            // Vytvořit execution záznam
            var execution = new ProjectExecution
            {
                ProjectId = projectId,
                ExecutionType = parameters.ContainsKey("__testMode") && (bool)parameters["__testMode"] ? "test" : "production",
                Status = ExecutionStatus.Running,
                StartedAt = DateTime.UtcNow,
                InitiatedBy = initiatedBy,
                InputParameters = JsonSerializer.Serialize(parameters),
                ItemsProcessedCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _executionRepository.CreateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                // Získat ID výchozího workflow orchestrátoru
                var defaultOrchestratorId = await _orchestratorSettings.GetDefaultWorkflowOrchestratorIdAsync();
                if (string.IsNullOrEmpty(defaultOrchestratorId))
                {
                    throw new BusinessException("No default workflow orchestrator configured. Please configure a default workflow orchestrator in the orchestrator settings.");
                }
                
                _logger.LogInformation("Using default workflow orchestrator ID: {OrchestratorId}", defaultOrchestratorId);

                // Získat typovanou instanci orchestrátoru - WorkflowOrchestratorV2 je jediný workflow orchestrátor
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetService<IOrchestrator<WorkflowOrchestratorRequest, WorkflowOrchestratorResponse>>();
                
                if (orchestrator == null)
                {
                    throw new BusinessException($"Could not create orchestrator instance. WorkflowOrchestratorV2 is not registered in DI.");
                }
                
                // Ověřit, že je to správný orchestrátor
                if (orchestrator.Id != defaultOrchestratorId)
                {
                    _logger.LogWarning("Orchestrator ID mismatch. Expected: {Expected}, Actual: {Actual}", defaultOrchestratorId, orchestrator.Id);
                    throw new BusinessException($"Orchestrator ID mismatch. Expected {defaultOrchestratorId} but got {orchestrator.Id}");
                }

                // Připravit workflow definition z projektu
                var workflowDefinition = new
                {
                    projectId = project.Id,
                    projectName = project.Name,
                    stages = project.Stages.OrderBy(s => s.Order).Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        order = s.Order,
                        isParallel = false, // ProjectStage doesn't have IsParallel property yet
                        tools = s.StageTools.Select(st => new
                        {
                            toolName = st.ToolName,
                            toolConfiguration = new Dictionary<string, object>() // ProjectStageTool doesn't have ToolConfiguration yet
                        })
                    }),
                    configuration = string.IsNullOrEmpty(project.Configuration) ? null : JsonSerializer.Deserialize<object>(project.Configuration)
                };

                // Log project stages info
                _logger.LogInformation("Project {ProjectId} has {StageCount} stages", project.Id, project.Stages.Count);
                
                // Try to get workflow from workflow designer first
                List<OAI.Core.DTOs.Workflow.WorkflowStep> workflowSteps = null;
                
                try
                {
                    // Get workflow designer service
                    var workflowDesignerService = scope.ServiceProvider.GetService<OAI.Core.Interfaces.Workflow.IWorkflowDesignerService>();
                    if (workflowDesignerService != null)
                    {
                        var workflowData = await workflowDesignerService.GetWorkflowAsync(projectId);
                        if (workflowData != null && workflowData.Steps != null && workflowData.Steps.Any())
                        {
                            _logger.LogInformation("Found workflow designer data with {StepCount} steps", workflowData.Steps.Count);
                            
                            // Debug: Log each step details
                            foreach (var step in workflowData.Steps)
                            {
                                _logger.LogInformation("Step '{StepName}' (Type: {StepType}, AdapterId: {AdapterId}, AdapterType: {AdapterType})", 
                                    step.Name, step.Type, step.AdapterId ?? "NULL", step.AdapterType ?? "NULL");
                            }
                            // Convert WorkflowStepDto to WorkflowStep
                            workflowSteps = workflowData.Steps.Select(dto => new OAI.Core.DTOs.Workflow.WorkflowStep
                            {
                                Id = dto.Id,
                                Name = dto.Name,
                                Type = dto.Type,
                                Description = dto.Description,
                                Position = dto.Position,
                                Next = dto.Next,
                                Tool = dto.Tool,
                                UseReAct = dto.UseReAct,
                                TimeoutSeconds = dto.TimeoutSeconds,
                                RetryCount = dto.RetryCount,
                                Configuration = dto.Configuration ?? new Dictionary<string, object>(),
                                AdapterId = dto.AdapterId,
                                AdapterType = dto.AdapterType,
                                AdapterConfiguration = dto.AdapterConfiguration != null ? 
                                    new Dictionary<string, Dictionary<string, object>> { ["config"] = dto.AdapterConfiguration } : 
                                    new Dictionary<string, Dictionary<string, object>>(),
                                Condition = dto.Condition,
                                IsFinal = dto.IsFinal
                            }).ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not load workflow designer data: {Error}", ex.Message);
                }
                
                // If no workflow designer data, use project stages
                if (workflowSteps == null || !workflowSteps.Any())
                {
                    _logger.LogInformation("Using project stages for workflow steps");
                    foreach (var stage in project.Stages.OrderBy(s => s.Order))
                    {
                        _logger.LogInformation("Stage: {StageName} (Order: {Order}, Tools: {ToolCount})", 
                            stage.Name, stage.Order, stage.StageTools.Count);
                    }
                    
                    workflowSteps = project.Stages.OrderBy(s => s.Order).Select((stage, index) => new OAI.Core.DTOs.Workflow.WorkflowStep
                        {
                            Id = stage.Id.ToString(),
                            Name = stage.Name,
                            Type = "default", // Default step type
                            Position = stage.Order,
                            Next = index < project.Stages.Count - 1 ? project.Stages.OrderBy(s => s.Order).Skip(index + 1).First().Id.ToString() : null,
                            Tool = stage.StageTools.FirstOrDefault()?.ToolName, // Primary tool for the stage
                            UseReAct = true, // Enable ReAct pattern by default
                            TimeoutSeconds = 300,
                            RetryCount = 3,
                            Configuration = new Dictionary<string, object>
                            {
                                ["stageId"] = stage.Id,
                                ["stageName"] = stage.Name,
                                ["stageOrder"] = stage.Order,
                                ["tools"] = stage.StageTools.Select(t => t.ToolName).ToList()
                            },
                            IsFinal = index == project.Stages.Count - 1
                        }).ToList();
                }
                
                // Připravit request pro orchestrátor
                var request = new WorkflowOrchestratorRequest
                {
                    WorkflowId = projectId, // Use project ID as workflow ID
                    WorkflowDefinition = new OAI.Core.DTOs.Workflow.WorkflowDefinition
                    {
                        Steps = workflowSteps
                    },
                    InitialParameters = parameters,
                    AIModel = "default", // Will be set from orchestrator settings
                    EnableIntelligentRetry = true,
                    MaxRetries = 3
                };
                
                // Log converted workflow steps
                _logger.LogInformation("Converted to {StepCount} workflow steps", request.WorkflowDefinition.Steps.Count);
                foreach (var step in request.WorkflowDefinition.Steps)
                {
                    _logger.LogInformation("Workflow Step: {StepId} - {StepName} (Tool: {Tool}, IsFinal: {IsFinal})", 
                        step.Id, step.Name, step.Tool ?? "none", step.IsFinal);
                }

                // Vytvořit orchestration context
                var context = new OAI.ServiceLayer.Services.Orchestration.Base.OrchestratorContext(
                    userId: initiatedBy,
                    sessionId: Guid.NewGuid().ToString()
                );
                
                // Přidat metadata
                context.Metadata["projectId"] = projectId.ToString();
                context.Metadata["executionId"] = execution.Id.ToString();
                context.Metadata["executionType"] = execution.ExecutionType;
                
                // Přidat parametry jako variables
                foreach (var param in parameters)
                {
                    context.Variables[param.Key] = param.Value;
                }

                // Spustit orchestrátor
                _logger.LogInformation("Executing workflow with orchestrator {OrchestratorName} (ID: {OrchestratorId})", orchestrator.Name, orchestrator.Id);
                var result = await orchestrator.ExecuteAsync(request, context);
                // Získat response z result
                var response = result.Data;
                if (response == null)
                {
                    throw new BusinessException("Failed to get response from orchestrator result");
                }

                // Aktualizovat execution podle výsledku
                execution.Status = result.IsSuccess ? ExecutionStatus.Completed : ExecutionStatus.Failed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationSeconds = (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds;
                execution.ItemsProcessedCount = response.StepResults?.Count ?? 0;
                execution.OutputData = response.FinalOutputs != null ? JsonSerializer.Serialize(response.FinalOutputs) : null;
                
                // Sestavit execution log z výsledků kroků
                var executionLog = new List<object>();
                if (response.StepResults != null)
                {
                    foreach (var step in response.StepResults)
                    {
                        executionLog.Add(new
                        {
                            timestamp = step.StartedAt,
                            level = step.Success ? "Info" : "Error",
                            message = $"Step '{step.StepName}' {(step.Success ? "completed successfully" : "failed")}",
                            duration = step.DurationMs,
                            output = step.Output,
                            error = step.Error
                        });
                    }
                }
                execution.ExecutionLog = JsonSerializer.Serialize(executionLog);
                execution.ErrorMessage = result.Error?.Message;
                execution.UpdatedAt = DateTime.UtcNow;

                await _executionRepository.UpdateAsync(execution);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Workflow execution completed with status: {Status}", execution.Status);

                return new WorkflowExecutionResult
                {
                    ExecutionId = execution.Id,
                    Status = execution.Status.ToString(),
                    ErrorMessage = execution.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow for project {ProjectId}", projectId);

                // Aktualizovat execution jako failed
                execution.Status = ExecutionStatus.Failed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationSeconds = (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds;
                execution.ErrorMessage = ex.Message;
                execution.ErrorStackTrace = ex.StackTrace;
                execution.UpdatedAt = DateTime.UtcNow;

                await _executionRepository.UpdateAsync(execution);
                await _unitOfWork.SaveChangesAsync();

                return new WorkflowExecutionResult
                {
                    ExecutionId = execution.Id,
                    Status = "Failed",
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}