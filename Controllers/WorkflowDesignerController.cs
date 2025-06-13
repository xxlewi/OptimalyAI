using Microsoft.AspNetCore.Mvc;
using OptimalyAI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Robustnější workflow designer s podporou pro DAG a vizuální editor
    /// </summary>
    public class WorkflowDesignerController : Controller
    {
        // In-memory storage pro demo
        private static Dictionary<Guid, WorkflowGraphViewModel> _workflows = new();
        
        [HttpGet]
        public IActionResult Index(Guid projectId)
        {
            // Získej nebo vytvoř workflow
            if (!_workflows.ContainsKey(projectId))
            {
                _workflows[projectId] = CreateDefaultWorkflow(projectId);
            }
            
            var workflow = _workflows[projectId];
            
            ViewBag.ProjectId = projectId;
            ViewBag.AvailableTools = WorkflowPrototypeData.GetAvailableTools();
            ViewBag.ToolsByCategory = WorkflowPrototypeData.GetToolsByCategory();
            ViewBag.Orchestrators = new List<string> 
            { 
                "ConversationOrchestrator", 
                "ToolChainOrchestrator", 
                "ReActOrchestrator",
                "CustomOrchestrator" 
            };
            
            // Použít Simple Workflow Designer - vlastní implementace
            return View("SimpleWorkflowDesigner", workflow);
        }
        
        [HttpGet]
        public IActionResult LoadWorkflow(Guid projectId)
        {
            WorkflowGraphViewModel workflow;
            
            if (_workflows.ContainsKey(projectId))
            {
                workflow = _workflows[projectId];
            }
            else
            {
                // Create a basic workflow if none exists
                workflow = CreateDefaultWorkflow(projectId);
                _workflows[projectId] = workflow;
            }
            
            // Return the workflow data for visualization
            return Json(new { 
                success = true, 
                workflow = new
                {
                    projectId = workflow.ProjectId,
                    projectName = workflow.ProjectName,
                    nodes = workflow.Nodes.Select(n => new
                    {
                        id = n.Id,
                        name = n.Name,
                        type = n.Type.ToString(),
                        position = n.Position,
                        tools = n.Tools
                    }),
                    edges = workflow.Edges.Select(e => new
                    {
                        id = e.Id,
                        source = e.SourceId,
                        target = e.TargetId
                    }),
                    metadata = workflow.Metadata
                }
            });
        }
        
        private WorkflowGraphViewModel CreateDefaultWorkflow(Guid projectId)
        {
            var workflow = new WorkflowGraphViewModel
            {
                ProjectId = projectId,
                ProjectName = "Nový projekt",
                Metadata = new WorkflowMetadata
                {
                    Description = "Workflow pro zpracování požadavků",
                    Settings = new WorkflowSettings()
                }
            };
            
            // Prázdné workflow - uživatel si vytvoří vlastní uzly
            
            return workflow;
        }
        
        [HttpGet]
        public IActionResult GetWorkflow(Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                return Json(new { success = true, workflow = _workflows[projectId] });
            }
            
            return Json(new { success = false, message = "Workflow nenalezeno" });
        }
        
        [HttpPost]
        public IActionResult SaveWorkflow([FromBody] SaveWorkflowRequest request)
        {
            if (request == null || request.ProjectId == Guid.Empty)
            {
                return Json(new { success = false, message = "Neplatná data" });
            }
            
            try
            {
                // Get existing workflow or create new one
                WorkflowGraphViewModel workflow;
                if (_workflows.ContainsKey(request.ProjectId))
                {
                    workflow = _workflows[request.ProjectId];
                }
                else
                {
                    workflow = new WorkflowGraphViewModel
                    {
                        ProjectId = request.ProjectId,
                        ProjectName = "Workflow " + request.ProjectId,
                        Nodes = new List<WorkflowNode>(),
                        Edges = new List<WorkflowEdge>(),
                        Metadata = new WorkflowMetadata()
                    };
                }
                
                // Update workflow data
                workflow.Metadata.OrchestratorData = request.WorkflowData;
                workflow.Metadata.Description = "Visual workflow";
                workflow.LastModified = DateTime.Now;
                
                // Parse WorkflowData to update nodes and edges
                if (request.WorkflowData != null)
                {
                    var workflowDataJson = System.Text.Json.JsonSerializer.Serialize(request.WorkflowData);
                    var orchestratorData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(workflowDataJson);
                    
                    // Clear existing nodes and edges
                    workflow.Nodes.Clear();
                    workflow.Edges.Clear();
                    
                    // Recreate nodes from orchestrator data
                    if (orchestratorData != null)
                    {
                        var jsonElement = (System.Text.Json.JsonElement)orchestratorData;
                        
                        // Get node positions from metadata
                        var nodePositions = new Dictionary<string, NodePosition>();
                        if (jsonElement.TryGetProperty("metadata", out var metadata) && 
                            metadata.TryGetProperty("nodePositions", out var positions))
                        {
                            foreach (var pos in positions.EnumerateObject())
                            {
                                if (pos.Value.TryGetProperty("x", out var x) && 
                                    pos.Value.TryGetProperty("y", out var y))
                                {
                                    nodePositions[pos.Name] = new NodePosition 
                                    { 
                                        X = x.GetDouble(), 
                                        Y = y.GetDouble() 
                                    };
                                }
                            }
                        }
                        
                        // Create nodes from steps
                        if (jsonElement.TryGetProperty("steps", out var steps))
                        {
                            foreach (var step in steps.EnumerateArray())
                            {
                                if (step.TryGetProperty("id", out var id) &&
                                    step.TryGetProperty("name", out var name) &&
                                    step.TryGetProperty("type", out var type))
                                {
                                    var nodeId = id.GetString();
                                    var node = new WorkflowNode
                                    {
                                        Id = nodeId,
                                        Name = name.GetString(),
                                        Type = MapStepTypeToNodeType(type.GetString()),
                                        Position = nodePositions.ContainsKey(nodeId) ? nodePositions[nodeId] : new NodePosition { X = 100, Y = 100 }
                                    };
                                    
                                    // Add tools if present
                                    if (step.TryGetProperty("tools", out var tools))
                                    {
                                        node.Tools = new List<string>();
                                        foreach (var tool in tools.EnumerateArray())
                                        {
                                            node.Tools.Add(tool.GetString());
                                        }
                                    }
                                    
                                    workflow.Nodes.Add(node);
                                }
                            }
                        }
                        
                        // Create edges from step connections
                        if (jsonElement.TryGetProperty("steps", out var stepsForEdges))
                        {
                            foreach (var step in stepsForEdges.EnumerateArray())
                            {
                                if (step.TryGetProperty("id", out var fromId))
                                {
                                    var fromIdStr = fromId.GetString();
                                    
                                    // Regular next connection
                                    if (step.TryGetProperty("next", out var next))
                                    {
                                        workflow.Edges.Add(new WorkflowEdge
                                        {
                                            Id = Guid.NewGuid().ToString(),
                                            SourceId = fromIdStr,
                                            TargetId = next.GetString()
                                        });
                                    }
                                    
                                    // Decision branches
                                    if (step.TryGetProperty("branches", out var branches))
                                    {
                                        if (branches.TryGetProperty("true", out var trueBranch))
                                        {
                                            foreach (var target in trueBranch.EnumerateArray())
                                            {
                                                workflow.Edges.Add(new WorkflowEdge
                                                {
                                                    Id = Guid.NewGuid().ToString(),
                                                    SourceId = fromIdStr,
                                                    TargetId = target.GetString()
                                                });
                                            }
                                        }
                                        if (branches.TryGetProperty("false", out var falseBranch))
                                        {
                                            foreach (var target in falseBranch.EnumerateArray())
                                            {
                                                workflow.Edges.Add(new WorkflowEdge
                                                {
                                                    Id = Guid.NewGuid().ToString(),
                                                    SourceId = fromIdStr,
                                                    TargetId = target.GetString()
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Store in memory for now (TODO: save to database)
                _workflows[request.ProjectId] = workflow;
                
                return Json(new { success = true, message = "Workflow uloženo" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        public class SaveWorkflowRequest
        {
            public Guid ProjectId { get; set; }
            public object WorkflowData { get; set; }
        }
        
        [HttpPost]
        public IActionResult AddNode(Guid projectId, [FromBody] WorkflowNode node)
        {
            if (!_workflows.ContainsKey(projectId))
            {
                return Json(new { success = false, message = "Workflow nenalezeno" });
            }
            
            var workflow = _workflows[projectId];
            
            // Nastav výchozí porty podle typu uzlu
            if (node.Type == NodeType.Task)
            {
                node.InputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "input", DataType = "object", IsRequired = true } 
                };
                node.OutputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "output", DataType = "object" },
                    new NodePort { Name = "error", DataType = "string" }
                };
            }
            else if (node.Type == NodeType.Condition)
            {
                node.InputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "input", DataType = "object", IsRequired = true } 
                };
                node.OutputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "true", DataType = "object" },
                    new NodePort { Name = "false", DataType = "object" }
                };
            }
            else if (node.Type == NodeType.Parallel)
            {
                node.InputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "input", DataType = "object", IsRequired = true } 
                };
                node.OutputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "branch1", DataType = "object" },
                    new NodePort { Name = "branch2", DataType = "object" },
                    new NodePort { Name = "branch3", DataType = "object" }
                };
            }
            else if (node.Type == NodeType.Join)
            {
                node.InputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "input1", DataType = "object" },
                    new NodePort { Name = "input2", DataType = "object" },
                    new NodePort { Name = "input3", DataType = "object" }
                };
                node.OutputPorts = new List<NodePort> 
                { 
                    new NodePort { Name = "output", DataType = "object" }
                };
            }
            
            workflow.Nodes.Add(node);
            workflow.LastModified = DateTime.Now;
            
            return Json(new { success = true, node = node });
        }
        
        [HttpPost]
        public IActionResult UpdateNode(Guid projectId, [FromBody] WorkflowNode node)
        {
            if (!_workflows.ContainsKey(projectId))
            {
                return Json(new { success = false, message = "Workflow nenalezeno" });
            }
            
            var workflow = _workflows[projectId];
            var existingNode = workflow.Nodes.FirstOrDefault(n => n.Id == node.Id);
            
            if (existingNode == null)
            {
                return Json(new { success = false, message = "Uzel nenalezen" });
            }
            
            // Aktualizuj vlastnosti
            existingNode.Name = node.Name;
            existingNode.Description = node.Description;
            existingNode.Position = node.Position;
            existingNode.Tools = node.Tools;
            existingNode.UseReAct = node.UseReAct;
            existingNode.Orchestrator = node.Orchestrator;
            existingNode.ConditionExpression = node.ConditionExpression;
            existingNode.LoopCondition = node.LoopCondition;
            existingNode.MaxIterations = node.MaxIterations;
            existingNode.Configuration = node.Configuration;
            
            workflow.LastModified = DateTime.Now;
            
            return Json(new { success = true });
        }
        
        [HttpPost]
        public IActionResult DeleteNode(Guid projectId, string nodeId)
        {
            if (!_workflows.ContainsKey(projectId))
            {
                return Json(new { success = false, message = "Workflow nenalezeno" });
            }
            
            var workflow = _workflows[projectId];
            
            // Smaž uzel
            workflow.Nodes.RemoveAll(n => n.Id == nodeId);
            
            // Smaž všechny hrany spojené s uzlem
            workflow.Edges.RemoveAll(e => e.SourceId == nodeId || e.TargetId == nodeId);
            
            workflow.LastModified = DateTime.Now;
            
            return Json(new { success = true });
        }
        
        [HttpPost]
        public IActionResult AddEdge(Guid projectId, [FromBody] WorkflowEdge edge)
        {
            if (!_workflows.ContainsKey(projectId))
            {
                return Json(new { success = false, message = "Workflow nenalezeno" });
            }
            
            var workflow = _workflows[projectId];
            
            // Ověř, že uzly existují
            var sourceNode = workflow.Nodes.FirstOrDefault(n => n.Id == edge.SourceId);
            var targetNode = workflow.Nodes.FirstOrDefault(n => n.Id == edge.TargetId);
            
            if (sourceNode == null || targetNode == null)
            {
                return Json(new { success = false, message = "Zdrojový nebo cílový uzel neexistuje" });
            }
            
            // Ověř, že není cyklus (jednoduchá kontrola)
            if (HasPath(workflow, edge.TargetId, edge.SourceId))
            {
                return Json(new { success = false, message = "Tato hrana by vytvořila cyklus" });
            }
            
            workflow.Edges.Add(edge);
            workflow.LastModified = DateTime.Now;
            
            return Json(new { success = true, edge = edge });
        }
        
        [HttpPost]
        public IActionResult DeleteEdge(Guid projectId, string edgeId)
        {
            if (!_workflows.ContainsKey(projectId))
            {
                return Json(new { success = false, message = "Workflow nenalezeno" });
            }
            
            var workflow = _workflows[projectId];
            workflow.Edges.RemoveAll(e => e.Id == edgeId);
            workflow.LastModified = DateTime.Now;
            
            return Json(new { success = true });
        }
        
        [HttpPost]
        public IActionResult ValidateWorkflow(Guid projectId)
        {
            if (!_workflows.ContainsKey(projectId))
            {
                return Json(new { success = false, message = "Workflow nenalezeno" });
            }
            
            var workflow = _workflows[projectId];
            var errors = new List<string>();
            
            // Kontrola: musí mít alespoň jeden uzel
            if (!workflow.Nodes.Any())
                errors.Add("Workflow musí obsahovat alespoň jeden uzel");
            
            // Kontrola: upozornění na nepropojené uzly (ale ne chyba)
            foreach (var node in workflow.Nodes)
            {
                bool hasInput = workflow.Edges.Any(e => e.TargetId == node.Id);
                bool hasOutput = workflow.Edges.Any(e => e.SourceId == node.Id);
                
                // Je OK mít uzly bez vstupu (vstupní body) nebo bez výstupu (koncové body)
                // Ale uzel úplně nepropojený může být problém
                if (!hasInput && !hasOutput && workflow.Nodes.Count > 1)
                {
                    errors.Add($"Uzel '{node.Name}' není propojený s ostatními uzly");
                }
            }
            
            // Kontrola: task uzly musí mít nástroje nebo orchestrátor
            foreach (var node in workflow.Nodes.Where(n => n.Type == NodeType.Task))
            {
                if (!node.Tools.Any() && string.IsNullOrEmpty(node.Orchestrator))
                {
                    errors.Add($"Úloha '{node.Name}' musí mít přiřazené nástroje nebo orchestrátor");
                }
            }
            
            // Kontrola: podmínkové uzly musí mít podmínku
            foreach (var node in workflow.Nodes.Where(n => n.Type == NodeType.Condition))
            {
                if (string.IsNullOrEmpty(node.ConditionExpression))
                {
                    errors.Add($"Podmínkový uzel '{node.Name}' musí mít definovanou podmínku");
                }
            }
            
            return Json(new 
            { 
                success = !errors.Any(), 
                errors = errors,
                message = errors.Any() ? "Workflow obsahuje chyby" : "Workflow je validní"
            });
        }
        
        [HttpPost]
        public IActionResult SaveDrawflowWorkflow([FromBody] dynamic workflowData)
        {
            try
            {
                var projectId = Guid.Parse(workflowData.ProjectId.ToString());
                
                // Uložit Drawflow data do našeho formátu
                // V reálné aplikaci byste parsovali DrawflowData a převedli na váš model
                _workflows[projectId] = new WorkflowGraphViewModel
                {
                    ProjectId = projectId,
                    ProjectName = workflowData.ProjectName.ToString(),
                    LastModified = DateTime.Now,
                    // Zde byste parsovali drawflowData.DrawflowData a převedli na Nodes/Edges
                };
                
                return Json(new { success = true, message = "Workflow uloženo" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        private NodeType MapStepTypeToNodeType(string stepType)
        {
            return stepType switch
            {
                "process" => NodeType.Task,
                "ai-tool" => NodeType.Task,
                "decision" => NodeType.Condition,
                "parallel-gateway" => NodeType.Parallel,
                _ => NodeType.Task
            };
        }
        
        // Helper metoda pro detekci cyklů
        private bool HasPath(WorkflowGraphViewModel workflow, string from, string to)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(from);
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == to) return true;
                
                if (visited.Contains(current)) continue;
                visited.Add(current);
                
                foreach (var edge in workflow.Edges.Where(e => e.SourceId == current))
                {
                    queue.Enqueue(edge.TargetId);
                }
            }
            
            return false;
        }
        
        [HttpGet]
        public IActionResult ExportWorkflow(Guid projectId)
        {
            if (!_workflows.ContainsKey(projectId))
            {
                return Json(new { success = false, message = "Workflow nenalezeno" });
            }
            
            var workflow = _workflows[projectId];
            
            // Export jako JSON pro import/export funkcionalitu
            return Json(new 
            { 
                success = true, 
                workflow = workflow,
                exportDate = DateTime.Now
            });
        }
        
        [HttpPost]
        public IActionResult ImportWorkflow(Guid projectId, [FromBody] WorkflowGraphViewModel workflow)
        {
            if (workflow == null)
            {
                return Json(new { success = false, message = "Neplatná data" });
            }
            
            workflow.ProjectId = projectId;
            workflow.LastModified = DateTime.Now;
            _workflows[projectId] = workflow;
            
            return Json(new { success = true, message = "Workflow importováno" });
        }
        
        [HttpPost]
        public IActionResult SaveOrchestratorSettings([FromBody] OrchestratorSettingsRequest request)
        {
            if (request == null || request.ProjectId == Guid.Empty)
            {
                return Json(new { success = false, message = "Neplatná data" });
            }
            
            try
            {
                // Get or create workflow
                if (!_workflows.ContainsKey(request.ProjectId))
                {
                    _workflows[request.ProjectId] = CreateDefaultWorkflow(request.ProjectId);
                }
                
                var workflow = _workflows[request.ProjectId];
                
                // Update orchestrator settings in metadata
                if (workflow.Metadata == null)
                {
                    workflow.Metadata = new WorkflowMetadata();
                }
                
                if (workflow.Metadata.Settings == null)
                {
                    workflow.Metadata.Settings = new WorkflowSettings();
                }
                
                // Apply settings
                workflow.Metadata.Settings.DefaultOrchestrator = request.Settings.DefaultOrchestrator;
                workflow.Metadata.Settings.DefaultModel = request.Settings.DefaultModel;
                workflow.Metadata.Settings.DefaultTemperature = request.Settings.DefaultTemperature;
                workflow.Metadata.Settings.DefaultSystemPrompt = request.Settings.DefaultSystemPrompt;
                workflow.Metadata.Settings.EnableReActByDefault = request.Settings.EnableReActByDefault;
                
                // Ensure we have orchestrator data structure
                if (workflow.Metadata.OrchestratorData == null)
                {
                    workflow.Metadata.OrchestratorData = new
                    {
                        metadata = new
                        {
                            settings = workflow.Metadata.Settings,
                            nodePositions = new Dictionary<string, object>()
                        }
                    };
                }
                
                workflow.LastModified = DateTime.Now;
                
                return Json(new { success = true, message = "Nastavení uloženo" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        public class OrchestratorSettingsRequest
        {
            public Guid ProjectId { get; set; }
            public OrchestratorSettings Settings { get; set; }
        }
        
        public class OrchestratorSettings
        {
            public string DefaultOrchestrator { get; set; }
            public string DefaultModel { get; set; }
            public double DefaultTemperature { get; set; }
            public string DefaultSystemPrompt { get; set; }
            public bool EnableReActByDefault { get; set; }
        }
    }
}