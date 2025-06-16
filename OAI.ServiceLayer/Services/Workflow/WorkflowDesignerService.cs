using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Workflow;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Workflow;

namespace OAI.ServiceLayer.Services.Workflow
{
    public class WorkflowDesignerService : IWorkflowDesignerService
    {
        private readonly IGuidRepository<ProjectWorkflow> _workflowRepository;
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WorkflowDesignerService> _logger;

        public WorkflowDesignerService(
            IGuidRepository<ProjectWorkflow> workflowRepository,
            IGuidRepository<Project> projectRepository,
            IUnitOfWork unitOfWork,
            ILogger<WorkflowDesignerService> logger)
        {
            _workflowRepository = workflowRepository;
            _projectRepository = projectRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<WorkflowDesignerDto> GetWorkflowAsync(Guid projectId)
        {
            try
            {
                var workflows = await _workflowRepository.GetAsync(
                    filter: w => w.ProjectId == projectId && w.IsActive,
                    orderBy: q => q.OrderByDescending(w => w.CreatedAt),
                    take: 1);
                
                var workflow = workflows.FirstOrDefault();

                if (workflow == null)
                {
                    _logger.LogInformation($"No workflow found for project {projectId}, creating new");
                    return CreateNewWorkflow(projectId);
                }

                // Deserialize orchestrator data from StepsDefinition
                if (!string.IsNullOrEmpty(workflow.StepsDefinition))
                {
                    try
                    {
                        _logger.LogInformation($"Attempting to deserialize StepsDefinition: {workflow.StepsDefinition.Substring(0, Math.Min(workflow.StepsDefinition.Length, 200))}...");
                        
                        var deserializerOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var dto = JsonSerializer.Deserialize<WorkflowDesignerDto>(workflow.StepsDefinition, deserializerOptions);
                        if (dto != null)
                        {
                            dto.Id = workflow.Id;
                            dto.ProjectId = workflow.ProjectId;
                            _logger.LogInformation($"Successfully loaded workflow with {dto.Steps?.Count ?? 0} steps");
                            return dto;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize workflow data");
                        // If deserialization fails, return basic workflow
                    }
                }
                else
                {
                    _logger.LogWarning($"Workflow {workflow.Id} has empty StepsDefinition");
                }

                // Return basic workflow if no orchestrator data
                return new WorkflowDesignerDto
                {
                    Id = workflow.Id,
                    ProjectId = workflow.ProjectId,
                    Name = workflow.Name,
                    Description = workflow.Description ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting workflow for project {projectId}");
                throw;
            }
        }

        public async Task<WorkflowDesignerDto> SaveWorkflowAsync(Guid projectId, SaveWorkflowDto dto)
        {
            try
            {
                // Parse the workflow data
                _logger.LogInformation($"Received workflow data: {dto.WorkflowData.Substring(0, Math.Min(dto.WorkflowData.Length, 500))}...");
                
                var deserializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var workflowData = JsonSerializer.Deserialize<WorkflowDesignerDto>(dto.WorkflowData, deserializerOptions);
                if (workflowData == null)
                {
                    throw new ArgumentException("Invalid workflow data");
                }
                _logger.LogInformation($"Parsed workflow with {workflowData.Steps?.Count ?? 0} steps, FirstStepId: {workflowData.FirstStepId}");

                // Find existing workflow or create new
                var workflows = await _workflowRepository.GetAsync(
                    filter: w => w.ProjectId == projectId && w.IsActive,
                    orderBy: q => q.OrderByDescending(w => w.CreatedAt),
                    take: 1);
                
                var workflow = workflows.FirstOrDefault();

                if (workflow == null)
                {
                    workflow = new ProjectWorkflow
                    {
                        ProjectId = projectId,
                        Name = workflowData.Name ?? $"Workflow {DateTime.Now:yyyy-MM-dd HH:mm}",
                        Description = workflowData.Description,
                        WorkflowType = "visual", // Visual workflow designer
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _workflowRepository.AddAsync(workflow);
                }
                else
                {
                    workflow.Name = workflowData.Name ?? workflow.Name;
                    workflow.Description = workflowData.Description ?? workflow.Description;
                    workflow.UpdatedAt = DateTime.UtcNow;
                    _workflowRepository.Update(workflow);
                }

                // Store the entire workflow data in StepsDefinition
                var serializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                workflow.StepsDefinition = JsonSerializer.Serialize(workflowData, serializerOptions);
                
                _logger.LogInformation($"Saving workflow {workflow.Id} with StepsDefinition length: {workflow.StepsDefinition?.Length ?? 0}");
                _logger.LogInformation($"StepsDefinition preview: {workflow.StepsDefinition?.Substring(0, Math.Min(workflow.StepsDefinition?.Length ?? 0, 200))}...");
                _logger.LogInformation($"Number of steps in workflowData before save: {workflowData.Steps?.Count ?? 0}");

                await _unitOfWork.SaveChangesAsync();

                workflowData.Id = workflow.Id;
                workflowData.ProjectId = workflow.ProjectId;

                _logger.LogInformation($"Workflow {workflow.Id} saved for project {projectId}");
                return workflowData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving workflow for project {projectId}");
                throw;
            }
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowAsync(WorkflowDesignerDto workflow)
        {
            var result = new WorkflowValidationResult { IsValid = true };

            try
            {
                // Check if workflow has steps
                if (workflow.Steps == null || !workflow.Steps.Any())
                {
                    result.IsValid = false;
                    result.Errors.Add("Workflow musí obsahovat alespoň jeden krok");
                    return result;
                }

                // Check if first step exists
                if (!string.IsNullOrEmpty(workflow.FirstStepId))
                {
                    if (!workflow.Steps.Any(s => s.Id == workflow.FirstStepId))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"První krok '{workflow.FirstStepId}' neexistuje");
                    }
                }
                else
                {
                    result.IsValid = false;
                    result.Errors.Add("Workflow musí mít definovaný první krok");
                }

                // Validate each step
                foreach (var step in workflow.Steps)
                {
                    // Check tool steps have tools assigned
                    if (step.Type == "tool" && string.IsNullOrEmpty(step.Tool))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Krok '{step.Name}' typu 'tool' musí mít přiřazený nástroj");
                    }

                    // Check decision steps have condition
                    if (step.Type == "decision" && string.IsNullOrEmpty(step.Condition))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Rozhodovací krok '{step.Name}' musí mít definovanou podmínku");
                    }

                    // Check adapter steps have adapter configured
                    if ((step.Type == "input-adapter" || step.Type == "output-adapter") && string.IsNullOrEmpty(step.AdapterId))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Adaptér '{step.Name}' musí mít vybraný typ adaptéru");
                    }

                    // Validate connections
                    if (!string.IsNullOrEmpty(step.Next))
                    {
                        if (!workflow.Steps.Any(s => s.Id == step.Next))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Krok '{step.Name}' odkazuje na neexistující následující krok '{step.Next}'");
                        }
                    }

                    // Validate branches
                    if (step.Branches != null)
                    {
                        foreach (var branch in step.Branches)
                        {
                            foreach (var targetId in branch.Value)
                            {
                                if (!workflow.Steps.Any(s => s.Id == targetId))
                                {
                                    result.IsValid = false;
                                    result.Errors.Add($"Větev '{branch.Key}' kroku '{step.Name}' odkazuje na neexistující krok '{targetId}'");
                                }
                            }
                        }
                    }
                }

                // Check for disconnected components (optional - warning only)
                var reachableSteps = GetReachableSteps(workflow);
                var unreachableSteps = workflow.Steps.Where(s => !reachableSteps.Contains(s.Id)).ToList();
                if (unreachableSteps.Any())
                {
                    foreach (var step in unreachableSteps)
                    {
                        result.Errors.Add($"Varování: Krok '{step.Name}' není dostupný z počátečního kroku");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating workflow");
                result.IsValid = false;
                result.Errors.Add("Chyba při validaci workflow");
                return result;
            }
        }

        public async Task<WorkflowExportDto> ExportWorkflowAsync(Guid projectId, string format = "json")
        {
            try
            {
                var workflow = await GetWorkflowAsync(projectId);
                
                string data;
                string contentType;
                string extension;

                switch (format.ToLower())
                {
                    case "json":
                        data = JsonSerializer.Serialize(workflow, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });
                        contentType = "application/json";
                        extension = "json";
                        break;
                    
                    default:
                        throw new ArgumentException($"Unsupported export format: {format}");
                }

                return new WorkflowExportDto
                {
                    Format = format,
                    Data = data,
                    FileName = $"workflow-{projectId}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting workflow for project {projectId}");
                throw;
            }
        }

        public async Task<WorkflowDesignerDto> ImportWorkflowAsync(Guid projectId, string data, string format = "json")
        {
            try
            {
                WorkflowDesignerDto? workflowData;

                switch (format.ToLower())
                {
                    case "json":
                        workflowData = JsonSerializer.Deserialize<WorkflowDesignerDto>(data);
                        break;
                    
                    default:
                        throw new ArgumentException($"Unsupported import format: {format}");
                }

                if (workflowData == null)
                {
                    throw new ArgumentException("Invalid workflow data");
                }

                // Update project ID
                workflowData.ProjectId = projectId;

                // Validate the imported workflow
                var validationResult = await ValidateWorkflowAsync(workflowData);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Imported workflow is not valid: {string.Join(", ", validationResult.Errors)}");
                }

                // Save the workflow
                var saveDto = new SaveWorkflowDto
                {
                    WorkflowData = JsonSerializer.Serialize(workflowData)
                };

                return await SaveWorkflowAsync(projectId, saveDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error importing workflow for project {projectId}");
                throw;
            }
        }

        private WorkflowDesignerDto CreateNewWorkflow(Guid projectId)
        {
            return new WorkflowDesignerDto
            {
                Id = Guid.Empty,
                ProjectId = projectId,
                Name = $"Workflow {DateTime.Now:yyyy-MM-dd HH:mm}",
                Description = "Nové workflow vytvořené ve vizuálním designeru",
                Steps = new List<WorkflowStepDto>(),
                Metadata = new WorkflowMetadataDto
                {
                    CreatedWith = "SimpleWorkflowDesigner",
                    CreatedAt = DateTime.UtcNow
                }
            };
        }

        private HashSet<string> GetReachableSteps(WorkflowDesignerDto workflow)
        {
            var reachable = new HashSet<string>();
            var toVisit = new Queue<string>();

            if (!string.IsNullOrEmpty(workflow.FirstStepId))
            {
                toVisit.Enqueue(workflow.FirstStepId);
            }

            while (toVisit.Count > 0)
            {
                var stepId = toVisit.Dequeue();
                if (reachable.Contains(stepId)) continue;

                reachable.Add(stepId);
                var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
                
                if (step != null)
                {
                    // Add next step
                    if (!string.IsNullOrEmpty(step.Next))
                    {
                        toVisit.Enqueue(step.Next);
                    }

                    // Add branch targets
                    if (step.Branches != null)
                    {
                        foreach (var branch in step.Branches.Values)
                        {
                            foreach (var targetId in branch)
                            {
                                toVisit.Enqueue(targetId);
                            }
                        }
                    }
                }
            }

            return reachable;
        }
    }
}