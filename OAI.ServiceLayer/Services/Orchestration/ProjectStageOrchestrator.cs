using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Services.Orchestration.Exceptions;
using System.Text.Json;

namespace OAI.ServiceLayer.Services.Orchestration
{
    public class ProjectStageOrchestratorRequest : OrchestratorRequestDto
    {
        public ProjectStage Stage { get; set; }
        public Dictionary<string, object> StageParameters { get; set; }
        public string ExecutionId { get; set; }
        public bool UseReActMode { get; set; }
    }

    public class ProjectStageOrchestratorResponse : OrchestratorResponseDto
    {
        public string StageId { get; set; }
        public string StageName { get; set; }
        public ExecutionStatus Status { get; set; }
        public Dictionary<string, object> OutputData { get; set; } = new();
        public List<StageToolExecutionResultDto> StageToolResults { get; set; } = new();
        public string ReActSummary { get; set; }
        public string Message { get; set; }
        public string Error => ErrorMessage;
    }

    public class ProjectStageOrchestrator : BaseOrchestrator<ProjectStageOrchestratorRequest, ProjectStageOrchestratorResponse>
    {
        private readonly IOrchestrator<ToolChainOrchestratorRequestDto, ConversationOrchestratorResponseDto> _toolChainOrchestrator;
        private readonly IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto> _conversationOrchestrator;
        private readonly IReActAgent _reActAgent;
        private readonly IToolRegistry _toolRegistry;
        private readonly new ILogger<ProjectStageOrchestrator> _logger;

        public ProjectStageOrchestrator(
            IOrchestrator<ToolChainOrchestratorRequestDto, ConversationOrchestratorResponseDto> toolChainOrchestrator,
            IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto> conversationOrchestrator,
            IReActAgent reActAgent,
            IToolRegistry toolRegistry,
            IOrchestratorMetrics metrics,
            ILogger<ProjectStageOrchestrator> logger,
            IServiceProvider serviceProvider)
            : base(logger, metrics, serviceProvider)
        {
            _toolChainOrchestrator = toolChainOrchestrator;
            _conversationOrchestrator = conversationOrchestrator;
            _reActAgent = reActAgent;
            _toolRegistry = toolRegistry;
            _logger = logger;
        }

        public override string Id => "project_stage_orchestrator";
        public override string Name => "ProjectStageOrchestrator";
        public override string Description => "Orchestrates execution of a single project workflow stage";

        protected override async Task<ProjectStageOrchestratorResponse> ExecuteCoreAsync(
            ProjectStageOrchestratorRequest request,
            IOrchestratorContext context,
            OrchestratorResult<ProjectStageOrchestratorResponse> result,
            CancellationToken cancellationToken)
        {
            var stage = request.Stage;
            _logger.LogInformation("Processing stage {StageName} for execution {ExecutionId}", 
                stage.Name, request.ExecutionId);

            context.Variables["stageId"] = stage.Id.ToString();
            context.Variables["stageName"] = stage.Name;
            context.Variables["executionId"] = request.ExecutionId;

            var response = new ProjectStageOrchestratorResponse
            {
                StageId = stage.Id.ToString(),
                StageName = stage.Name,
                ExecutionId = request.ExecutionId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // Handle different orchestrator types
                switch (stage.OrchestratorType)
                {
                    case "ConversationOrchestrator":
                        await ExecuteConversationOrchestrator(stage, request, context, response, cancellationToken);
                        break;
                        
                    case "ToolChainOrchestrator":
                        await ExecuteToolChainOrchestrator(stage, request, context, response, cancellationToken);
                        break;
                        
                    case "CustomOrchestrator":
                        await ExecuteCustomOrchestrator(stage, request, context, response, cancellationToken);
                        break;
                        
                    default:
                        throw new UnsupportedOrchestratorTypeException(stage.Id.ToString(), stage.Name, request.ExecutionId, stage.OrchestratorType);
                }

                // Execute ReAct agent if configured
                if (!string.IsNullOrEmpty(stage.ReActAgentType) && request.UseReActMode)
                {
                    await ExecuteReActAgent(stage, request, context, response, cancellationToken);
                }

                response.Status = ExecutionStatus.Completed;
                response.Success = true;
                response.Message = $"Stage '{stage.Name}' executed successfully";
                response.CompletedAt = DateTime.UtcNow;
                response.DurationMs = (response.CompletedAt - response.StartedAt).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing stage {StageName}", stage.Name);
                response.Status = ExecutionStatus.Failed;
                response.Success = false;
                response.Message = $"Stage execution failed: {ex.Message}";
                response.ErrorMessage = ex.Message;
                response.CompletedAt = DateTime.UtcNow;
                response.DurationMs = (response.CompletedAt - response.StartedAt).TotalMilliseconds;
            }

            // Add execution metadata
            response.Metadata["stageType"] = stage.Type.ToString();
            response.Metadata["orchestratorType"] = stage.OrchestratorType;
            response.Metadata["executionStrategy"] = stage.ExecutionStrategy.ToString();

            return response;
        }

        private async Task ExecuteConversationOrchestrator(
            ProjectStage stage,
            ProjectStageOrchestratorRequest request,
            IOrchestratorContext context,
            ProjectStageOrchestratorResponse response,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Executing ConversationOrchestrator for stage {StageName}", stage.Name);

            var conversationRequest = new ConversationOrchestratorRequestDto
            {
                UserId = request.UserId,
                SessionId = request.SessionId,
                ConversationId = Guid.NewGuid().ToString(),
                Message = request.StageParameters.TryGetValue("prompt", out var prompt) 
                    ? prompt.ToString() 
                    : stage.Description
            };

            try
            {
                var conversationResult = await _conversationOrchestrator.ExecuteAsync(
                    conversationRequest, context, cancellationToken);

                if (conversationResult.IsSuccess && conversationResult.Data != null)
            {
                response.OutputData["response"] = conversationResult.Data.Response ?? "";
                response.OutputData["modelId"] = conversationResult.Data.ModelId ?? "";
                response.OutputData["toolsDetected"] = conversationResult.Data.ToolsDetected;
                
                // Add tool usage information
                if (conversationResult.Data.ToolsUsed != null)
                {
                    foreach (var tool in conversationResult.Data.ToolsUsed)
                    {
                        response.StageToolResults.Add(new StageToolExecutionResultDto
                        {
                            ToolId = tool.ToolId,
                            ToolName = tool.ToolName,
                            Success = tool.Success,
                            Result = tool.Parameters ?? new Dictionary<string, object>(),
                            ExecutionTime = tool.DurationMs / 1000.0 // Convert to seconds
                        });
                    }
                }

                // Add to parent response
                response.ToolsUsed.AddRange(conversationResult.Data.ToolsUsed);
            }
            else
            {
                throw new ChildOrchestratorExecutionException(
                    stage.Id.ToString(), stage.Name, request.ExecutionId, 
                    "ConversationOrchestrator", conversationRequest.ConversationId, 
                    conversationResult.Error?.Message ?? "Unknown error");
            }
            }
            catch (ChildOrchestratorExecutionException)
            {
                throw; // Re-throw our specific exceptions
            }
            catch (Exception ex)
            {
                throw new ChildOrchestratorExecutionException(
                    stage.Id.ToString(), stage.Name, request.ExecutionId, 
                    "ConversationOrchestrator", conversationRequest.ConversationId, 
                    ex.Message, ex);
            }
        }

        private async Task ExecuteToolChainOrchestrator(
            ProjectStage stage,
            ProjectStageOrchestratorRequest request,
            IOrchestratorContext context,
            ProjectStageOrchestratorResponse response,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Executing ToolChainOrchestrator for stage {StageName}", stage.Name);

            // Get tools for this stage
            var stageTools = stage.StageTools.OrderBy(t => t.Order).ToList();
            if (!stageTools.Any())
            {
                throw new StageToolsNotConfiguredException(stage.Id.ToString(), stage.Name, request.ExecutionId);
            }

            // Build tool chain steps
            var steps = new List<ToolChainStepDto>();
            var stepIdMap = new Dictionary<string, string>();
            
            foreach (var stageTool in stageTools)
            {
                var stepId = $"step_{stageTool.Order}";
                stepIdMap[stageTool.ToolId] = stepId;
                
                var step = new ToolChainStepDto
                {
                    StepId = stepId,
                    ToolId = stageTool.ToolId,
                    Parameters = new Dictionary<string, object>()
                };

                // Apply tool configuration
                if (!string.IsNullOrEmpty(stageTool.Configuration))
                {
                    try
                    {
                        var configDoc = JsonDocument.Parse(stageTool.Configuration);
                        foreach (var prop in configDoc.RootElement.EnumerateObject())
                        {
                            step.Parameters[prop.Name] = prop.Value.GetRawText();
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse tool configuration for {ToolId}", stageTool.ToolId);
                        throw new InvalidToolConfigurationException(stage.Id.ToString(), stage.Name, request.ExecutionId, stageTool.ToolId, "Invalid JSON format in tool configuration", ex);
                    }
                }

                // Apply input mapping
                if (!string.IsNullOrEmpty(stageTool.InputMapping))
                {
                    try
                    {
                        var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(stageTool.InputMapping);
                        if (mappings != null)
                        {
                            step.ParameterMappings = mappings;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse input mapping for {ToolId}", stageTool.ToolId);
                        throw new InvalidInputMappingException(stage.Id.ToString(), stage.Name, request.ExecutionId, stageTool.ToolId, "Invalid JSON format in input mapping", ex);
                    }
                }

                // Apply stage parameters
                foreach (var param in request.StageParameters)
                {
                    if (!step.Parameters.ContainsKey(param.Key))
                    {
                        step.Parameters[param.Key] = param.Value;
                    }
                }

                steps.Add(step);
            }

            var toolChainRequest = new ToolChainOrchestratorRequestDto
            {
                UserId = request.UserId,
                SessionId = request.SessionId,
                Steps = steps,
                ExecutionStrategy = stage.ExecutionStrategy.ToString().ToLower(),
                StopOnError = stage.ErrorHandling == ErrorHandlingStrategy.StopOnError,
                GlobalParameters = request.StageParameters,
                TimeoutSeconds = stage.TimeoutSeconds
            };

            try
            {
                var toolChainResult = await _toolChainOrchestrator.ExecuteAsync(
                    toolChainRequest, context, cancellationToken);

                if (toolChainResult.IsSuccess && toolChainResult.Data != null)
            {
                // Process tool results from orchestrator response
                if (toolChainResult.Data.ToolsUsed != null)
                {
                    foreach (var tool in toolChainResult.Data.ToolsUsed)
                    {
                        response.StageToolResults.Add(new StageToolExecutionResultDto
                        {
                            ToolId = tool.ToolId,
                            ToolName = tool.ToolName,
                            Success = tool.Success,
                            Result = tool.Parameters ?? new Dictionary<string, object>(),
                            ExecutionTime = tool.DurationMs / 1000.0 // Convert to seconds
                        });

                        // Store output data
                        if (tool.Parameters != null)
                        {
                            response.OutputData[$"{tool.ToolId}_output"] = tool.Parameters;
                        }
                    }
                }

                // Add to parent response
                response.ToolsUsed.AddRange(toolChainResult.Data.ToolsUsed);
            }
            else
            {
                throw new ChildOrchestratorExecutionException(
                    stage.Id.ToString(), stage.Name, request.ExecutionId, 
                    "ToolChainOrchestrator", toolChainRequest.SessionId ?? "unknown", 
                    toolChainResult.Error?.Message ?? "Unknown error");
            }
            }
            catch (ChildOrchestratorExecutionException)
            {
                throw; // Re-throw our specific exceptions
            }
            catch (Exception ex)
            {
                throw new ChildOrchestratorExecutionException(
                    stage.Id.ToString(), stage.Name, request.ExecutionId, 
                    "ToolChainOrchestrator", toolChainRequest.SessionId ?? "unknown", 
                    ex.Message, ex);
            }
        }

        private async Task ExecuteCustomOrchestrator(
            ProjectStage stage,
            ProjectStageOrchestratorRequest request,
            IOrchestratorContext context,
            ProjectStageOrchestratorResponse response,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Executing CustomOrchestrator for stage {StageName}", stage.Name);
            
            // Custom orchestrator logic can be implemented here
            // For now, we'll use ToolChainOrchestrator as fallback
            await ExecuteToolChainOrchestrator(stage, request, context, response, cancellationToken);
        }

        private async Task ExecuteReActAgent(
            ProjectStage stage,
            ProjectStageOrchestratorRequest request,
            IOrchestratorContext context,
            ProjectStageOrchestratorResponse response,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Executing ReAct agent for stage {StageName}", stage.Name);

            var objective = request.StageParameters.TryGetValue("objective", out var obj) 
                ? obj.ToString() 
                : $"Complete the stage: {stage.Name}";

            try
            {
                var reActResult = await _reActAgent.ExecuteAsync(objective, context, cancellationToken);

                if (reActResult.IsCompleted)
            {
                response.ReActSummary = reActResult.FinalAnswer;
                response.OutputData["reActThoughts"] = reActResult.Thoughts.Select(t => t.Content).ToList();
                response.OutputData["reActActions"] = reActResult.Actions.Select(a => new
                {
                    Tool = a.ToolName,
                    Input = a.Input,
                    Reasoning = a.Reasoning
                }).ToList();
                response.OutputData["reActObservations"] = reActResult.Observations.Select(o => o.Content).ToList();
            }
            else
            {
                throw new ReActAgentExecutionException(
                    stage.Id.ToString(), stage.Name, request.ExecutionId, 
                    stage.ReActAgentType, objective, 
                    "ReAct agent did not complete successfully");
            }
            }
            catch (ReActAgentExecutionException)
            {
                throw; // Re-throw our specific exceptions
            }
            catch (Exception ex)
            {
                throw new ReActAgentExecutionException(
                    stage.Id.ToString(), stage.Name, request.ExecutionId, 
                    stage.ReActAgentType, objective, 
                    ex.Message, ex);
            }
        }

        public override async Task<OrchestratorValidationResult> ValidateAsync(ProjectStageOrchestratorRequest request)
        {
            var validationResult = new OrchestratorValidationResult { IsValid = true };
            var errors = new List<string>();

            if (request == null)
            {
                errors.Add("Request cannot be null");
                validationResult.IsValid = false;
                validationResult.Errors = errors;
                return await Task.FromResult(validationResult);
            }

            if (request.Stage == null)
            {
                errors.Add("Stage cannot be null");
            }
            else
            {
                // Validate stage basic properties
                if (string.IsNullOrEmpty(request.Stage.OrchestratorType))
                {
                    errors.Add("Stage must have an orchestrator type");
                }
                else
                {
                    // Validate supported orchestrator types
                    var supportedTypes = new[] { "ConversationOrchestrator", "ToolChainOrchestrator", "CustomOrchestrator" };
                    if (!supportedTypes.Contains(request.Stage.OrchestratorType))
                    {
                        errors.Add($"Unsupported orchestrator type: {request.Stage.OrchestratorType}");
                    }
                }

                // Validate stage has execution strategy
                if (!request.Stage.StageTools.Any() && string.IsNullOrEmpty(request.Stage.ReActAgentType))
                {
                    errors.Add("Stage must have either tools or a ReAct agent");
                }

                // Validate execution ID
                if (string.IsNullOrEmpty(request.ExecutionId))
                {
                    errors.Add("ExecutionId is required");
                }

                // Validate stage tools configuration for ToolChainOrchestrator
                if (request.Stage.OrchestratorType == "ToolChainOrchestrator" && request.Stage.StageTools.Any())
                {
                    foreach (var stageTool in request.Stage.StageTools)
                    {
                        if (string.IsNullOrEmpty(stageTool.ToolId))
                        {
                            errors.Add($"Tool at order {stageTool.Order} has empty ToolId");
                        }

                        // Validate tool configuration JSON
                        if (!string.IsNullOrEmpty(stageTool.Configuration))
                        {
                            try
                            {
                                JsonDocument.Parse(stageTool.Configuration);
                            }
                            catch (JsonException)
                            {
                                errors.Add($"Tool {stageTool.ToolId} has invalid JSON configuration");
                            }
                        }

                        // Validate input mapping JSON
                        if (!string.IsNullOrEmpty(stageTool.InputMapping))
                        {
                            try
                            {
                                JsonSerializer.Deserialize<Dictionary<string, string>>(stageTool.InputMapping);
                            }
                            catch (JsonException)
                            {
                                errors.Add($"Tool {stageTool.ToolId} has invalid input mapping JSON");
                            }
                        }
                    }
                }

                // Validate timeout
                if (request.Stage.TimeoutSeconds <= 0)
                {
                    errors.Add("Stage timeout must be greater than 0");
                }
            }

            // If validation fails, throw specific exception
            if (errors.Any())
            {
                var stageId = request.Stage?.Id.ToString() ?? "unknown";
                var stageName = request.Stage?.Name ?? "unknown";
                var executionId = request.ExecutionId ?? "unknown";
                
                throw new StageValidationException(stageId, stageName, executionId, errors);
            }

            return await Task.FromResult(validationResult);
        }

        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = false,
                SupportsParallelExecution = true,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxConcurrentExecutions = 10,
                DefaultTimeout = TimeSpan.FromMinutes(10),
                SupportedToolCategories = new List<string> { "All" },
                SupportedModels = new List<string> { "All" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supports_react"] = true,
                    ["supports_tool_chain"] = true,
                    ["supports_conversation"] = true
                }
            };
        }
    }

    public class StageToolExecutionResultDto
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public bool Success { get; set; }
        public Dictionary<string, object> Result { get; set; }
        public double ExecutionTime { get; set; }
    }
}