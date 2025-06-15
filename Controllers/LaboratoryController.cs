using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs.Workflow;
using OAI.ViewModels.Laboratory;

namespace OptimalyAI.Controllers
{
    public class LaboratoryController : Controller
    {
        public IActionResult WorkflowDesigner()
        {
            // Create demo workflow designer with mock data
            var viewModel = CreateDemoWorkflowDesignerViewModel();
            
            ViewData["Title"] = "Workflow Designer - Laboratory";
            return View(viewModel);
        }

        public IActionResult GoJSDemo()
        {
            // Create demo for GoJS workflow designer
            var viewModel = CreateDemoWorkflowDesignerViewModel();
            
            ViewData["Title"] = "GoJS Workflow Designer - Laboratory";
            return View(viewModel);
        }

        public IActionResult DrawIODemo()
        {
            // Create demo for Draw.io workflow designer
            var viewModel = CreateDemoWorkflowDesignerViewModel();
            
            ViewData["Title"] = "Draw.io Workflow Designer - Laboratory";
            return View(viewModel);
        }

        [HttpPost]
        public IActionResult SaveWorkflow([FromBody] WorkflowDefinition workflow)
        {
            // Demo implementation - just return success
            return Json(new { success = true, message = "Workflow saved successfully (demo mode)" });
        }

        [HttpPost]
        public IActionResult ValidateWorkflow([FromBody] WorkflowDefinition workflow)
        {
            // Demo validation - simulate validation
            var validationResult = new
            {
                isValid = true,
                errors = new List<string>(),
                warnings = new List<string> { "This is a demo validation" }
            };

            return Json(validationResult);
        }

        [HttpGet]
        public IActionResult GetAvailableTools()
        {
            // Return mock tools data
            var tools = GetDemoTools();
            return Json(tools);
        }

        [HttpGet]
        public IActionResult GetAvailableAdapters()
        {
            // Return mock adapters data
            var adapters = GetDemoAdapters();
            return Json(adapters);
        }

        private WorkflowDesignerViewModel CreateDemoWorkflowDesignerViewModel()
        {
            return new WorkflowDesignerViewModel
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Demo Project",
                WorkflowDefinition = CreateSampleWorkflow(),
                AvailableTools = GetDemoTools(),
                AvailableAdapters = GetDemoAdapters(),
                AvailableOrchestrators = GetDemoOrchestrators()
            };
        }

        private WorkflowDefinition CreateSampleWorkflow()
        {
            return new WorkflowDefinition
            {
                Name = "Sample Workflow",
                Description = "Demo workflow for testing JointJS designer",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Id = "start",
                        Name = "Start",
                        Type = "start",
                        Position = 1,
                        Next = "web_search"
                    },
                    new WorkflowStep
                    {
                        Id = "web_search", 
                        Name = "Web Search",
                        Type = "tool",
                        Tool = "web_search",
                        Configuration = new Dictionary<string, object>
                        {
                            ["query"] = "{{search_query}}",
                            ["max_results"] = 10
                        },
                        Position = 2,
                        Next = "decision"
                    },
                    new WorkflowStep
                    {
                        Id = "decision",
                        Name = "Check Results",
                        Type = "decision",
                        Position = 3,
                        Condition = "{{web_search.results_count}} > 0",
                        Branches = new WorkflowBranches
                        {
                            True = new List<string> { "process_results" },
                            False = new List<string> { "no_results" }
                        }
                    },
                    new WorkflowStep
                    {
                        Id = "process_results",
                        Name = "Process Results", 
                        Type = "tool",
                        Tool = "data_analyzer",
                        Configuration = new Dictionary<string, object>
                        {
                            ["data"] = "{{web_search.results}}",
                            ["analysis_type"] = "sentiment"
                        },
                        Position = 4,
                        Next = "end"
                    },
                    new WorkflowStep
                    {
                        Id = "no_results",
                        Name = "No Results",
                        Type = "tool",
                        Tool = "content_writer",
                        Configuration = new Dictionary<string, object>
                        {
                            ["prompt"] = "No results found for query: {{search_query}}"
                        },
                        Position = 5,
                        Next = "end"
                    },
                    new WorkflowStep
                    {
                        Id = "end",
                        Name = "End",
                        Type = "end",
                        Position = 6
                    }
                },
                FirstStepId = "start",
                LastStepIds = new List<string> { "end" }
            };
        }

        private List<ToolInfo> GetDemoTools()
        {
            return new List<ToolInfo>
            {
                new ToolInfo
                {
                    Id = "web_search",
                    Name = "Web Search",
                    Description = "Search the web for information",
                    Category = "Search",
                    Icon = "fas fa-search",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "query", Type = "string", Required = true, Description = "Search query" },
                        new ToolParameter { Name = "max_results", Type = "int", Required = false, Description = "Maximum number of results" }
                    }
                },
                new ToolInfo
                {
                    Id = "data_analyzer",
                    Name = "Data Analyzer", 
                    Description = "Analyze data using AI",
                    Category = "Analysis",
                    Icon = "fas fa-chart-bar",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "data", Type = "object", Required = true, Description = "Data to analyze" },
                        new ToolParameter { Name = "analysis_type", Type = "string", Required = true, Description = "Type of analysis" }
                    }
                },
                new ToolInfo
                {
                    Id = "content_writer",
                    Name = "Content Writer",
                    Description = "Generate content using AI",
                    Category = "Generation", 
                    Icon = "fas fa-pen",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "prompt", Type = "string", Required = true, Description = "Content prompt" },
                        new ToolParameter { Name = "style", Type = "string", Required = false, Description = "Writing style" }
                    }
                },
                new ToolInfo
                {
                    Id = "image_analyzer",
                    Name = "Image Analyzer",
                    Description = "Analyze images using AI",
                    Category = "Analysis",
                    Icon = "fas fa-image",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "image_url", Type = "string", Required = true, Description = "URL of image to analyze" }
                    }
                },
                new ToolInfo
                {
                    Id = "excel_exporter",
                    Name = "Excel Exporter",
                    Description = "Export data to Excel file",
                    Category = "Export",
                    Icon = "fas fa-file-excel",
                    Parameters = new List<ToolParameter>
                    {
                        new ToolParameter { Name = "data", Type = "object", Required = true, Description = "Data to export" },
                        new ToolParameter { Name = "filename", Type = "string", Required = false, Description = "Output filename" }
                    }
                }
            };
        }

        private List<AdapterInfo> GetDemoAdapters()
        {
            return new List<AdapterInfo>
            {
                new AdapterInfo
                {
                    Id = "file_reader",
                    Name = "File Reader",
                    Description = "Read data from files",
                    Type = "Input",
                    Icon = "fas fa-file-import",
                    Parameters = new List<AdapterParameter>
                    {
                        new AdapterParameter { Name = "file_path", Type = "string", Required = true, Description = "Path to input file" },
                        new AdapterParameter { Name = "format", Type = "string", Required = false, Description = "File format (auto-detect if not specified)" }
                    }
                },
                new AdapterInfo
                {
                    Id = "database_connector",
                    Name = "Database Connector",
                    Description = "Connect to database",
                    Type = "Bidirectional",
                    Icon = "fas fa-database",
                    Parameters = new List<AdapterParameter>
                    {
                        new AdapterParameter { Name = "connection_string", Type = "string", Required = true, Description = "Database connection string" },
                        new AdapterParameter { Name = "query", Type = "string", Required = true, Description = "SQL query to execute" }
                    }
                },
                new AdapterInfo
                {
                    Id = "api_connector",
                    Name = "API Connector",
                    Description = "Connect to REST APIs",
                    Type = "Bidirectional",
                    Icon = "fas fa-plug",
                    Parameters = new List<AdapterParameter>
                    {
                        new AdapterParameter { Name = "url", Type = "string", Required = true, Description = "API endpoint URL" },
                        new AdapterParameter { Name = "method", Type = "string", Required = false, Description = "HTTP method (GET, POST, etc.)" },
                        new AdapterParameter { Name = "headers", Type = "object", Required = false, Description = "HTTP headers" }
                    }
                }
            };
        }

        private List<OrchestratorInfo> GetDemoOrchestrators()
        {
            return new List<OrchestratorInfo>
            {
                new OrchestratorInfo
                {
                    Id = "react_orchestrator",
                    Name = "ReAct Orchestrator",
                    Description = "Reasoning and Acting orchestrator",
                    Icon = "fas fa-brain"
                },
                new OrchestratorInfo
                {
                    Id = "tool_chain_orchestrator", 
                    Name = "Tool Chain Orchestrator",
                    Description = "Sequential tool execution",
                    Icon = "fas fa-link"
                },
                new OrchestratorInfo
                {
                    Id = "conversation_orchestrator",
                    Name = "Conversation Orchestrator", 
                    Description = "Interactive conversation flow",
                    Icon = "fas fa-comments"
                }
            };
        }
    }
}