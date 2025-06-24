using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OAI.Core.DTOs.Workflow;

namespace OAI.Core.Interfaces.Discovery
{
    public interface IWorkflowBuilder
    {
        Task<WorkflowDesignerDto> BuildWorkflowAsync(
            WorkflowIntent intent,
            List<ComponentMatch> components,
            CancellationToken cancellationToken);

        Task<WorkflowDesignerDto> UpdateWorkflowAsync(
            string currentWorkflowJson,
            WorkflowIntent intent,
            List<ComponentMatch> components,
            CancellationToken cancellationToken);
    }
}