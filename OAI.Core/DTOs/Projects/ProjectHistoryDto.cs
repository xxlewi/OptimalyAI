using System;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro historii změn projektu
    /// </summary>
    public class ProjectHistoryDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string ChangeType { get; set; }
        public string Description { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public int ProjectVersion { get; set; }
        public string Notes { get; set; }

        // Pro zobrazení v UI
        public string ChangeTypeFormatted => FormatChangeType(ChangeType);
        public string ChangeIcon => GetChangeIcon(ChangeType);
        public string ChangeColor => GetChangeColor(ChangeType);

        private string FormatChangeType(string type)
        {
            return type switch
            {
                "StatusChange" => "Změna stavu",
                "ConfigurationUpdate" => "Aktualizace konfigurace",
                "WorkflowAdded" => "Přidáno workflow",
                "WorkflowRemoved" => "Odebráno workflow",
                "ToolAdded" => "Přidán nástroj",
                "ToolRemoved" => "Odebrán nástroj",
                "OrchestratorAdded" => "Přidán orchestrátor",
                "OrchestratorRemoved" => "Odebrán orchestrátor",
                "CustomerInfoUpdate" => "Aktualizace zákazníka",
                "RequirementUpdate" => "Úprava požadavku",
                _ => type
            };
        }

        private string GetChangeIcon(string type)
        {
            return type switch
            {
                "StatusChange" => "fas fa-exchange-alt",
                "ConfigurationUpdate" => "fas fa-cog",
                "WorkflowAdded" or "WorkflowRemoved" => "fas fa-project-diagram",
                "ToolAdded" or "ToolRemoved" => "fas fa-tools",
                "OrchestratorAdded" or "OrchestratorRemoved" => "fas fa-robot",
                "CustomerInfoUpdate" => "fas fa-user",
                "RequirementUpdate" => "fas fa-edit",
                _ => "fas fa-history"
            };
        }

        private string GetChangeColor(string type)
        {
            return type switch
            {
                "StatusChange" => "info",
                "ConfigurationUpdate" => "warning",
                "WorkflowAdded" or "ToolAdded" or "OrchestratorAdded" => "success",
                "WorkflowRemoved" or "ToolRemoved" or "OrchestratorRemoved" => "danger",
                _ => "secondary"
            };
        }
    }

    /// <summary>
    /// DTO pro vytvoření záznamu historie
    /// </summary>
    public class CreateProjectHistoryDto
    {
        public Guid ProjectId { get; set; }
        public string ChangeType { get; set; }
        public string Description { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ChangedBy { get; set; }
        public string Notes { get; set; }
    }
}