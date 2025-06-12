using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace OptimalyAI.ViewModels
{
    /// <summary>
    /// Prototype ViewModels pro testování UI workflow designeru
    /// Data se ukládají pouze v ViewModelu - bez persistence
    /// </summary>
    
    public class ProjectListItemViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public int StageCount { get; set; }
        public string TriggerType { get; set; }
        public DateTime? NextRun { get; set; }
        public DateTime? LastRun { get; set; }
        public bool LastRunSuccess { get; set; }
        public int SuccessRate { get; set; }
        public int TotalRuns { get; set; }
        public string WorkflowType { get; set; }
    }
    
    public class WorkflowPrototypeViewModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = "Demo E-commerce Project";
        public string WorkflowType { get; set; } = "";
        public List<WorkflowStagePrototype> Stages { get; set; } = new();
        public string TriggerType { get; set; } = "Manual";
        public string Schedule { get; set; } = "";
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; } = "Demo User";
        public int WorkflowVersion { get; set; } = 1;
        public string DrawflowData { get; set; } = "";
    }

    public class WorkflowStagePrototype
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "Analysis";
        public string ExecutionStrategy { get; set; } = "Sequential";
        public List<string> Tools { get; set; } = new();
        public bool UseReAct { get; set; } = false;
        public int Order { get; set; }
        public string Status { get; set; } = "Draft";
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    public class CreateStagePrototypeViewModel
    {
        [Required]
        public string Name { get; set; } = "";
        
        public string Description { get; set; } = "";
        
        [Required]
        public string Type { get; set; } = "Analysis";
        
        [Required]
        public string ExecutionStrategy { get; set; } = "Sequential";
        
        public List<string> SelectedTools { get; set; } = new();
        
        public bool UseReAct { get; set; } = false;
        
        public Dictionary<string, object> AdvancedConfig { get; set; } = new();
        
        public Guid ProjectId { get; set; }
    }

    public class WorkflowTypeOption
    {
        public string Value { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public List<WorkflowStagePrototype> DefaultStages { get; set; } = new();
    }

    public static class WorkflowPrototypeData
    {
        public static List<WorkflowTypeOption> GetWorkflowTypes()
        {
            return new List<WorkflowTypeOption>
            {
                new()
                {
                    Value = "ecommerce_product_search",
                    Name = "🛒 E-commerce vyhledávání produktů",
                    Icon = "fas fa-shopping-cart",
                    Description = "Automatické vyhledávání podobných produktů podle fotek zákazníka",
                    DefaultStages = new List<WorkflowStagePrototype>
                    {
                        new() { Name = "Analýza vstupních fotek", Type = "Analysis", UseReAct = true, Tools = new() { "image_analyzer" }, Order = 1 },
                        new() { Name = "Vyhledávání produktů", Type = "Search", ExecutionStrategy = "Parallel", Tools = new() { "aliexpress_search", "web_search" }, Order = 2 },
                        new() { Name = "Filtrování výsledků", Type = "Processing", UseReAct = true, Tools = new() { "product_filter", "similarity_scorer" }, Order = 3 },
                        new() { Name = "Export dat", Type = "Export", Tools = new() { "excel_exporter" }, Order = 4 }
                    }
                },
                new()
                {
                    Value = "image_generation",
                    Name = "🎨 Generování a úprava obrázků",
                    Icon = "fas fa-paint-brush",
                    Description = "AI generování obrázků s následnou editací a optimalizací",
                    DefaultStages = new List<WorkflowStagePrototype>
                    {
                        new() { Name = "Úprava promptu", Type = "Analysis", UseReAct = true, Tools = new() { "prompt_enhancer" }, Order = 1 },
                        new() { Name = "Generování obrázku", Type = "Generation", ExecutionStrategy = "Conditional", Tools = new() { "dalle_generator", "midjourney_api" }, Order = 2 },
                        new() { Name = "Následné úpravy", Type = "Processing", Tools = new() { "image_upscaler", "background_remover" }, Order = 3 }
                    }
                },
                new()
                {
                    Value = "content_creation",
                    Name = "📝 Tvorba obsahu",
                    Icon = "fas fa-pen-fancy",
                    Description = "Komplexní tvorba obsahu od researche po publikaci",
                    DefaultStages = new List<WorkflowStagePrototype>
                    {
                        new() { Name = "Research", Type = "Search", ExecutionStrategy = "Parallel", Tools = new() { "web_search", "academic_search" }, Order = 1 },
                        new() { Name = "Plánování obsahu", Type = "Analysis", UseReAct = true, Tools = new() { "outline_generator" }, Order = 2 },
                        new() { Name = "Psaní textu", Type = "Generation", Tools = new() { "content_writer", "grammar_checker" }, Order = 3 },
                        new() { Name = "Review a publikace", Type = "Processing", UseReAct = true, Tools = new() { "quality_assessor", "publisher" }, Order = 4 }
                    }
                },
                new()
                {
                    Value = "data_analysis",
                    Name = "📊 Analýza dat",
                    Icon = "fas fa-chart-line",
                    Description = "Komplexní analýza dat s vizualizací a reportingem",
                    DefaultStages = new List<WorkflowStagePrototype>
                    {
                        new() { Name = "Import dat", Type = "Import", ExecutionStrategy = "Parallel", Tools = new() { "csv_reader", "api_fetcher", "database_connector" }, Order = 1 },
                        new() { Name = "Zpracování dat", Type = "Processing", Tools = new() { "data_cleaner", "transformer" }, Order = 2 },
                        new() { Name = "Analýza a vizualizace", Type = "Analysis", Tools = new() { "data_analyzer", "chart_generator" }, Order = 3 },
                        new() { Name = "Reporting", Type = "Export", Tools = new() { "report_generator", "dashboard_creator" }, Order = 4 }
                    }
                },
                new()
                {
                    Value = "chatbot_conversation",
                    Name = "💬 Chatbot konverzace",
                    Icon = "fas fa-comments",
                    Description = "Inteligentní chatbot s možností tool integration",
                    DefaultStages = new List<WorkflowStagePrototype>
                    {
                        new() { Name = "Konverzace s AI", Type = "Conversation", UseReAct = true, Tools = new() { "conversation_ai", "tool_detector" }, Order = 1 }
                    }
                },
                new()
                {
                    Value = "custom",
                    Name = "⚙️ Vlastní workflow",
                    Icon = "fas fa-cogs",
                    Description = "Vlastní workflow sestavený od nuly",
                    DefaultStages = new List<WorkflowStagePrototype>()
                }
            };
        }

        public static List<string> GetStageTypes()
        {
            return new List<string>
            {
                "Analysis", "Search", "Processing", "Export", "Import", 
                "Generation", "Conversation", "Custom"
            };
        }

        public static List<string> GetExecutionStrategies()
        {
            return new List<string>
            {
                "Sequential", "Parallel", "Conditional"
            };
        }

        public static List<string> GetAvailableTools()
        {
            return new List<string>
            {
                // Analysis tools
                "image_analyzer", "data_analyzer", "sentiment_analyzer", "quality_assessor",
                
                // Search tools  
                "web_search", "aliexpress_search", "alibaba_search", "academic_search",
                
                // Generation tools
                "content_writer", "image_generator", "dalle_generator", "midjourney_api",
                "prompt_enhancer", "outline_generator",
                
                // Processing tools
                "product_filter", "similarity_scorer", "data_cleaner", "transformer",
                "image_upscaler", "background_remover", "grammar_checker",
                
                // Export/Import tools
                "excel_exporter", "csv_reader", "database_connector", "api_fetcher",
                "report_generator", "dashboard_creator", "publisher",
                
                // Conversation tools
                "conversation_ai", "tool_detector"
            };
        }

        public static Dictionary<string, List<string>> GetToolsByCategory()
        {
            return new Dictionary<string, List<string>>
            {
                ["Analysis"] = new() { "image_analyzer", "data_analyzer", "sentiment_analyzer", "quality_assessor" },
                ["Search"] = new() { "web_search", "aliexpress_search", "alibaba_search", "academic_search" },
                ["Generation"] = new() { "content_writer", "image_generator", "dalle_generator", "midjourney_api", "prompt_enhancer" },
                ["Processing"] = new() { "product_filter", "similarity_scorer", "data_cleaner", "transformer", "image_upscaler" },
                ["Export"] = new() { "excel_exporter", "report_generator", "dashboard_creator", "publisher" },
                ["Import"] = new() { "csv_reader", "database_connector", "api_fetcher" },
                ["Conversation"] = new() { "conversation_ai", "tool_detector" }
            };
        }
    }
    
    // ===== NEW ROBUST WORKFLOW MODELS =====
    
    // Node types in the workflow
    public enum NodeType
    {
        Start,          // Entry point
        End,            // Exit point
        Task,           // Regular task/tool execution
        Condition,      // Conditional branching
        Parallel,       // Parallel split
        Join,           // Parallel join
        Loop,           // Loop construct
        Wait,           // Wait for event/time
        SubWorkflow     // Nested workflow
    }
    
    // Represents a node in the workflow graph
    public class WorkflowNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public NodeType Type { get; set; }
        public NodePosition Position { get; set; } = new(); // For visual editor
        
        // Task-specific properties
        public List<string> Tools { get; set; } = new();
        public bool UseReAct { get; set; }
        public string Orchestrator { get; set; } // ConversationOrchestrator, ToolChainOrchestrator, etc.
        
        // Input/Output definitions
        public List<NodePort> InputPorts { get; set; } = new();
        public List<NodePort> OutputPorts { get; set; } = new();
        
        // Condition-specific
        public string ConditionExpression { get; set; } // For conditional nodes
        
        // Loop-specific
        public string LoopCondition { get; set; }
        public int MaxIterations { get; set; } = 10;
        
        // Configuration
        public Dictionary<string, object> Configuration { get; set; } = new();
        public NodeStatus Status { get; set; } = NodeStatus.Draft;
    }
    
    // Node position for visual editor
    public class NodePosition
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
    
    // Node status
    public enum NodeStatus
    {
        Draft,
        Active,
        Running,
        Completed,
        Failed,
        Skipped
    }
    
    // Input/Output ports on nodes
    public class NodePort
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string DataType { get; set; } // string, number, object, array, file
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
    }
    
    // Represents an edge (connection) between nodes
    public class WorkflowEdge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string SourcePortId { get; set; }
        public string TargetPortId { get; set; }
        
        // Edge conditions (for conditional branches)
        public string Condition { get; set; } // e.g., "result == 'success'"
        public string Label { get; set; } // Visual label
        
        // Data transformation between nodes
        public string DataMapping { get; set; } // JSON mapping rules
    }
    
    // Enhanced workflow model using nodes and edges
    public class WorkflowGraphViewModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public List<WorkflowNode> Nodes { get; set; } = new();
        public List<WorkflowEdge> Edges { get; set; } = new();
        public WorkflowMetadata Metadata { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.Now;
        
        // Helper methods
        public WorkflowNode GetStartNode() => Nodes.FirstOrDefault(n => n.Type == NodeType.Start);
        public WorkflowNode GetEndNode() => Nodes.FirstOrDefault(n => n.Type == NodeType.End);
        public List<WorkflowNode> GetNextNodes(string nodeId) => 
            Edges.Where(e => e.SourceId == nodeId)
                .Select(e => Nodes.FirstOrDefault(n => n.Id == e.TargetId))
                .Where(n => n != null)
                .ToList();
    }
    
    // Workflow metadata
    public class WorkflowMetadata
    {
        public string Description { get; set; }
        public string Version { get; set; } = "1.0";
        public Dictionary<string, object> Variables { get; set; } = new(); // Global workflow variables
        public WorkflowTrigger Trigger { get; set; } = new();
        public WorkflowSettings Settings { get; set; } = new();
        
        // Store the orchestrator-compatible format
        public object OrchestratorData { get; set; }
        
        // Visual designer metadata
        public Dictionary<string, NodePosition> NodePositions { get; set; } = new();
    }
    
    // Workflow trigger configuration
    public class WorkflowTrigger
    {
        public string Type { get; set; } = "Manual"; // Manual, Schedule, Event, Webhook
        public string CronExpression { get; set; }
        public string EventName { get; set; }
        public Dictionary<string, object> Config { get; set; } = new();
    }
    
    // Workflow settings
    public class WorkflowSettings
    {
        public int MaxExecutionTime { get; set; } = 3600; // seconds
        public int MaxRetries { get; set; } = 3;
        public bool EnableDebugLogging { get; set; } = true;
        public string ErrorHandling { get; set; } = "StopOnError"; // StopOnError, ContinueOnError, Rollback
    }
}