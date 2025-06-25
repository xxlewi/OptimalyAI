using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Services.Orchestration.ReAct;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller for managing and monitoring ReAct agents
    /// </summary>
    public class ReActAgentsController : Controller
    {
        private readonly ILogger<ReActAgentsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAgentMemory _agentMemory;
        private readonly IOrchestratorMetrics _metrics;
        private readonly IServiceProvider _serviceProvider;

        public ReActAgentsController(
            ILogger<ReActAgentsController> logger,
            IConfiguration configuration,
            IAgentMemory agentMemory,
            IOrchestratorMetrics metrics,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _agentMemory = agentMemory;
            _metrics = metrics;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Main dashboard showing all ReAct agents and their status
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var viewModel = new ReActAgentsDashboardViewModel
            {
                // Get configuration profiles
                Profiles = GetReActProfiles(),
                
                // Get current settings
                CurrentSettings = new ReActSettings
                {
                    Enabled = _configuration.GetValue<bool>("ReActSettings:Enabled", true),
                    MaxIterations = _configuration.GetValue<int>("ReActSettings:MaxIterations", 10),
                    ThoughtVisibility = _configuration.GetValue<string>("ReActSettings:ThoughtVisibility", "Full"),
                    AutoEnableForComplexQueries = _configuration.GetValue<bool>("ReActSettings:AutoEnableForComplexQueries", true),
                    DefaultModel = _configuration.GetValue<string>("ReActSettings:DefaultModel", "deepseek-coder:6.7b")
                },
                
                // Get available agents
                AvailableAgents = new List<AgentInfo>
                {
                    new AgentInfo
                    {
                        Id = "universal",
                        Name = "Universal ReAct Agent",
                        Type = "UniversalReActAgent",
                        Description = "Univerzální agent pro všechny typy úloh - coding, konverzace, workflow",
                        Status = "Active",
                        Capabilities = new[] { "Coding", "File Operations", "Web Search", "Tool Execution", "Multi-step Reasoning", "Dynamic Model Selection" }
                    }
                },
                
                // Get recent executions from memory
                RecentExecutions = await GetRecentExecutions()
            };

            // Get metrics if available
            if (_metrics != null)
            {
                var timeRange = OAI.Core.Interfaces.Orchestration.TimeRange.LastDay;
                var allMetrics = await _metrics.GetAllMetricsAsync(timeRange);
                
                // Aggregate metrics from all orchestrators
                if (allMetrics != null && allMetrics.Any())
                {
                    var totalExecutions = allMetrics.Sum(m => m.TotalExecutions);
                    var successfulExecutions = allMetrics.Sum(m => m.SuccessfulExecutions);
                    var avgExecutionTime = allMetrics.Average(m => m.AverageExecutionTime.TotalMilliseconds);
                    
                    viewModel.Metrics = new AgentMetrics
                    {
                        TotalExecutions = totalExecutions,
                        SuccessRate = totalExecutions > 0 ? (double)successfulExecutions / totalExecutions : 0,
                        AverageExecutionTime = TimeSpan.FromMilliseconds(avgExecutionTime),
                        ToolUsageRate = CalculateToolUsageRate(allMetrics)
                    };
                }
            }

            return View(viewModel);
        }

        /// <summary>
        /// Show details of a specific ReAct execution
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExecutionDetails(string executionId)
        {
            if (string.IsNullOrEmpty(executionId))
                return NotFound();

            // Try to get execution from memory
            var scratchpad = await _agentMemory.GetScratchpadAsync(executionId);
            
            if (scratchpad == null)
                return NotFound();
                
            var thoughts = scratchpad.Thoughts;
            var actions = scratchpad.Actions;
            var observations = scratchpad.Observations;

            if (!thoughts.Any() && !actions.Any() && !observations.Any())
                return NotFound();

            var viewModel = new ExecutionDetailsViewModel
            {
                ExecutionId = executionId,
                Thoughts = thoughts.ToList(),
                Actions = actions.ToList(),
                Observations = observations.ToList(),
                Timeline = BuildExecutionTimeline(thoughts, actions, observations)
            };

            return View(viewModel);
        }

        /// <summary>
        /// Live monitoring view for active ReAct executions
        /// </summary>
        public IActionResult Monitor()
        {
            return View();
        }

        /// <summary>
        /// Configuration page for ReAct settings
        /// </summary>
        public IActionResult Configure()
        {
            var viewModel = new ReActConfigurationViewModel
            {
                Settings = new ReActSettings
                {
                    Enabled = _configuration.GetValue<bool>("ReActSettings:Enabled", true),
                    MaxIterations = _configuration.GetValue<int>("ReActSettings:MaxIterations", 3),
                    ThoughtVisibility = _configuration.GetValue<string>("ReActSettings:ThoughtVisibility", "Full"),
                    AutoEnableForComplexQueries = _configuration.GetValue<bool>("ReActSettings:AutoEnableForComplexQueries", true),
                    DefaultModel = _configuration.GetValue<string>("ReActSettings:DefaultModel", "llama3.2"),
                    TimeoutSeconds = _configuration.GetValue<int>("ReActSettings:TimeoutSeconds", 30),
                    EnableParallelTools = _configuration.GetValue<bool>("ReActSettings:EnableParallelTools", false)
                },
                AvailableModels = new[] { "llama3.2", "llama3.1", "mistral", "phi3" },
                ThoughtVisibilityOptions = new[] { "Full", "Summary", "None" }
            };

            return View(viewModel);
        }

        /// <summary>
        /// API endpoint to get active executions for monitoring
        /// </summary>
        [HttpGet]
        [Route("api/react/executions/active")]
        public async Task<IActionResult> GetActiveExecutions()
        {
            // This would need to be implemented with proper tracking
            // For now, return empty list
            var activeExecutions = new List<ActiveExecutionDto>();
            
            return Json(activeExecutions);
        }

        /// <summary>
        /// API endpoint to get execution timeline
        /// </summary>
        [HttpGet]
        [Route("api/react/executions/{executionId}/timeline")]
        public async Task<IActionResult> GetExecutionTimeline(string executionId)
        {
            var scratchpad = await _agentMemory.GetScratchpadAsync(executionId);
            
            if (scratchpad == null)
                return NotFound();
                
            var timeline = BuildExecutionTimeline(scratchpad.Thoughts, scratchpad.Actions, scratchpad.Observations);
            
            return Json(timeline);
        }

        #region Private Helper Methods

        private Dictionary<string, ReActProfile> GetReActProfiles()
        {
            var profiles = new Dictionary<string, ReActProfile>();
            
            // Default profile
            profiles["default"] = new ReActProfile
            {
                Name = "Universal",
                Description = "Univerzální profil pro všechny typy úloh",
                MaxIterations = 10,
                Model = "deepseek-coder:6.7b",
                Temperature = 0.3
            };

            // Load custom profiles from configuration if available
            var customProfiles = _configuration.GetSection("ReActSettings:Profiles");
            if (customProfiles.Exists())
            {
                foreach (var profile in customProfiles.GetChildren())
                {
                    profiles[profile.Key] = new ReActProfile
                    {
                        Name = profile["Name"] ?? profile.Key,
                        Description = profile["Description"] ?? "",
                        MaxIterations = profile.GetValue<int>("MaxIterations", 3),
                        Model = profile["Model"] ?? "llama3.2",
                        Temperature = profile.GetValue<double>("Temperature", 0.7)
                    };
                }
            }

            return profiles;
        }

        private async Task<List<RecentExecution>> GetRecentExecutions(int limit = 10)
        {
            // This is a simplified version - in production, you'd want to store this properly
            var executions = new List<RecentExecution>();
            
            // For now, return empty list
            // In real implementation, this would query stored execution data
            
            return executions;
        }

        private List<TimelineEvent> BuildExecutionTimeline(
            IEnumerable<AgentThought> thoughts,
            IEnumerable<AgentAction> actions,
            IEnumerable<AgentObservation> observations)
        {
            var timeline = new List<TimelineEvent>();

            foreach (var thought in thoughts)
            {
                timeline.Add(new TimelineEvent
                {
                    Type = "thought",
                    Timestamp = thought.CreatedAt,
                    StepNumber = thought.StepNumber,
                    Title = "Myšlení",
                    Content = thought.Content,
                    Icon = "fas fa-brain",
                    Color = "info"
                });
            }

            foreach (var action in actions)
            {
                timeline.Add(new TimelineEvent
                {
                    Type = "action",
                    Timestamp = action.CreatedAt,
                    StepNumber = action.StepNumber,
                    Title = action.IsFinalAnswer ? "Finální odpověď" : $"Akce: {action.ToolName}",
                    Content = action.IsFinalAnswer ? action.FinalAnswer : $"Volání nástroje {action.ToolName}",
                    Icon = action.IsFinalAnswer ? "fas fa-flag-checkered" : "fas fa-tools",
                    Color = action.IsFinalAnswer ? "success" : "warning"
                });
            }

            foreach (var observation in observations)
            {
                timeline.Add(new TimelineEvent
                {
                    Type = "observation",
                    Timestamp = observation.CreatedAt,
                    StepNumber = observation.StepNumber,
                    Title = $"Výsledek: {observation.ToolName}",
                    Content = observation.Content?.Length > 200 
                        ? observation.Content.Substring(0, 200) + "..." 
                        : observation.Content,
                    Icon = observation.IsSuccess ? "fas fa-check-circle" : "fas fa-times-circle",
                    Color = observation.IsSuccess ? "success" : "danger",
                    IsSuccess = observation.IsSuccess
                });
            }

            return timeline.OrderBy(e => e.Timestamp).ToList();
        }

        private double CalculateToolUsageRate(IList<OAI.Core.Interfaces.Orchestration.OrchestratorMetricsData> allMetrics)
        {
            if (allMetrics == null || !allMetrics.Any())
                return 0;

            var totalExecutions = allMetrics.Sum(m => m.TotalExecutions);
            if (totalExecutions == 0)
                return 0;

            // Calculate tool usage rate from available data
            var totalToolUsage = allMetrics.Sum(m => m.ToolUsageCount?.Values.Sum() ?? 0);
            
            if (totalToolUsage > 0)
            {
                return (double)totalToolUsage / totalExecutions;
            }
            
            return 0.65; // Mock 65% tool usage rate if no data available
        }

        #endregion
    }

    #region ViewModels

    public class ReActAgentsDashboardViewModel
    {
        public Dictionary<string, ReActProfile> Profiles { get; set; } = new();
        public ReActSettings CurrentSettings { get; set; }
        public List<AgentInfo> AvailableAgents { get; set; } = new();
        public List<RecentExecution> RecentExecutions { get; set; } = new();
        public AgentMetrics Metrics { get; set; }
    }

    public class ReActProfile
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int MaxIterations { get; set; }
        public string Model { get; set; }
        public double Temperature { get; set; }
    }

    public class ReActSettings
    {
        public bool Enabled { get; set; }
        public int MaxIterations { get; set; }
        public string ThoughtVisibility { get; set; }
        public bool AutoEnableForComplexQueries { get; set; }
        public string DefaultModel { get; set; }
        public int TimeoutSeconds { get; set; }
        public bool EnableParallelTools { get; set; }
    }

    public class AgentInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string[] Capabilities { get; set; }
    }

    public class RecentExecution
    {
        public string ExecutionId { get; set; }
        public DateTime StartedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public string Input { get; set; }
        public string Result { get; set; }
        public int Steps { get; set; }
        public bool Success { get; set; }
        public int ToolsUsed { get; set; }
    }

    public class AgentMetrics
    {
        public int TotalExecutions { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public double ToolUsageRate { get; set; }
    }

    public class ExecutionDetailsViewModel
    {
        public string ExecutionId { get; set; }
        public List<AgentThought> Thoughts { get; set; } = new();
        public List<AgentAction> Actions { get; set; } = new();
        public List<AgentObservation> Observations { get; set; } = new();
        public List<TimelineEvent> Timeline { get; set; } = new();
    }

    public class TimelineEvent
    {
        public string Type { get; set; }
        public DateTime Timestamp { get; set; }
        public int StepNumber { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }
        public bool? IsSuccess { get; set; }
    }

    public class ReActConfigurationViewModel
    {
        public ReActSettings Settings { get; set; }
        public string[] AvailableModels { get; set; }
        public string[] ThoughtVisibilityOptions { get; set; }
    }

    public class ActiveExecutionDto
    {
        public string ExecutionId { get; set; }
        public string Input { get; set; }
        public DateTime StartedAt { get; set; }
        public int CurrentStep { get; set; }
        public string CurrentPhase { get; set; }
        public double Progress { get; set; }
    }

    #endregion
}