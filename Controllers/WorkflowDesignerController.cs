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
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                
                // If OrchestratorData exists, ensure it has the current settings
                if (workflow.Metadata?.OrchestratorData != null)
                {
                    // Return the orchestrator data as-is with settings
                    return Json(new { 
                        success = true, 
                        orchestratorData = workflow.Metadata.OrchestratorData 
                    });
                }
                
                // Return empty workflow structure with settings
                return Json(new { 
                    success = true, 
                    orchestratorData = new
                    {
                        steps = new List<object>(),
                        metadata = new
                        {
                            settings = workflow.Metadata?.Settings,
                            nodePositions = new Dictionary<string, object>()
                        }
                    }
                });
            }
            
            // No workflow exists at all
            return Json(new { 
                success = true,
                orchestratorData = (object)null
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
            
            // Vytvoř základní start a end uzly
            var startNode = new WorkflowNode
            {
                Id = "start_node",
                Name = "Začátek",
                Type = NodeType.Start,
                Position = new NodePosition { X = 100, Y = 200 },
                OutputPorts = new List<NodePort>
                {
                    new NodePort { Name = "output", DataType = "object" }
                }
            };
            
            var endNode = new WorkflowNode
            {
                Id = "end_node",
                Name = "Konec",
                Type = NodeType.End,
                Position = new NodePosition { X = 800, Y = 200 },
                InputPorts = new List<NodePort>
                {
                    new NodePort { Name = "input", DataType = "object" }
                }
            };
            
            workflow.Nodes.Add(startNode);
            workflow.Nodes.Add(endNode);
            
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
            
            // Nemůžeme smazat start nebo end uzel
            if (nodeId == "start_node" || nodeId == "end_node")
            {
                return Json(new { success = false, message = "Nelze smazat počáteční nebo koncový uzel" });
            }
            
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
            
            // Kontrola: musí mít start a end
            if (!workflow.Nodes.Any(n => n.Type == NodeType.Start))
                errors.Add("Workflow musí mít počáteční uzel");
                
            if (!workflow.Nodes.Any(n => n.Type == NodeType.End))
                errors.Add("Workflow musí mít koncový uzel");
            
            // Kontrola: všechny uzly kromě start musí mít vstup
            foreach (var node in workflow.Nodes.Where(n => n.Type != NodeType.Start))
            {
                if (!workflow.Edges.Any(e => e.TargetId == node.Id))
                {
                    errors.Add($"Uzel '{node.Name}' nemá žádný vstup");
                }
            }
            
            // Kontrola: všechny uzly kromě end musí mít výstup
            foreach (var node in workflow.Nodes.Where(n => n.Type != NodeType.End))
            {
                if (!workflow.Edges.Any(e => e.SourceId == node.Id))
                {
                    errors.Add($"Uzel '{node.Name}' nemá žádný výstup");
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