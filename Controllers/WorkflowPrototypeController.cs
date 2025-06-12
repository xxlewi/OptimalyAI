using Microsoft.AspNetCore.Mvc;
using OptimalyAI.ViewModels;
using OAI.Core.Interfaces.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Prototype controller pro testování nového workflow UI
    /// Používá pouze ViewModely - žádná persistence do DB
    /// </summary>
    public class WorkflowPrototypeController : Controller
    {
        private readonly IToolRegistry _toolRegistry;
        
        // Simulace in-memory storage pro demo účely
        private static Dictionary<Guid, WorkflowPrototypeViewModel> _workflows = new();
        private static Dictionary<Guid, List<WorkflowStagePrototype>> _projectStages = new();
        private static List<ProjectListItemViewModel> _projects = InitializeDemoProjects();
        
        public WorkflowPrototypeController(IToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;
        }

        private static List<ProjectListItemViewModel> InitializeDemoProjects()
        {
            return new List<ProjectListItemViewModel>
            {
                new ProjectListItemViewModel
                { 
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), 
                    Name = "E-commerce vyhledávač produktů",
                    Description = "Automatické vyhledávání podobných produktů podle fotek",
                    Status = "Active",
                    CustomerName = "Fashion Store CZ",
                    CustomerEmail = "info@fashionstore.cz",
                    StageCount = 5,
                    TriggerType = "Event",
                    NextRun = null,
                    LastRun = DateTime.Now.AddHours(-2),
                    LastRunSuccess = true,
                    SuccessRate = 92,
                    TotalRuns = 156,
                    WorkflowType = "ecommerce_search"
                },
                new ProjectListItemViewModel
                { 
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), 
                    Name = "Generátor produktových popisů",
                    Description = "AI generování popisů z fotek produktů",
                    Status = "Draft",
                    CustomerName = "TechGadgets s.r.o.",
                    CustomerEmail = "admin@techgadgets.cz",
                    StageCount = 3,
                    TriggerType = "Manual",
                    NextRun = null,
                    LastRun = null,
                    LastRunSuccess = false,
                    SuccessRate = 0,
                    TotalRuns = 0,
                    WorkflowType = "content_generation"
                },
                new ProjectListItemViewModel
                { 
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), 
                    Name = "Analýza konkurence",
                    Description = "Pravidelný monitoring cen konkurence",
                    Status = "Active",
                    CustomerName = "Market Leaders",
                    CustomerEmail = "analytics@marketleaders.com",
                    StageCount = 7,
                    TriggerType = "Schedule",
                    NextRun = DateTime.Now.AddHours(4),
                    LastRun = DateTime.Now.AddDays(-1),
                    LastRunSuccess = true,
                    SuccessRate = 78,
                    TotalRuns = 89,
                    WorkflowType = "data_analysis"
                },
                new ProjectListItemViewModel
                { 
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), 
                    Name = "Customer Support Bot",
                    Description = "Automatický chatbot pro zákaznickou podporu",
                    Status = "Active",
                    CustomerName = "Support Plus",
                    CustomerEmail = "bot@supportplus.cz",
                    StageCount = 4,
                    TriggerType = "Event",
                    NextRun = null,
                    LastRun = DateTime.Now.AddMinutes(-30),
                    LastRunSuccess = false,
                    SuccessRate = 65,
                    TotalRuns = 1247,
                    WorkflowType = "chatbot"
                }
            };
        }
        
        public IActionResult Test()
        {
            return Content("WorkflowPrototype controller works!");
        }
        
        public IActionResult Index()
        {
            // Statistiky pro dashboard
            ViewBag.DemoProjects = _projects;
            ViewBag.TotalProjects = _projects.Count;
            ViewBag.ActiveProjects = _projects.Count(p => p.Status == "Active");
            ViewBag.DraftProjects = _projects.Count(p => p.Status == "Draft");
            ViewBag.FailedProjects = _projects.Count(p => !p.LastRunSuccess && p.TotalRuns > 0);
            
            return View();
        }
        
        [HttpGet("Index/{projectId}")]
        public async Task<IActionResult> IndexWithProject(Guid projectId)
        {
            // Přesměruj na Drawflow designer
            return await DrawflowDesigner(projectId);
        }
        
        [HttpGet]
        public async Task<IActionResult> DrawflowDesigner(Guid projectId)
        {
            // Get project info
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }
            
            // Create workflow model for Drawflow
            var workflow = new DrawflowWorkflow
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                DrawflowData = "{\"drawflow\":{\"Home\":{\"data\":{}}}}",
                LastModified = DateTime.Now
            };
            
            // Get available tools
            var tools = await _toolRegistry.GetAllToolsAsync();
            ViewBag.AvailableTools = tools.Select(t => new { 
                Id = t.Id, 
                Name = t.Name, 
                Category = t.Category,
                Description = t.Description 
            }).ToList();
            
            // Get orchestrators
            ViewBag.Orchestrators = new List<string> 
            { 
                "ConversationOrchestrator", 
                "ToolChainOrchestrator", 
                "ReActOrchestrator",
                "ParallelOrchestrator" 
            };
            
            return View("DrawflowDesigner", workflow);
        }

        [HttpGet]
        public IActionResult Details(Guid projectId)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }

            // Získej workflow data pokud existují
            var workflow = _workflows.ContainsKey(projectId) ? _workflows[projectId] : null;
            
            ViewBag.Project = project;
            ViewBag.Workflow = workflow;
            ViewBag.HasWorkflow = workflow != null && workflow.Stages.Any();
            
            // Simulace historie běhů
            ViewBag.ExecutionHistory = GenerateExecutionHistory(projectId);
            
            return View();
        }
        
        private List<dynamic> GenerateExecutionHistory(Guid projectId)
        {
            var history = new List<dynamic>();
            var random = new Random();
            
            for (int i = 0; i < 10; i++)
            {
                history.Add(new
                {
                    Id = Guid.NewGuid(),
                    StartedAt = DateTime.Now.AddDays(-i).AddHours(random.Next(-12, 12)),
                    CompletedAt = DateTime.Now.AddDays(-i).AddHours(random.Next(-11, 13)),
                    Status = random.Next(0, 10) > 2 ? "Success" : "Failed",
                    ItemsProcessed = random.Next(50, 500),
                    Duration = TimeSpan.FromMinutes(random.Next(5, 120)),
                    InitiatedBy = i % 3 == 0 ? "Plánované spuštění" : "Manuální spuštění",
                    ErrorMessage = random.Next(0, 10) > 2 ? null : "Chyba při zpracování některých položek"
                });
            }
            
            return history.OrderByDescending(h => h.StartedAt).ToList();
        }

        [HttpGet]
        public IActionResult Designer(Guid? projectId = null, string? name = null)
        {
            // Přesměruj na nový robustní designer
            return RedirectToAction("Index", "WorkflowDesigner", new { projectId = projectId ?? Guid.Parse("11111111-1111-1111-1111-111111111111") });
        }

        [HttpPost]
        public IActionResult ApplyWorkflowType(Guid projectId, string workflowType)
        {
            var workflowTypes = WorkflowPrototypeData.GetWorkflowTypes();
            var selectedType = workflowTypes.FirstOrDefault(wt => wt.Value == workflowType);

            if (selectedType != null && _workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                workflow.WorkflowType = workflowType;
                workflow.Stages = selectedType.DefaultStages.Select((stage, index) => 
                {
                    stage.Order = index + 1;
                    return stage;
                }).ToList();
                workflow.LastModified = DateTime.Now;
            }

            return RedirectToAction("Designer", new { projectId });
        }

        [HttpGet]
        public IActionResult CreateStage(Guid projectId)
        {
            var model = new CreateStagePrototypeViewModel
            {
                ProjectId = projectId
            };

            ViewBag.StageTypes = WorkflowPrototypeData.GetStageTypes();
            ViewBag.ExecutionStrategies = WorkflowPrototypeData.GetExecutionStrategies();
            ViewBag.AvailableTools = WorkflowPrototypeData.GetAvailableTools();
            ViewBag.ToolsByCategory = WorkflowPrototypeData.GetToolsByCategory();

            return PartialView("_CreateStage", model);
        }

        [HttpPost]
        public IActionResult CreateStage(CreateStagePrototypeViewModel model)
        {
            if (ModelState.IsValid && _workflows.ContainsKey(model.ProjectId))
            {
                var workflow = _workflows[model.ProjectId];
                
                var newStage = new WorkflowStagePrototype
                {
                    Id = Guid.NewGuid(),
                    Name = model.Name,
                    Description = model.Description,
                    Type = model.Type,
                    ExecutionStrategy = model.ExecutionStrategy,
                    Tools = model.SelectedTools ?? new List<string>(),
                    UseReAct = model.UseReAct,
                    Order = workflow.Stages.Count + 1,
                    Status = "Draft",
                    Configuration = model.AdvancedConfig ?? new Dictionary<string, object>
                    {
                        ["timeout"] = 300,
                        ["retryCount"] = 3,
                        ["priority"] = "Normal",
                        ["enableLogging"] = true
                    }
                };

                workflow.Stages.Add(newStage);
                workflow.LastModified = DateTime.Now;

                return Json(new { success = true, message = "Stage byla úspěšně vytvořena" });
            }

            return Json(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
        }

        [HttpGet]
        public IActionResult EditStage(Guid stageId, Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                var stage = workflow.Stages.FirstOrDefault(s => s.Id == stageId);
                
                if (stage != null)
                {
                    var model = new CreateStagePrototypeViewModel
                    {
                        Name = stage.Name,
                        Description = stage.Description,
                        Type = stage.Type,
                        ExecutionStrategy = stage.ExecutionStrategy,
                        SelectedTools = stage.Tools,
                        UseReAct = stage.UseReAct,
                        ProjectId = projectId,
                        AdvancedConfig = stage.Configuration
                    };

                    ViewBag.StageTypes = WorkflowPrototypeData.GetStageTypes();
                    ViewBag.ExecutionStrategies = WorkflowPrototypeData.GetExecutionStrategies();
                    ViewBag.AvailableTools = WorkflowPrototypeData.GetAvailableTools();
                    ViewBag.ToolsByCategory = WorkflowPrototypeData.GetToolsByCategory();
                    ViewBag.StageId = stageId;

                    return PartialView("_EditStage", model);
                }
            }

            return NotFound();
        }

        [HttpPost]
        public IActionResult UpdateStage(Guid stageId, CreateStagePrototypeViewModel model)
        {
            if (ModelState.IsValid && _workflows.ContainsKey(model.ProjectId))
            {
                var workflow = _workflows[model.ProjectId];
                var stage = workflow.Stages.FirstOrDefault(s => s.Id == stageId);
                
                if (stage != null)
                {
                    stage.Name = model.Name;
                    stage.Description = model.Description;
                    stage.Type = model.Type;
                    stage.ExecutionStrategy = model.ExecutionStrategy;
                    stage.Tools = model.SelectedTools ?? new List<string>();
                    stage.UseReAct = model.UseReAct;
                    stage.Configuration = model.AdvancedConfig ?? new Dictionary<string, object>();

                    workflow.LastModified = DateTime.Now;

                    return Json(new { success = true, message = "Stage byla úspěšně aktualizována" });
                }
            }

            return Json(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
        }

        [HttpPost]
        public IActionResult DeleteStage(Guid stageId, Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                var stage = workflow.Stages.FirstOrDefault(s => s.Id == stageId);
                
                if (stage != null)
                {
                    workflow.Stages.Remove(stage);
                    
                    // Reorder remaining stages
                    for (int i = 0; i < workflow.Stages.Count; i++)
                    {
                        workflow.Stages[i].Order = i + 1;
                    }
                    
                    workflow.LastModified = DateTime.Now;

                    return Json(new { success = true, message = "Stage byla smazána" });
                }
            }

            return Json(new { success = false, message = "Stage nebyla nalezena" });
        }

        [HttpPost]
        public IActionResult ReorderStages(Guid projectId, List<Guid> stageIds)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                
                for (int i = 0; i < stageIds.Count; i++)
                {
                    var stage = workflow.Stages.FirstOrDefault(s => s.Id == stageIds[i]);
                    if (stage != null)
                    {
                        stage.Order = i + 1;
                    }
                }
                
                workflow.Stages = workflow.Stages.OrderBy(s => s.Order).ToList();
                workflow.LastModified = DateTime.Now;

                return Json(new { success = true, message = "Stages byly přeuspořádány" });
            }

            return Json(new { success = false, message = "Projekt nebyl nalezen" });
        }

        [HttpPost]
        public IActionResult DuplicateStage(Guid stageId, Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                var originalStage = workflow.Stages.FirstOrDefault(s => s.Id == stageId);
                
                if (originalStage != null)
                {
                    var duplicatedStage = new WorkflowStagePrototype
                    {
                        Id = Guid.NewGuid(),
                        Name = originalStage.Name + " (kopie)",
                        Description = originalStage.Description,
                        Type = originalStage.Type,
                        ExecutionStrategy = originalStage.ExecutionStrategy,
                        Tools = new List<string>(originalStage.Tools),
                        UseReAct = originalStage.UseReAct,
                        Order = workflow.Stages.Count + 1,
                        Status = "Draft",
                        Configuration = new Dictionary<string, object>(originalStage.Configuration)
                    };

                    workflow.Stages.Add(duplicatedStage);
                    workflow.LastModified = DateTime.Now;

                    return Json(new { success = true, message = "Stage byla duplikována" });
                }
            }

            return Json(new { success = false, message = "Stage nebyla nalezena" });
        }

        [HttpGet]
        public IActionResult TestWorkflow(Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                ViewBag.Workflow = workflow;
                return PartialView("_TestWorkflow");
            }

            return NotFound();
        }

        [HttpPost]
        public IActionResult SimulateWorkflowExecution(Guid projectId, Dictionary<string, object> parameters)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                
                // Simulace spuštění workflow
                var result = new
                {
                    executionId = Guid.NewGuid(),
                    status = "Running",
                    startedAt = DateTime.Now,
                    stages = workflow.Stages.Select(s => new
                    {
                        stageId = s.Id,
                        name = s.Name,
                        status = "Pending",
                        estimatedDuration = TimeSpan.FromSeconds(30 + (s.Tools.Count * 15))
                    })
                };

                return Json(new { success = true, result });
            }

            return Json(new { success = false, message = "Workflow nebyl nalezen" });
        }

        [HttpGet]
        public IActionResult Clear(Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                _workflows.Remove(projectId);
            }
            
            return RedirectToAction("Designer", new { projectId });
        }

        [HttpGet]
        public IActionResult SaveTemplate(Guid projectId, string templateName)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                
                // Simulace uložení jako template
                var templateId = Guid.NewGuid();
                
                return Json(new { 
                    success = true, 
                    message = $"Workflow byl uložen jako šablona '{templateName}'",
                    templateId 
                });
            }

            return Json(new { success = false, message = "Workflow nebyl nalezen" });
        }
        
        [HttpPost]
        public IActionResult CreateProject(string name, string customerId, string description, string workflowType, string priority, string launchType)
        {
            var newProject = new ProjectListItemViewModel
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                Status = "Draft",
                CustomerName = customerId switch
                {
                    "" => "Bez zákazníka",
                    null => "Bez zákazníka",
                    "1" => "Fashion Store CZ",
                    "2" => "TechGadgets s.r.o.",
                    "3" => "Market Leaders",
                    _ => "Nový zákazník"
                },
                CustomerEmail = customerId switch
                {
                    "" => "",
                    null => "",
                    "1" => "info@fashionstore.cz",
                    "2" => "admin@techgadgets.cz",
                    "3" => "analytics@marketleaders.com",
                    _ => "new@customer.cz"
                },
                StageCount = 0,
                TriggerType = "Manual",
                NextRun = null,
                LastRun = null,
                LastRunSuccess = false,
                SuccessRate = 0,
                TotalRuns = 0,
                WorkflowType = workflowType ?? "custom"
            };
            
            _projects.Add(newProject);
            
            // Initialize workflow for the project
            _workflows[newProject.Id] = new WorkflowPrototypeViewModel
            {
                ProjectId = newProject.Id,
                ProjectName = name,
                WorkflowType = workflowType ?? ""
            };
            
            return Json(new { 
                success = true, 
                projectId = newProject.Id,
                message = "Projekt byl úspěšně vytvořen"
            });
        }
        
        // Drawflow specific methods
        [HttpPost]
        public IActionResult SaveWorkflow([FromBody] DrawflowWorkflow workflow)
        {
            if (workflow == null)
            {
                return Json(new { success = false, message = "Invalid workflow data" });
            }
            
            // Store workflow data in memory
            if (!_workflows.ContainsKey(workflow.ProjectId))
            {
                _workflows[workflow.ProjectId] = new WorkflowPrototypeViewModel
                {
                    ProjectId = workflow.ProjectId,
                    ProjectName = workflow.ProjectName
                };
            }
            
            // Store Drawflow data
            _workflows[workflow.ProjectId].DrawflowData = workflow.DrawflowData;
            _workflows[workflow.ProjectId].LastModified = DateTime.Now;
            
            return Json(new { success = true, message = "Workflow byl úspěšně uložen" });
        }
        
        [HttpPost]
        public IActionResult ValidateWorkflow(Guid projectId)
        {
            var errors = new List<string>();
            
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                if (string.IsNullOrEmpty(workflow.DrawflowData))
                {
                    errors.Add("Workflow je prázdný");
                }
                else
                {
                    try
                    {
                        var data = System.Text.Json.JsonDocument.Parse(workflow.DrawflowData);
                        var drawflow = data.RootElement.GetProperty("drawflow");
                        var home = drawflow.GetProperty("Home");
                        var nodes = home.GetProperty("data");
                        
                        var nodeCount = 0;
                        var hasStart = false;
                        var hasEnd = false;
                        
                        foreach (var node in nodes.EnumerateObject())
                        {
                            nodeCount++;
                            var nodeData = node.Value.GetProperty("data");
                            var nodeType = nodeData.GetProperty("type").GetString();
                            
                            if (nodeType == "start") hasStart = true;
                            if (nodeType == "end") hasEnd = true;
                        }
                        
                        if (nodeCount == 0) errors.Add("Workflow neobsahuje žádné uzly");
                        if (!hasStart) errors.Add("Workflow musí mít počáteční uzel");
                        if (!hasEnd) errors.Add("Workflow musí mít koncový uzel");
                    }
                    catch
                    {
                        errors.Add("Neplatná struktura workflow");
                    }
                }
            }
            else
            {
                errors.Add("Workflow nebyl nalezen");
            }
            
            if (errors.Any())
            {
                return Json(new { success = false, errors });
            }
            
            return Json(new { success = true, message = "Workflow je validní" });
        }
        
        [HttpGet]
        public IActionResult ExportWorkflow(Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                var workflow = _workflows[projectId];
                var json = workflow.DrawflowData ?? "{}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                return File(bytes, "application/json", $"workflow_{projectId}.json");
            }
            
            return NotFound();
        }
        
        [HttpPost]
        public async Task<IActionResult> ImportWorkflow(Guid projectId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Soubor nebyl nahrán" });
            }
            
            try
            {
                using var stream = new System.IO.MemoryStream();
                await file.CopyToAsync(stream);
                var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                
                // Validate JSON
                var doc = System.Text.Json.JsonDocument.Parse(json);
                
                // Store workflow
                if (!_workflows.ContainsKey(projectId))
                {
                    _workflows[projectId] = new WorkflowPrototypeViewModel
                    {
                        ProjectId = projectId,
                        ProjectName = "Imported Workflow"
                    };
                }
                
                _workflows[projectId].DrawflowData = json;
                _workflows[projectId].LastModified = DateTime.Now;
                
                return Json(new { success = true, message = "Workflow byl úspěšně importován" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Chyba při importu: {ex.Message}" });
            }
        }
        
        [HttpPost]
        public IActionResult ClearWorkflow(Guid projectId)
        {
            if (_workflows.ContainsKey(projectId))
            {
                _workflows[projectId].DrawflowData = "{\"drawflow\":{\"Home\":{\"data\":{}}}}";
                _workflows[projectId].LastModified = DateTime.Now;
                return Json(new { success = true, message = "Workflow byl vymazán" });
            }
            
            return Json(new { success = false, message = "Workflow nebyl nalezen" });
        }
    }
    
    /// <summary>
    /// Drawflow workflow model
    /// </summary>
    public class DrawflowWorkflow
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string DrawflowData { get; set; } = ""; // JSON string from Drawflow
        public DateTime LastModified { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}