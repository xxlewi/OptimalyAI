using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Discovery;
using OAI.ServiceLayer.Services.Orchestration;
using OAI.ServiceLayer.Services.Discovery;

namespace OptimalyAI.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time Discovery Orchestrator communication
    /// Provides live workflow building and intent analysis
    /// </summary>
    public class DiscoveryHub : Hub
    {
        private readonly ILogger<DiscoveryHub> _logger;
        private readonly DiscoveryOrchestrator _discoveryOrchestrator;
        private readonly IStepTestExecutor _stepTestExecutor;
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
        private static readonly ConcurrentDictionary<string, string> _userSessions = new();

        public DiscoveryHub(
            ILogger<DiscoveryHub> logger,
            DiscoveryOrchestrator discoveryOrchestrator,
            IStepTestExecutor stepTestExecutor)
        {
            _logger = logger;
            _discoveryOrchestrator = discoveryOrchestrator;
            _stepTestExecutor = stepTestExecutor;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Discovery client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation("Discovery client disconnected: {ConnectionId}", Context.ConnectionId);
            
            // Clean up any active cancellation tokens
            if (_cancellationTokens.TryRemove(Context.ConnectionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            
            // Clean up session mapping
            _userSessions.TryRemove(Context.ConnectionId, out _);
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join a discovery session for a specific project
        /// </summary>
        public async Task JoinDiscoverySession(string projectId, string? sessionId = null)
        {
            var actualSessionId = sessionId ?? Guid.NewGuid().ToString();
            _userSessions[Context.ConnectionId] = actualSessionId;
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"discovery-{projectId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{actualSessionId}");
            
            _logger.LogInformation("Client {ConnectionId} joined discovery session {SessionId} for project {ProjectId}", 
                Context.ConnectionId, actualSessionId, projectId);
                
            await Clients.Caller.SendAsync("DiscoverySessionJoined", new
            {
                sessionId = actualSessionId,
                projectId = projectId,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Leave discovery session
        /// </summary>
        public async Task LeaveDiscoverySession(string projectId, string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"discovery-{projectId}");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sessionId}");
            
            _userSessions.TryRemove(Context.ConnectionId, out _);
            
            _logger.LogInformation("Client {ConnectionId} left discovery session {SessionId} for project {ProjectId}", 
                Context.ConnectionId, sessionId, projectId);
                
            await Clients.Caller.SendAsync("DiscoverySessionLeft", new
            {
                sessionId = sessionId,
                projectId = projectId,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Discover workflow from natural language message
        /// </summary>
        public async Task DiscoverWorkflow(string projectId, string message, string? currentWorkflowJson = null, Dictionary<string, object>? context = null)
        {
            // Create cancellation token for this request
            var cts = new CancellationTokenSource();
            _cancellationTokens[Context.ConnectionId] = cts;
            
            try
            {
                _logger.LogInformation("DiscoverWorkflow called - ProjectId: {ProjectId}, Message: {Message}", 
                    projectId, message);
                
                // Get or create session ID
                var sessionId = _userSessions.GetValueOrDefault(Context.ConnectionId, Guid.NewGuid().ToString());
                
                // Notify that we're processing
                await Clients.Caller.SendAsync("DiscoveryProcessingStarted", new
                {
                    sessionId = sessionId,
                    projectId = projectId,
                    message = message,
                    timestamp = DateTime.UtcNow
                });

                // Create discovery request
                var discoveryRequest = new DiscoveryChatRequestDto
                {
                    Message = message,
                    ProjectId = Guid.TryParse(projectId, out var parsedProjectId) ? parsedProjectId : Guid.Empty,
                    CurrentWorkflowJson = currentWorkflowJson,
                    Context = context ?? new Dictionary<string, object>(),
                    SessionId = sessionId
                };

                // Create orchestrator context
                var orchestratorContext = new OAI.ServiceLayer.Services.Orchestration.Base.OrchestratorContext(
                    Context.UserIdentifier ?? "anonymous",
                    sessionId)
                {
                    ExecutionTimeout = TimeSpan.FromMinutes(2)
                };

                // Subscribe to orchestrator events for real-time updates
                orchestratorContext.OnToolExecutionStarted += async (sender, args) =>
                {
                    await Clients.Caller.SendAsync("IntentAnalysisStarted", new
                    {
                        sessionId = sessionId,
                        step = "intent_analysis",
                        timestamp = DateTime.UtcNow
                    });
                };
                
                orchestratorContext.OnToolExecutionCompleted += async (sender, args) =>
                {
                    await Clients.Caller.SendAsync("ComponentMatchingStarted", new
                    {
                        sessionId = sessionId,
                        step = "component_matching",
                        timestamp = DateTime.UtcNow
                    });
                };

                // Send progress updates
                await Clients.Caller.SendAsync("WorkflowBuildingStarted", new
                {
                    sessionId = sessionId,
                    step = "workflow_building",
                    timestamp = DateTime.UtcNow
                });

                // Execute discovery orchestration
                _logger.LogInformation("Executing Discovery Orchestrator for message: {Message}", message);
                var orchestratorResult = await _discoveryOrchestrator.ExecuteAsync(
                    discoveryRequest, 
                    orchestratorContext,
                    cts.Token);

                if (!orchestratorResult.IsSuccess || orchestratorResult.Data == null)
                {
                    _logger.LogError("Discovery Orchestrator failed: {Error}", orchestratorResult.Error?.Message);
                    await Clients.Caller.SendAsync("DiscoveryError", new
                    {
                        sessionId = sessionId,
                        error = orchestratorResult.Error?.Message ?? "Discovery failed",
                        timestamp = DateTime.UtcNow
                    });
                    return;
                }

                var discoveryResponse = orchestratorResult.Data;

                // Send workflow suggestion with progress
                await Clients.Caller.SendAsync("WorkflowSuggestionReceived", new
                {
                    sessionId = sessionId,
                    projectId = projectId,
                    response = discoveryResponse,
                    timestamp = DateTime.UtcNow
                });

                // Send individual workflow updates for live building
                if (discoveryResponse.WorkflowUpdates != null)
                {
                    foreach (var update in discoveryResponse.WorkflowUpdates)
                    {
                        await Clients.Caller.SendAsync("WorkflowStepAdded", new
                        {
                            sessionId = sessionId,
                            update = update,
                            timestamp = DateTime.UtcNow
                        });
                        
                        // Small delay to show progressive building
                        await Task.Delay(200, cts.Token);
                    }
                }

                // Send completion notification
                await Clients.Caller.SendAsync("DiscoveryCompleted", new
                {
                    sessionId = sessionId,
                    projectId = projectId,
                    confidence = discoveryResponse.WorkflowSuggestion?.Confidence ?? 0,
                    stepsCount = discoveryResponse.WorkflowSuggestion?.WorkflowDefinition?.Steps?.Count ?? 0,
                    requiredToolsCount = discoveryResponse.WorkflowSuggestion?.RequiredTools?.Count ?? 0,
                    timestamp = DateTime.UtcNow
                });

                // Notify group about new discovery activity
                await Clients.Group($"discovery-{projectId}").SendAsync("DiscoveryActivityUpdate", new
                {
                    sessionId = sessionId,
                    message = message,
                    confidence = discoveryResponse.WorkflowSuggestion?.Confidence ?? 0,
                    timestamp = DateTime.UtcNow
                });

            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Discovery workflow cancelled");
                await Clients.Caller.SendAsync("DiscoveryCancelled", new
                {
                    sessionId = _userSessions.GetValueOrDefault(Context.ConnectionId),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DiscoverWorkflow");
                await Clients.Caller.SendAsync("DiscoveryError", new
                {
                    sessionId = _userSessions.GetValueOrDefault(Context.ConnectionId),
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
            finally
            {
                // Clean up cancellation token
                if (_cancellationTokens.TryRemove(Context.ConnectionId, out var removedCts))
                {
                    removedCts.Dispose();
                }
            }
        }

        /// <summary>
        /// Stop current discovery process
        /// </summary>
        public async Task StopDiscovery()
        {
            _logger.LogInformation("Client {ConnectionId} requested to stop discovery", Context.ConnectionId);
            
            // Cancel the active discovery
            if (_cancellationTokens.TryGetValue(Context.ConnectionId, out var cts))
            {
                cts.Cancel();
                await Clients.Caller.SendAsync("DiscoveryStopped", new
                {
                    sessionId = _userSessions.GetValueOrDefault(Context.ConnectionId),
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Request available components for building workflow
        /// </summary>
        public async Task RequestAvailableComponents()
        {
            try
            {
                // This would typically call the component matcher to get available tools/adapters
                await Clients.Caller.SendAsync("AvailableComponentsReceived", new
                {
                    tools = new[] { "web_search", "firecrawl_scraper", "jina_reader", "llm_tornado" },
                    adapters = new[] { "manual_input", "database_output", "csv_output", "json_output", "email_output" },
                    orchestrators = new[] { "conversation_orchestrator", "tool_chain_orchestrator", "web_scraping_orchestrator" },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting available components");
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }

        /// <summary>
        /// Save discovered workflow to project
        /// </summary>
        public async Task SaveDiscoveredWorkflow(string projectId, string workflowJson, string workflowName)
        {
            try
            {
                var sessionId = _userSessions.GetValueOrDefault(Context.ConnectionId);
                
                // This would typically save the workflow to the database
                // For now, just acknowledge the save request
                await Clients.Caller.SendAsync("WorkflowSaved", new
                {
                    sessionId = sessionId,
                    projectId = projectId,
                    workflowName = workflowName,
                    timestamp = DateTime.UtcNow
                });
                
                _logger.LogInformation("Workflow '{WorkflowName}' saved for project {ProjectId} by session {SessionId}", 
                    workflowName, projectId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving discovered workflow");
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }

        /// <summary>
        /// Test execution of an individual workflow step
        /// </summary>
        public async Task TestWorkflowStep(TestStepRequestDto request)
        {
            var sessionId = _userSessions.GetValueOrDefault(Context.ConnectionId);
            
            try
            {
                _logger.LogInformation("Testing step {StepId} of type {StepType} for session {SessionId}", 
                    request.StepId, request.StepType, sessionId);
                
                // Notify test started
                await Clients.Caller.SendAsync("StepTestStarted", new
                {
                    sessionId = sessionId,
                    stepId = request.StepId,
                    stepType = request.StepType,
                    timestamp = DateTime.UtcNow
                });

                // Execute step test
                var result = await _stepTestExecutor.TestStepAsync(request);

                // Send detailed test results
                await Clients.Caller.SendAsync("StepTestCompleted", new
                {
                    sessionId = sessionId,
                    stepId = request.StepId,
                    stepType = request.StepType,
                    result = result,
                    timestamp = DateTime.UtcNow
                });

                // Send performance metrics if available
                if (result.Performance != null)
                {
                    await Clients.Caller.SendAsync("StepPerformanceMetrics", new
                    {
                        sessionId = sessionId,
                        stepId = request.StepId,
                        performance = result.Performance,
                        timestamp = DateTime.UtcNow
                    });
                }

                // Send validation results
                if (result.Validation != null)
                {
                    await Clients.Caller.SendAsync("StepValidationResults", new
                    {
                        sessionId = sessionId,
                        stepId = request.StepId,
                        validation = result.Validation,
                        timestamp = DateTime.UtcNow
                    });
                }

                // Send suggestions if available
                if (result.Suggestions?.Count > 0)
                {
                    await Clients.Caller.SendAsync("StepSuggestions", new
                    {
                        sessionId = sessionId,
                        stepId = request.StepId,
                        suggestions = result.Suggestions,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing workflow step {StepId}", request.StepId);
                await Clients.Caller.SendAsync("StepTestError", new
                {
                    sessionId = sessionId,
                    stepId = request.StepId,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Get step testing status and metrics
        /// </summary>
        public async Task GetStepTestingStatus()
        {
            try
            {
                var sessionId = _userSessions.GetValueOrDefault(Context.ConnectionId);
                
                await Clients.Caller.SendAsync("StepTestingStatusReceived", new
                {
                    sessionId = sessionId,
                    available = true,
                    supportedStepTypes = new[] { "tool", "adapter", "orchestrator" },
                    maxTimeoutSeconds = 300,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting step testing status");
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }
    }
}