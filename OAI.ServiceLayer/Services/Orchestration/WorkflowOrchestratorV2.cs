using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Attributes;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Tools;
using OAI.Core.DTOs.Workflow;
using OAI.Core.Entities;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.AI;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Base;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Intelligent workflow orchestrator that executes complete workflows with AI-driven validation and retry
    /// Uses configured AI server (Ollama or LM Studio) based on orchestrator configuration
    /// </summary>
    [OrchestratorMetadata(
        id: "workflow_orchestrator_v2",
        name: "WorkflowOrchestratorV2",
        description: "Intelligent workflow orchestrator with configured AI server support",
        IsWorkflowNode = false,
        IsEnabledByDefault = true,
        Tags = new[] { "workflow", "execution", "ai", "automation" },
        RequestTypeName = "OAI.Core.DTOs.Orchestration.WorkflowOrchestratorRequest",
        ResponseTypeName = "OAI.Core.DTOs.Orchestration.WorkflowOrchestratorResponse"
    )]
    public class WorkflowOrchestratorV2 : BaseOrchestrator<WorkflowOrchestratorRequest, WorkflowOrchestratorResponse>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IToolExecutor _toolExecutor;
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly IOrchestratorConfigurationService _orchestratorConfigService;
        private readonly OAI.ServiceLayer.Services.AI.IAiServerService _aiServerService;
        private readonly IOllamaService _ollamaService;
        private readonly ILMStudioService _lmStudioService;
        private readonly new ILogger<WorkflowOrchestratorV2> _logger;

        // Cached configuration
        private OrchestratorConfigurationDto _cachedConfig;
        private DateTime _configCacheTime = DateTime.MinValue;
        private readonly TimeSpan _configCacheDuration = TimeSpan.FromMinutes(5);

        // Static capabilities for metadata-based discovery
        public static OrchestratorCapabilities StaticCapabilities { get; } = new OrchestratorCapabilities
        {
            SupportsReActPattern = true,
            SupportsToolCalling = true,
            SupportsMultiModal = false,
            MaxIterations = 50,
            SupportedInputTypes = new[] { "workflow", "json" },
            SupportedOutputTypes = new[] { "execution_result", "json" }
        };

        public WorkflowOrchestratorV2(
            IUnitOfWork unitOfWork,
            IToolExecutor toolExecutor,
            IAdapterRegistry adapterRegistry,
            IOrchestratorConfigurationService orchestratorConfigService,
            OAI.ServiceLayer.Services.AI.IAiServerService aiServerService,
            IOllamaService ollamaService,
            ILMStudioService lmStudioService,
            ILogger<WorkflowOrchestratorV2> logger,
            IOrchestratorMetrics metrics,
            IServiceProvider serviceProvider)
            : base(logger, metrics, serviceProvider)
        {
            _unitOfWork = unitOfWork;
            _toolExecutor = toolExecutor;
            _adapterRegistry = adapterRegistry;
            _orchestratorConfigService = orchestratorConfigService;
            _aiServerService = aiServerService;
            _ollamaService = ollamaService;
            _lmStudioService = lmStudioService;
            _logger = logger;
        }

        public override string Id => "workflow_orchestrator_v2";
        public override string Name => "WorkflowOrchestratorV2";
        public override string Description => "Intelligent workflow orchestrator with configured AI server support";

        protected override async Task<WorkflowOrchestratorResponse> ExecuteCoreAsync(
            WorkflowOrchestratorRequest request,
            IOrchestratorContext context,
            OrchestratorResult<WorkflowOrchestratorResponse> result,
            CancellationToken cancellationToken)
        {
            var response = new WorkflowOrchestratorResponse
            {
                WorkflowId = request.WorkflowId,
                ExecutionId = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // Get orchestrator configuration
                var config = await GetOrchestratorConfigurationAsync();
                
                // Load workflow if only ID provided
                var definition = request.WorkflowDefinition;
                if (definition == null && request.WorkflowId != Guid.Empty)
                {
                    definition = await LoadWorkflowDefinitionAsync(request.WorkflowId);
                }

                if (definition == null)
                {
                    throw new InvalidOperationException("No workflow definition provided");
                }

                // Initialize execution context with configured model
                var executionContext = new WorkflowExecutionContext
                {
                    WorkflowId = request.WorkflowId,
                    ExecutionId = response.ExecutionId,
                    Variables = new Dictionary<string, object>(request.InitialParameters),
                    AIModel = request.AIModel ?? config?.DefaultModelName ?? "qwen2.5-14b-instruct",
                    AiServerType = config?.AiServerType?.ToString() ?? "Ollama"
                };

                _logger.LogInformation("Executing workflow with AI server: {ServerType}, Model: {Model}", 
                    executionContext.AiServerType, executionContext.AIModel);

                // Build execution plan
                var executionPlan = BuildExecutionPlan(definition);
                _logger.LogInformation("Built execution plan with {StepCount} steps", executionPlan.Count);

                // Execute each step
                foreach (var step in executionPlan)
                {
                    var stepResult = await ExecuteStepWithIntelligenceAsync(
                        step, 
                        definition, 
                        executionContext, 
                        config,
                        request.EnableIntelligentRetry,
                        request.MaxRetries,
                        cancellationToken);

                    response.StepResults.Add(stepResult);

                    if (!stepResult.Success)
                    {
                        response.Status = WorkflowOrchestratorExecutionStatus.Failed;
                        response.ErrorMessage = $"Step {step.Id} failed: {stepResult.Error}";
                        break;
                    }

                    // Update context with step outputs
                    if (stepResult.Output != null)
                    {
                        executionContext.Variables[$"{step.Id}_output"] = stepResult.Output;
                    }
                }

                // Generate AI summary if all steps completed
                if (response.Status != WorkflowOrchestratorExecutionStatus.Failed)
                {
                    response.Status = WorkflowOrchestratorExecutionStatus.Completed;
                    response.FinalOutputs = ExtractFinalOutputs(executionContext, definition);
                    response.AIGuidanceSummary = await GenerateExecutionSummaryAsync(
                        definition, response.StepResults, executionContext, config);
                }

                response.Success = response.Status == WorkflowOrchestratorExecutionStatus.Completed;
                response.CompletedAt = DateTime.UtcNow;
                response.DurationMs = (response.CompletedAt - response.StartedAt).TotalMilliseconds;

                // Save execution to database
                await SaveExecutionResultAsync(request.WorkflowId, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow {WorkflowId}", request.WorkflowId);
                response.Status = WorkflowOrchestratorExecutionStatus.Failed;
                response.Success = false;
                response.ErrorMessage = ex.Message;
                response.CompletedAt = DateTime.UtcNow;
                response.DurationMs = (response.CompletedAt - response.StartedAt).TotalMilliseconds;
            }

            return response;
        }

        private async Task<OrchestratorConfigurationDto> GetOrchestratorConfigurationAsync()
        {
            // Check cache first
            if (_cachedConfig != null && DateTime.UtcNow - _configCacheTime < _configCacheDuration)
            {
                return _cachedConfig;
            }

            try
            {
                var config = await _orchestratorConfigService.GetByOrchestratorIdAsync(Id);
                if (config != null)
                {
                    _cachedConfig = config;
                    _configCacheTime = DateTime.UtcNow;
                    
                    // Enrich with AI server info
                    if (config.AiServerId.HasValue)
                    {
                        var aiServer = await _aiServerService.GetByIdAsync(config.AiServerId.Value);
                        if (aiServer != null)
                        {
                            config.AiServerType = aiServer.ServerType;
                            config.AiServerName = aiServer.Name;
                        }
                    }
                }
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get orchestrator configuration");
                return null;
            }
        }

        private async Task<string> GenerateWithConfiguredAIAsync(
            string prompt, 
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config,
            CancellationToken cancellationToken)
        {
            string response = null;
            
            // Use configured AI server
            if (config?.AiServerType == OAI.Core.Entities.AiServerType.LMStudio && _lmStudioService.IsAvailable)
            {
                try
                {
                    _logger.LogDebug("Using LM Studio for generation with model {Model}", context.AIModel);
                    var lmResponse = await _lmStudioService.GenerateAsync(new GenerateRequest
                    {
                        Model = context.AIModel,
                        Prompt = prompt,
                        Options = new GenerateOptions
                        {
                            Temperature = 0.7f,
                            MaxTokens = 2000
                        }
                    }, cancellationToken);
                    response = lmResponse.Response;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LM Studio generation failed, falling back to Ollama");
                }
            }
            
            // Default to Ollama if LM Studio not configured or failed
            if (string.IsNullOrEmpty(response))
            {
                _logger.LogDebug("Using Ollama for generation with model {Model}", context.AIModel);
                response = await _ollamaService.GenerateAsync(context.AIModel, prompt, cancellationToken);
            }
            
            return response;
        }

        private async Task<StepExecutionResult> ExecuteStepWithIntelligenceAsync(
            WorkflowStep step,
            WorkflowDefinition workflow,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config,
            bool enableIntelligentRetry,
            int maxRetries,
            CancellationToken cancellationToken)
        {
            var result = new StepExecutionResult
            {
                StepId = step.Id,
                StepName = step.Name,
                StartedAt = DateTime.UtcNow
            };

            int attemptCount = 0;
            Exception lastException = null;

            while (attemptCount <= maxRetries)
            {
                try
                {
                    attemptCount++;
                    _logger.LogDebug("Executing step {StepName} (attempt {Attempt})", step.Name, attemptCount);

                    // Execute based on step type
                    var stepOutput = await ExecuteStepByTypeAsync(step, context, config, cancellationToken);

                    // Validate output with AI if enabled
                    if (enableIntelligentRetry && !string.IsNullOrEmpty(step.Description))
                    {
                        var validationResult = await ValidateStepOutputAsync(
                            step, stepOutput, context, config, attemptCount == 1);

                        if (!validationResult.IsValid)
                        {
                            if (attemptCount < maxRetries)
                            {
                                _logger.LogWarning("Step {StepName} output validation failed: {Reason}. Retrying with AI guidance.",
                                    step.Name, validationResult.Reason);

                                // Apply AI suggestions for retry
                                await ApplyAISuggestionsAsync(step, context, validationResult);
                                continue;
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    $"Output validation failed after {maxRetries} attempts: {validationResult.Reason}");
                            }
                        }
                    }

                    // Success
                    result.Success = true;
                    result.Output = stepOutput;
                    result.CompletedAt = DateTime.UtcNow;
                    result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
                    result.AttemptCount = attemptCount;
                    
                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError(ex, "Error executing step {StepName} (attempt {Attempt})", 
                        step.Name, attemptCount);

                    if (attemptCount >= maxRetries)
                    {
                        break;
                    }

                    if (enableIntelligentRetry)
                    {
                        // Get AI guidance for retry
                        var retryGuidance = await GetAIRetryGuidanceAsync(step, ex, context, config);
                        if (retryGuidance.ShouldRetry)
                        {
                            await ApplyRetryGuidanceAsync(step, context, retryGuidance);
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attemptCount)), cancellationToken);
                            continue;
                        }
                    }
                    
                    break;
                }
            }

            // Failed after all attempts
            result.Success = false;
            result.Error = lastException?.Message ?? "Unknown error";
            result.CompletedAt = DateTime.UtcNow;
            result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
            result.AttemptCount = attemptCount;
            
            return result;
        }

        private async Task<object> ExecuteStepByTypeAsync(
            WorkflowStep step,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config,
            CancellationToken cancellationToken)
        {
            // Resolve parameters with context variables
            var resolvedParams = ResolveParameters(step.Configuration, context.Variables);

            switch (step.Type?.ToLower())
            {
                case "tool":
                    return await ExecuteToolStepAsync(step, resolvedParams, cancellationToken);
                    
                case "ai-conversation":
                    return await ExecuteAIConversationAsync(step, resolvedParams, context, config, cancellationToken);
                    
                case "orchestrator":
                    return await ExecuteOrchestratorStepAsync(step, resolvedParams, context, config, cancellationToken);
                    
                case "decision":
                    return await ExecuteDecisionStepAsync(step, resolvedParams, context);
                    
                case "input-adapter":
                case "output-adapter":
                    return await ExecuteAdapterStepAsync(step, resolvedParams, cancellationToken);
                    
                case "parallel-gateway":
                    return await ExecuteParallelStepsAsync(step, context, config, cancellationToken);
                    
                default:
                    throw new NotSupportedException($"Step type '{step.Type}' is not supported");
            }
        }

        private async Task<object> ExecuteToolStepAsync(
            WorkflowStep step,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            var toolId = step.Tool ?? throw new InvalidOperationException("Tool ID is required for tool steps");
            
            var executionContext = new ToolExecutionContext();

            var toolResult = await _toolExecutor.ExecuteToolAsync(toolId, parameters, executionContext, cancellationToken);
            
            if (!toolResult.IsSuccess)
            {
                throw new InvalidOperationException($"Tool execution failed: {toolResult.Error?.Message ?? "Unknown error"}");
            }

            return toolResult.Data;
        }

        private async Task<object> ExecuteAIConversationAsync(
            WorkflowStep step,
            Dictionary<string, object> parameters,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config,
            CancellationToken cancellationToken)
        {
            var prompt = parameters.GetValueOrDefault("prompt")?.ToString() 
                ?? throw new InvalidOperationException("Prompt is required for AI conversation steps");

            // Build context-aware prompt
            var contextualPrompt = BuildContextualPrompt(prompt, context);

            var aiResponse = await GenerateWithConfiguredAIAsync(contextualPrompt, context, config, cancellationToken);

            return new
            {
                response = aiResponse,
                model = context.AIModel,
                serverType = context.AiServerType
            };
        }

        private async Task<object> ExecuteOrchestratorStepAsync(
            WorkflowStep step,
            Dictionary<string, object> parameters,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("🤖 ORCHESTRATOR ANALYSIS STARTING for step '{StepName}'", step.Name);
            _logger.LogInformation("📊 Current context variables: {Variables}", string.Join(", ", context.Variables.Keys));
            
            // Analyze workflow flow and next steps
            await AnalyzeWorkflowFlowAsync(step, context);

            // Smart orchestration: Detect if next steps need images and we have URLs
            var processedData = await ProcessDataForNextStepsAsync(context, step, cancellationToken);

            // Build orchestrator prompt from context
            var contextPrompt = $@"
Analyze the following workflow data and provide orchestration decisions:

Step: {step.Name}
Description: {step.Description ?? "AI Orchestrator"}

Previous step outputs:
{JsonSerializer.Serialize(context.Variables, new JsonSerializerOptions { WriteIndented = true })}

Processed data:
{JsonSerializer.Serialize(processedData, new JsonSerializerOptions { WriteIndented = true })}

Configuration:
{JsonSerializer.Serialize(parameters, new JsonSerializerOptions { WriteIndented = true })}

Based on this information, provide analysis and decisions for the next workflow steps.
Return structured data that can be used by subsequent steps.";

            var aiResponse = await GenerateWithConfiguredAIAsync(contextPrompt, context, config, cancellationToken);

            // Update context with processed data (especially downloaded images)
            if (processedData.ContainsKey("downloadedImages"))
            {
                context.Variables["orchestrator_images"] = processedData["downloadedImages"];
                _logger.LogInformation("Added {Count} downloaded images to context as 'orchestrator_images'", 
                    ((List<Dictionary<string, object>>)processedData["downloadedImages"]).Count);
            }

            return new
            {
                orchestratorResponse = aiResponse,
                contextSummary = context.Variables.Keys.ToList(),
                processedData = processedData,
                model = context.AIModel,
                serverType = context.AiServerType,
                stepAnalysis = "Orchestrator step completed successfully with smart data processing"
            };
        }

        private async Task<Dictionary<string, object>> ProcessDataForNextStepsAsync(
            WorkflowExecutionContext context, 
            WorkflowStep currentStep,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, object>();

            try
            {
                // Look for image URLs in previous step outputs
                var imageUrls = ExtractImageUrlsFromContext(context);
                
                if (imageUrls.Any())
                {
                    _logger.LogInformation("Found {Count} image URLs, downloading for next steps...", imageUrls.Count);
                    
                    var downloadedImages = await DownloadImagesAsync(imageUrls, cancellationToken);
                    
                    if (downloadedImages.Any())
                    {
                        result["downloadedImages"] = downloadedImages;
                        result["imageCount"] = downloadedImages.Count;
                        _logger.LogInformation("Successfully downloaded {Count} images", downloadedImages.Count);
                    }
                }

                // Include original context data
                foreach (var kvp in context.Variables)
                {
                    if (!result.ContainsKey(kvp.Key))
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing data for next steps");
                result["processingError"] = ex.Message;
            }

            return result;
        }

        private List<string> ExtractImageUrlsFromContext(WorkflowExecutionContext context)
        {
            var imageUrls = new List<string>();

            foreach (var variable in context.Variables)
            {
                var urls = ExtractUrlsFromValue(variable.Value);
                imageUrls.AddRange(urls.Where(IsImageUrl));
            }

            return imageUrls.Distinct().ToList();
        }

        private List<string> ExtractUrlsFromValue(object value)
        {
            var urls = new List<string>();

            if (value is string str)
            {
                // Find URLs in string using regex
                var urlPattern = @"https?://[^\s<>\""]+\.(jpg|jpeg|png|gif|webp|bmp|tiff)(?:\?[^\s<>\""]*)?";
                var matches = System.Text.RegularExpressions.Regex.Matches(str, urlPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    urls.Add(match.Value);
                }
            }
            else if (value != null)
            {
                // Try to serialize and search in JSON
                try
                {
                    var json = JsonSerializer.Serialize(value);
                    return ExtractUrlsFromValue(json);
                }
                catch
                {
                    // Ignore serialization errors
                }
            }

            return urls;
        }

        private bool IsImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            var lowerUrl = url.ToLowerInvariant();
            return lowerUrl.Contains(".jpg") || lowerUrl.Contains(".jpeg") || 
                   lowerUrl.Contains(".png") || lowerUrl.Contains(".gif") || 
                   lowerUrl.Contains(".webp") || lowerUrl.Contains(".bmp") || 
                   lowerUrl.Contains(".tiff");
        }

        private async Task<List<Dictionary<string, object>>> DownloadImagesAsync(
            List<string> imageUrls, 
            CancellationToken cancellationToken)
        {
            var downloadedImages = new List<Dictionary<string, object>>();
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "OptimalyAI-Orchestrator/1.0");

            var semaphore = new SemaphoreSlim(5, 5); // Limit concurrent downloads

            var downloadTasks = imageUrls.Select(async url =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await DownloadSingleImageAsync(httpClient, url, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(downloadTasks);
            downloadedImages.AddRange(results.Where(r => r != null));

            return downloadedImages;
        }

        private async Task<Dictionary<string, object>> DownloadSingleImageAsync(
            HttpClient httpClient, 
            string url, 
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Downloading image from: {Url}", url);
                
                var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                return new Dictionary<string, object>
                {
                    ["url"] = url,
                    ["imageData"] = imageBytes,
                    ["contentType"] = contentType,
                    ["size"] = imageBytes.Length,
                    ["downloadedAt"] = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image from {Url}", url);
                return null;
            }
        }

        private async Task<ValidationResult> ValidateStepOutputAsync(
            WorkflowStep step,
            object output,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config,
            bool isFirstAttempt)
        {
            var validationPrompt = $@"
You are validating the output of a workflow step.

Step: {step.Name}
Expected Output: {step.Description}
Actual Output: {JsonSerializer.Serialize(output)}
Context: {JsonSerializer.Serialize(context.Variables)}

Analyze if the actual output meets the expected criteria. Consider:
1. Data completeness
2. Format correctness
3. Business logic requirements
4. Any specific validation rules mentioned

Respond in JSON format:
{{
  ""isValid"": true/false,
  ""reason"": ""explanation if invalid"",
  ""suggestions"": [""list of suggestions for retry if invalid""]
}}";

            var response = await GenerateWithConfiguredAIAsync(validationPrompt, context, config, CancellationToken.None);

            try
            {
                return JsonSerializer.Deserialize<ValidationResult>(response);
            }
            catch
            {
                // Fallback to simple validation
                return new ValidationResult { IsValid = true };
            }
        }

        private async Task<RetryGuidance> GetAIRetryGuidanceAsync(
            WorkflowStep step,
            Exception exception,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config)
        {
            var guidancePrompt = $@"
A workflow step failed with an error. Analyze and provide retry guidance.

Step: {step.Name}
Step Type: {step.Type}
Parameters: {JsonSerializer.Serialize(step.Configuration)}
Error: {exception.Message}
Stack Trace: {exception.StackTrace}
Current Context: {JsonSerializer.Serialize(context.Variables)}

Should we retry? If yes, what adjustments should be made?

Respond in JSON format:
{{
  ""shouldRetry"": true/false,
  ""reason"": ""explanation"",
  ""parameterAdjustments"": {{""key"": ""new value""}},
  ""contextUpdates"": {{""key"": ""new value""}},
  ""waitSeconds"": number
}}";

            var response = await GenerateWithConfiguredAIAsync(guidancePrompt, context, config, CancellationToken.None);

            try
            {
                return JsonSerializer.Deserialize<RetryGuidance>(response);
            }
            catch
            {
                return new RetryGuidance { ShouldRetry = false };
            }
        }

        private async Task<string> GenerateExecutionSummaryAsync(
            WorkflowDefinition workflow,
            List<StepExecutionResult> stepResults,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config)
        {
            var summaryPrompt = $@"
Summarize the workflow execution results.

Workflow: {workflow.Name}
Description: {workflow.Description}

Step Results:
{JsonSerializer.Serialize(stepResults.Select(r => new 
{
    r.StepName,
    r.Success,
    r.AttemptCount,
    r.DurationMs,
    OutputSummary = r.Output?.ToString()?.Substring(0, Math.Min(200, r.Output.ToString().Length))
}))}

Final Context:
{JsonSerializer.Serialize(context.Variables.Take(10))}

Provide a concise summary of:
1. What was accomplished
2. Key results and outputs
3. Any issues encountered and how they were resolved
4. Recommendations for improvement

Keep it under 200 words.";

            return await GenerateWithConfiguredAIAsync(summaryPrompt, context, config, CancellationToken.None);
        }

        private List<WorkflowStep> BuildExecutionPlan(WorkflowDefinition definition)
        {
            // Simple topological sort for now
            // TODO: Implement proper dependency resolution
            return definition.Steps.OrderBy(s => s.Position).ToList();
        }

        private Dictionary<string, object> ResolveParameters(
            Dictionary<string, object> parameters,
            Dictionary<string, object> variables)
        {
            if (parameters == null) return new Dictionary<string, object>();

            var resolved = new Dictionary<string, object>();
            foreach (var param in parameters)
            {
                resolved[param.Key] = ResolveValue(param.Value, variables);
            }
            return resolved;
        }

        private object ResolveValue(object value, Dictionary<string, object> variables)
        {
            if (value is string strValue && strValue.StartsWith("{{") && strValue.EndsWith("}}"))
            {
                var variableName = strValue.Substring(2, strValue.Length - 4).Trim();
                return variables.GetValueOrDefault(variableName) ?? value;
            }
            return value;
        }

        private string BuildContextualPrompt(string prompt, WorkflowExecutionContext context)
        {
            var contextInfo = context.Variables.Any() 
                ? $"\n\nContext Information:\n{JsonSerializer.Serialize(context.Variables, new JsonSerializerOptions { WriteIndented = true })}\n\n"
                : "";

            return $"{prompt}{contextInfo}";
        }

        private async Task<WorkflowDefinition> LoadWorkflowDefinitionAsync(Guid workflowId)
        {
            var repo = _unitOfWork.GetGuidRepository<ProjectWorkflow>();
            var workflow = await repo.GetByIdAsync(workflowId);
            
            if (workflow == null) return null;
            
            return JsonSerializer.Deserialize<WorkflowDefinition>(workflow.StepsDefinition);
        }

        private Dictionary<string, object> ExtractFinalOutputs(
            WorkflowExecutionContext context,
            WorkflowDefinition definition)
        {
            var outputs = new Dictionary<string, object>();
            
            // Extract outputs from output adapters
            foreach (var step in definition.Steps.Where(s => s.Type == "output-adapter"))
            {
                var outputKey = $"{step.Id}_output";
                if (context.Variables.ContainsKey(outputKey))
                {
                    outputs[step.Name] = context.Variables[outputKey];
                }
            }

            // Include final step output if no output adapters
            if (!outputs.Any() && definition.Steps.Any())
            {
                var lastStep = definition.Steps.Last();
                var outputKey = $"{lastStep.Id}_output";
                if (context.Variables.ContainsKey(outputKey))
                {
                    outputs["final_output"] = context.Variables[outputKey];
                }
            }

            return outputs;
        }

        private async Task SaveExecutionResultAsync(Guid workflowId, WorkflowOrchestratorResponse response)
        {
            var execution = new ProjectExecution
            {
                WorkflowId = workflowId,
                ExecutionType = "Workflow",
                Status = response.Status == WorkflowOrchestratorExecutionStatus.Completed 
                    ? ExecutionStatus.Completed 
                    : ExecutionStatus.Failed,
                StartedAt = response.StartedAt,
                CompletedAt = response.CompletedAt,
                DurationSeconds = (int)(response.DurationMs / 1000),
                InputParameters = JsonSerializer.Serialize(response.StepResults.FirstOrDefault()?.Output),
                OutputData = JsonSerializer.Serialize(response.FinalOutputs),
                ErrorMessage = response.ErrorMessage,
                ExecutionLog = JsonSerializer.Serialize(response.StepResults)
            };

            var repo = _unitOfWork.GetGuidRepository<ProjectExecution>();
            await repo.AddAsync(execution);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task ApplyAISuggestionsAsync(
            WorkflowStep step,
            WorkflowExecutionContext context,
            ValidationResult validationResult)
        {
            // Apply suggestions from AI validation
            if (validationResult.Suggestions?.Any() == true)
            {
                foreach (var suggestion in validationResult.Suggestions)
                {
                    _logger.LogInformation("Applying AI suggestion: {Suggestion}", suggestion);
                    // Parse and apply suggestions
                    // This could modify step parameters or context variables
                }
            }
        }

        private async Task ApplyRetryGuidanceAsync(
            WorkflowStep step,
            WorkflowExecutionContext context,
            RetryGuidance guidance)
        {
            // Apply parameter adjustments
            if (guidance.ParameterAdjustments != null)
            {
                foreach (var adjustment in guidance.ParameterAdjustments)
                {
                    if (step.Configuration.ContainsKey(adjustment.Key))
                    {
                        step.Configuration[adjustment.Key] = adjustment.Value;
                    }
                }
            }

            // Apply context updates
            if (guidance.ContextUpdates != null)
            {
                foreach (var update in guidance.ContextUpdates)
                {
                    context.Variables[update.Key] = update.Value;
                }
            }

            // Wait if specified
            if (guidance.WaitSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(guidance.WaitSeconds));
            }
        }

        private async Task<object> ExecuteDecisionStepAsync(
            WorkflowStep step,
            Dictionary<string, object> parameters,
            WorkflowExecutionContext context)
        {
            var condition = parameters.GetValueOrDefault("condition")?.ToString();
            if (string.IsNullOrEmpty(condition))
            {
                throw new InvalidOperationException("Decision steps require a condition");
            }

            // Simple condition evaluation for now
            // TODO: Implement proper expression evaluation
            var result = EvaluateSimpleCondition(condition, context.Variables);
            
            return new
            {
                decision = result,
                nextStepId = result 
                    ? parameters.GetValueOrDefault("trueStepId")?.ToString()
                    : parameters.GetValueOrDefault("falseStepId")?.ToString()
            };
        }

        private async Task<object> ExecuteAdapterStepAsync(
            WorkflowStep step,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing adapter step '{StepName}' (ID: {StepId}, Type: {StepType}, AdapterId: {AdapterId})", 
                step.Name, step.Id, step.Type, step.AdapterId ?? "NULL");
                
            if (string.IsNullOrEmpty(step.AdapterId))
            {
                _logger.LogError("Adapter step '{StepName}' has no AdapterId. Available adapters need to be configured in workflow designer.", step.Name);
                throw new InvalidOperationException($"Adapter ID is required for step '{step.Name}'. Please configure the adapter in workflow designer.");
            }
            
            var adapterId = step.AdapterId;
            var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
            
            if (adapter == null)
            {
                throw new InvalidOperationException($"Adapter '{adapterId}' not found");
            }

            var adapterContext = new AdapterExecutionContext
            {
                Configuration = parameters,
                ExecutionId = Guid.NewGuid().ToString()
            };

            var result = await adapter.ExecuteAsync(adapterContext, cancellationToken);
            
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Adapter execution failed: {result.Error?.Message ?? "Unknown error"}");
            }

            return result.Data;
        }

        private async Task<object> ExecuteParallelStepsAsync(
            WorkflowStep gateway,
            WorkflowExecutionContext context,
            OrchestratorConfigurationDto config,
            CancellationToken cancellationToken)
        {
            // TODO: Implement parallel execution
            throw new NotImplementedException("Parallel execution not yet implemented");
        }

        private bool EvaluateSimpleCondition(string condition, Dictionary<string, object> variables)
        {
            // Very simple evaluation - just check if a variable exists and is truthy
            // TODO: Implement proper expression evaluation
            if (variables.TryGetValue(condition, out var value))
            {
                return value switch
                {
                    bool b => b,
                    string s => !string.IsNullOrEmpty(s),
                    int i => i != 0,
                    _ => value != null
                };
            }
            return false;
        }

        public override async Task<OrchestratorValidationResult> ValidateAsync(WorkflowOrchestratorRequest request)
        {
            var result = new OrchestratorValidationResult { IsValid = true };
            var errors = new List<string>();

            if (request == null)
            {
                errors.Add("Request cannot be null");
            }
            else
            {
                if (request.WorkflowId == Guid.Empty && request.WorkflowDefinition == null)
                {
                    errors.Add("Either WorkflowId or WorkflowDefinition must be provided");
                }

                if (request.MaxRetries < 0)
                {
                    errors.Add("MaxRetries must be non-negative");
                }
            }

            if (errors.Any())
            {
                result.IsValid = false;
                result.Errors = errors;
            }

            return result;
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
                DefaultTimeout = TimeSpan.FromMinutes(30),
                SupportedToolCategories = new List<string> { "All" },
                SupportedModels = new List<string> { "qwen2.5-14b-instruct", "llama3.2", "mistral", "gemma" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supports_intelligent_retry"] = true,
                    ["supports_ai_validation"] = true,
                    ["supports_context_aware_execution"] = true,
                    ["supports_all_step_types"] = true,
                    ["supports_configured_ai_server"] = true
                }
            };
        }

        private async Task AnalyzeWorkflowFlowAsync(WorkflowStep currentStep, WorkflowExecutionContext context)
        {
            try
            {
                _logger.LogInformation("🔍 WORKFLOW FLOW ANALYSIS:");
                
                // Analyze current context data
                _logger.LogInformation("📋 CONTEXT DATA ANALYSIS:");
                foreach (var variable in context.Variables)
                {
                    var dataType = GetDataTypeDescription(variable.Value);
                    _logger.LogInformation("   📝 {Key}: {DataType}", variable.Key, dataType);
                    
                    if (variable.Value is string str && str.Contains("http") && IsImageUrl(str))
                    {
                        _logger.LogInformation("   🔗 Found image URL in data: {Key}", variable.Key);
                    }
                }
                
                _logger.LogInformation("🤖 Orchestrator will now analyze data and decide next actions...");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during workflow flow analysis");
            }
        }

        private string GetDataTypeDescription(object value)
        {
            if (value == null) return "null";
            
            var type = value.GetType();
            
            if (type == typeof(string))
            {
                var str = (string)value;
                if (str.StartsWith("http")) return "URL";
                if (str.Length > 100) return $"Large text ({str.Length} chars)";
                return $"Text ({str.Length} chars)";
            }
            
            if (type == typeof(byte[])) return $"Binary data ({((byte[])value).Length} bytes)";
            if (type.IsArray) return $"Array ({((Array)value).Length} items)";
            if (value is System.Collections.IDictionary dict) return $"Dictionary ({dict.Count} items)";
            if (value is System.Collections.IEnumerable enumerable) return $"Collection ({enumerable.Cast<object>().Count()} items)";
            
            return type.Name;
        }
    }

    // Supporting classes
    public class WorkflowExecutionContext
    {
        public Guid WorkflowId { get; set; }
        public Guid ExecutionId { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        public string AIModel { get; set; }
        public string AiServerType { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; }
        public List<string> Suggestions { get; set; }
    }

    public class RetryGuidance
    {
        public bool ShouldRetry { get; set; }
        public string Reason { get; set; }
        public Dictionary<string, object> ParameterAdjustments { get; set; }
        public Dictionary<string, object> ContextUpdates { get; set; }
        public int WaitSeconds { get; set; }
    }
}