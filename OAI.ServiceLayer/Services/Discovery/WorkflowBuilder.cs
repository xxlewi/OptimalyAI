using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Workflow;
using OAI.Core.Interfaces.Discovery;
using OAI.ServiceLayer.Services.Orchestration;

namespace OAI.ServiceLayer.Services.Discovery
{
    /// <summary>
    /// Builds and updates workflow definitions based on intent and matched components
    /// </summary>
    public class WorkflowBuilder : IWorkflowBuilder
    {
        private readonly ILogger<WorkflowBuilder> _logger;

        public WorkflowBuilder(ILogger<WorkflowBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<WorkflowDesignerDto> BuildWorkflowAsync(
            WorkflowIntent intent,
            List<ComponentMatch> components,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Building new workflow from {ComponentCount} components", components.Count);

            var workflow = new WorkflowDesignerDto
            {
                Name = GenerateWorkflowName(intent),
                Description = intent.UserMessage,
                Steps = new List<WorkflowStepDto>()
            };

            var stepPosition = 0;

            // 1. Add input adapter (trigger)
            var inputAdapter = components.FirstOrDefault(c => 
                c.Type == ComponentType.Adapter && 
                c.ComponentId.Contains("input"));
            
            if (inputAdapter != null)
            {
                workflow.Steps.Add(CreateAdapterStep(inputAdapter, stepPosition++, true));
            }
            else
            {
                // Default manual input
                workflow.Steps.Add(CreateDefaultInputStep(stepPosition++));
            }

            // 2. Add data collection tools
            foreach (var source in intent.DataSources)
            {
                var tool = components.FirstOrDefault(c => 
                    c.Type == ComponentType.Tool && 
                    c.CanHandle(source));
                
                if (tool != null)
                {
                    workflow.Steps.Add(CreateToolStep(tool, source, stepPosition++));
                }
            }

            // 3. Add processing steps (orchestrators or tools)
            if (intent.RequiresProcessing)
            {
                var orchestrator = components.FirstOrDefault(c => 
                    c.Type == ComponentType.Orchestrator);
                
                if (orchestrator != null)
                {
                    workflow.Steps.Add(CreateOrchestratorStep(orchestrator, intent.ProcessingRequirements, stepPosition++));
                }
                else
                {
                    // Add processing tools
                    var processingTools = components.Where(c => 
                        c.Type == ComponentType.Tool && 
                        (c.ComponentId.Contains("llm") || c.ComponentId.Contains("transform")));
                    
                    foreach (var tool in processingTools.Take(2)) // Limit to avoid too many steps
                    {
                        workflow.Steps.Add(CreateToolStep(tool, null, stepPosition++));
                    }
                }
            }

            // 4. Add output adapter
            var outputAdapter = components.FirstOrDefault(c => 
                c.Type == ComponentType.Adapter && 
                c.ComponentId.Contains("output"));
            
            if (outputAdapter != null)
            {
                workflow.Steps.Add(CreateAdapterStep(outputAdapter, stepPosition++, false));
            }
            else
            {
                // Default file output
                workflow.Steps.Add(CreateDefaultOutputStep(stepPosition++, intent.Outputs.FirstOrDefault()));
            }

            // Connect the steps
            ConnectWorkflowSteps(workflow.Steps);

            _logger.LogInformation("Built workflow with {StepCount} steps", workflow.Steps.Count);

            return Task.FromResult(workflow);
        }

        public async Task<WorkflowDesignerDto> UpdateWorkflowAsync(
            string currentWorkflowJson,
            WorkflowIntent intent,
            List<ComponentMatch> components,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Updating existing workflow");

            // Parse current workflow
            var workflow = JsonSerializer.Deserialize<WorkflowDesignerDto>(currentWorkflowJson);
            if (workflow == null)
            {
                // If parsing fails, create new workflow
                return await BuildWorkflowAsync(intent, components, cancellationToken);
            }

            // Find highest position
            var maxPosition = workflow.Steps.Any() ? workflow.Steps.Max(s => s.Position) : -1;
            var nextPosition = maxPosition + 1;

            // Add missing components based on intent
            if (intent.RequiresWebScraping && !workflow.Steps.Any(s => s.Tool?.Contains("firecrawl") == true))
            {
                var firecrawlTool = components.FirstOrDefault(c => 
                    c.Type == ComponentType.Tool && 
                    c.ComponentId.Contains("firecrawl"));
                
                if (firecrawlTool != null)
                {
                    workflow.Steps.Add(CreateToolStep(firecrawlTool, intent.DataSources.FirstOrDefault(), nextPosition++));
                }
            }

            if (intent.RequiresProcessing && !workflow.Steps.Any(s => s.Type == "orchestrator"))
            {
                var orchestrator = components.FirstOrDefault(c => c.Type == ComponentType.Orchestrator);
                if (orchestrator != null)
                {
                    workflow.Steps.Add(CreateOrchestratorStep(orchestrator, intent.ProcessingRequirements, nextPosition++));
                }
            }

            // Ensure output exists
            if (!workflow.Steps.Any(s => s.Type == "adapter" && s.AdapterType == "output"))
            {
                var outputAdapter = components.FirstOrDefault(c => 
                    c.Type == ComponentType.Adapter && 
                    c.ComponentId.Contains("output"));
                
                if (outputAdapter != null)
                {
                    workflow.Steps.Add(CreateAdapterStep(outputAdapter, nextPosition++, false));
                }
                else
                {
                    workflow.Steps.Add(CreateDefaultOutputStep(nextPosition++, intent.Outputs.FirstOrDefault()));
                }
            }

            // Re-connect steps
            ConnectWorkflowSteps(workflow.Steps);

            _logger.LogInformation("Updated workflow now has {StepCount} steps", workflow.Steps.Count);

            return workflow;
        }

        private string GenerateWorkflowName(WorkflowIntent intent)
        {
            if (intent.RequiresWebScraping)
            {
                return "Web Data Extraction Workflow";
            }
            else if (intent.RequiresProcessing)
            {
                return "Data Processing Workflow";
            }
            else if (intent.DataSources.Any(ds => ds.Type == "file"))
            {
                return "File Processing Workflow";
            }
            else
            {
                return "Custom Workflow";
            }
        }

        private WorkflowStepDto CreateAdapterStep(ComponentMatch adapter, int position, bool isInput)
        {
            return new WorkflowStepDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = adapter.ComponentName,
                Type = "adapter",
                AdapterType = isInput ? "input" : "output",
                AdapterId = adapter.ComponentId,
                Configuration = adapter.RequiredConfiguration ?? new Dictionary<string, object>(),
                Position = position,
                IsFinal = false
            };
        }

        private WorkflowStepDto CreateToolStep(ComponentMatch tool, DataSource? dataSource, int position)
        {
            var config = new Dictionary<string, object>(tool.RequiredConfiguration ?? new Dictionary<string, object>());
            
            if (dataSource?.Url != null && !config.ContainsKey("url"))
            {
                config["url"] = dataSource.Url;
            }

            return new WorkflowStepDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = tool.ComponentName,
                Type = "tool",
                Tool = tool.ComponentId,
                Configuration = config,
                Position = position,
                IsFinal = false
            };
        }

        private WorkflowStepDto CreateOrchestratorStep(ComponentMatch orchestrator, ProcessingRequirements requirements, int position)
        {
            var config = new Dictionary<string, object>(orchestrator.RequiredConfiguration ?? new Dictionary<string, object>());
            
            if (requirements.Operations.Any())
            {
                config["operations"] = requirements.Operations;
            }

            return new WorkflowStepDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = orchestrator.ComponentName,
                Type = "orchestrator",
                Configuration = config,
                Position = position,
                IsFinal = false
            };
        }

        private WorkflowStepDto CreateDefaultInputStep(int position)
        {
            return new WorkflowStepDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Manual Input",
                Type = "adapter",
                AdapterType = "input",
                AdapterId = "manual_input",
                Configuration = new Dictionary<string, object>
                {
                    ["prompt"] = "Enter input data"
                },
                Position = position,
                IsFinal = false
            };
        }

        private WorkflowStepDto CreateDefaultOutputStep(int position, OutputTarget? output)
        {
            var config = new Dictionary<string, object>
            {
                ["filename"] = "output",
                ["format"] = output?.Format ?? "json"
            };

            string adapterId = "json_file_output";
            string name = "JSON File Output";

            if (output?.Format == "csv")
            {
                adapterId = "file_output";
                name = "File Output";
            }
            else if (output?.Type == "api")
            {
                adapterId = "api_output";
                name = "API Output";
                config["endpoint"] = "";
            }

            return new WorkflowStepDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Type = "adapter",
                AdapterType = "output",
                AdapterId = adapterId,
                Configuration = config,
                Position = position,
                IsFinal = false
            };
        }

        private void ConnectWorkflowSteps(List<WorkflowStepDto> steps)
        {
            if (!steps.Any())
                return;

            // Sort by position
            steps = steps.OrderBy(s => s.Position).ToList();

            // Connect in sequence
            for (int i = 0; i < steps.Count - 1; i++)
            {
                var currentStep = steps[i];
                var nextStep = steps[i + 1];

                // Set next step
                currentStep.Next = nextStep.Id;
            }

            _logger.LogDebug("Connected {StepCount} workflow steps", steps.Count);
        }
    }
}