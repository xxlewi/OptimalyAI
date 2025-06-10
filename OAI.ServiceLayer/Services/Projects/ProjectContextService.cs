using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using System.Text;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IProjectContextService
    {
        Task<string> GetProjectContextAsync(Guid projectId);
        Task UpdateProjectContextAsync(Guid projectId, string context);
        Task<string> GenerateProjectContextAsync(Guid projectId);
        Task AppendToProjectContextAsync(Guid projectId, string section, string content);
    }

    public class ProjectContextService : IProjectContextService
    {
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectOrchestrator> _orchestratorRepository;
        private readonly IGuidRepository<ProjectTool> _toolRepository;
        private readonly IGuidRepository<ProjectWorkflow> _workflowRepository;
        private readonly IGuidRepository<ProjectHistory> _historyRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProjectContextService> _logger;

        public ProjectContextService(
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectOrchestrator> orchestratorRepository,
            IGuidRepository<ProjectTool> toolRepository,
            IGuidRepository<ProjectWorkflow> workflowRepository,
            IGuidRepository<ProjectHistory> historyRepository,
            IUnitOfWork unitOfWork,
            ILogger<ProjectContextService> logger)
        {
            _projectRepository = projectRepository;
            _orchestratorRepository = orchestratorRepository;
            _toolRepository = toolRepository;
            _workflowRepository = workflowRepository;
            _historyRepository = historyRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<string> GetProjectContextAsync(Guid projectId)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId)
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", projectId);

            if (string.IsNullOrEmpty(project.ProjectContext))
            {
                return await GenerateProjectContextAsync(projectId);
            }

            return project.ProjectContext;
        }

        public async Task UpdateProjectContextAsync(Guid projectId, string context)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId)
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", projectId);

            var oldContext = project.ProjectContext;
            project.ProjectContext = context;
            project.UpdatedAt = DateTime.UtcNow;
            project.Version++;

            await _projectRepository.UpdateAsync(project);

            // Historie změny
            await CreateHistoryAsync(projectId, "ContextUpdated", 
                "Aktualizován kontext projektu", oldContext, context);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Project context updated for project {ProjectId}", projectId);
        }

        public async Task<string> GenerateProjectContextAsync(Guid projectId)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId,
                include: q => q
                    .Include(p => p.ProjectOrchestrators)
                    .Include(p => p.ProjectTools)
                    .Include(p => p.Workflows))
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", projectId);

            var sb = new StringBuilder();
            
            // Hlavička podobná CLAUDE.md
            sb.AppendLine($"# {project.Name} - Project Context");
            sb.AppendLine();
            sb.AppendLine($"This file provides AI agents and orchestrators with context about the project '{project.Name}'.");
            sb.AppendLine();

            // Základní informace
            sb.AppendLine("## Project Overview");
            sb.AppendLine($"- **Customer**: {project.CustomerName}");
            sb.AppendLine($"- **Status**: {project.Status}");
            sb.AppendLine($"- **Priority**: {project.Priority}");
            sb.AppendLine($"- **Type**: {project.ProjectType ?? "General"}");
            sb.AppendLine($"- **Created**: {project.CreatedAt:yyyy-MM-dd}");
            if (project.StartDate.HasValue)
                sb.AppendLine($"- **Started**: {project.StartDate:yyyy-MM-dd}");
            if (project.DueDate.HasValue)
                sb.AppendLine($"- **Due Date**: {project.DueDate:yyyy-MM-dd}");
            sb.AppendLine();

            // Požadavek zákazníka
            sb.AppendLine("## Customer Requirement");
            sb.AppendLine(project.CustomerRequirement);
            sb.AppendLine();

            // Popis projektu
            if (!string.IsNullOrEmpty(project.Description))
            {
                sb.AppendLine("## Project Description");
                sb.AppendLine(project.Description);
                sb.AppendLine();
            }

            // Konfigurace
            if (!string.IsNullOrEmpty(project.Configuration))
            {
                sb.AppendLine("## Configuration");
                sb.AppendLine("```json");
                sb.AppendLine(project.Configuration);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // Orchestrátoři
            if (project.ProjectOrchestrators?.Any() == true)
            {
                sb.AppendLine("## Orchestrators");
                foreach (var orchestrator in project.ProjectOrchestrators.Where(o => o.IsActive).OrderBy(o => o.Order))
                {
                    sb.AppendLine($"- **{orchestrator.OrchestratorName}** ({orchestrator.OrchestratorType})");
                    if (!string.IsNullOrEmpty(orchestrator.Configuration))
                    {
                        sb.AppendLine($"  - Configuration: {orchestrator.Configuration}");
                    }
                    sb.AppendLine($"  - Usage Count: {orchestrator.UsageCount}");
                }
                sb.AppendLine();
            }

            // Nástroje
            if (project.ProjectTools?.Any() == true)
            {
                sb.AppendLine("## AI Tools");
                foreach (var tool in project.ProjectTools.Where(t => t.IsActive))
                {
                    sb.AppendLine($"- **{tool.ToolName}** (ID: {tool.ToolId})");
                    if (tool.MaxDailyUsage.HasValue)
                    {
                        sb.AppendLine($"  - Daily Limit: {tool.MaxDailyUsage}");
                    }
                    if (tool.SuccessRate.HasValue)
                    {
                        sb.AppendLine($"  - Success Rate: {tool.SuccessRate:F1}%");
                    }
                    if (!string.IsNullOrEmpty(tool.DefaultParameters))
                    {
                        sb.AppendLine($"  - Default Parameters: {tool.DefaultParameters}");
                    }
                }
                sb.AppendLine();
            }

            // Workflows
            if (project.Workflows?.Any() == true)
            {
                sb.AppendLine("## Workflows");
                foreach (var workflow in project.Workflows.Where(w => w.IsActive))
                {
                    sb.AppendLine($"- **{workflow.Name}** ({workflow.WorkflowType})");
                    if (!string.IsNullOrEmpty(workflow.Description))
                    {
                        sb.AppendLine($"  - {workflow.Description}");
                    }
                    sb.AppendLine($"  - Trigger: {workflow.TriggerType}");
                    if (!string.IsNullOrEmpty(workflow.CronExpression))
                    {
                        sb.AppendLine($"  - Schedule: {workflow.CronExpression}");
                    }
                    sb.AppendLine($"  - Executions: {workflow.ExecutionCount} (Success: {workflow.SuccessCount})");
                }
                sb.AppendLine();
            }

            // Poznámky
            if (!string.IsNullOrEmpty(project.Notes))
            {
                sb.AppendLine("## Notes");
                sb.AppendLine(project.Notes);
                sb.AppendLine();
            }

            // Důležité instrukce
            sb.AppendLine("## Important Instructions");
            sb.AppendLine("- Always respect the customer's original requirements");
            sb.AppendLine("- Use only the configured tools and orchestrators");
            sb.AppendLine("- Log all significant actions for billing and monitoring");
            sb.AppendLine("- Follow the defined workflows when available");
            sb.AppendLine();

            var context = sb.ToString();

            // Uložení vygenerovaného kontextu
            project.ProjectContext = context;
            project.UpdatedAt = DateTime.UtcNow;
            await _projectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            return context;
        }

        public async Task AppendToProjectContextAsync(Guid projectId, string section, string content)
        {
            var currentContext = await GetProjectContextAsync(projectId);
            
            var sb = new StringBuilder(currentContext);
            
            // Najít sekci nebo ji vytvořit
            var sectionHeader = $"## {section}";
            var sectionIndex = currentContext.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
            
            if (sectionIndex >= 0)
            {
                // Najít konec sekce (další ## nebo konec souboru)
                var nextSectionIndex = currentContext.IndexOf("\n##", sectionIndex + 1);
                if (nextSectionIndex < 0)
                    nextSectionIndex = currentContext.Length;

                // Vložit obsah před konec sekce
                sb.Insert(nextSectionIndex, $"\n{content}\n");
            }
            else
            {
                // Přidat novou sekci na konec
                sb.AppendLine();
                sb.AppendLine(sectionHeader);
                sb.AppendLine(content);
            }

            await UpdateProjectContextAsync(projectId, sb.ToString());
        }

        private async Task CreateHistoryAsync(Guid projectId, string changeType, string description, 
            string oldValue, string newValue)
        {
            var history = new ProjectHistory
            {
                ProjectId = projectId,
                ChangeType = changeType,
                Description = description,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedBy = "System",
                ChangedAt = DateTime.UtcNow
            };

            await _historyRepository.CreateAsync(history);
        }
    }
}