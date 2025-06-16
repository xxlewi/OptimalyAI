using System;
using System.Threading.Tasks;
using OAI.Core.DTOs.Workflow;

namespace OAI.Core.Interfaces.Workflow
{
    public interface IWorkflowDesignerService
    {
        Task<WorkflowDesignerDto> GetWorkflowAsync(Guid projectId);
        Task<WorkflowDesignerDto> SaveWorkflowAsync(Guid projectId, SaveWorkflowDto dto);
        Task<WorkflowValidationResult> ValidateWorkflowAsync(WorkflowDesignerDto workflow);
        Task<WorkflowExportDto> ExportWorkflowAsync(Guid projectId, string format = "json");
        Task<WorkflowDesignerDto> ImportWorkflowAsync(Guid projectId, string data, string format = "json");
    }
}