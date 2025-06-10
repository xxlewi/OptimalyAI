using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Business
{
    public class WorkflowTemplateDto : BaseDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string RequestType { get; set; }
        public bool IsActive { get; set; }
        public int Version { get; set; }
        public string Configuration { get; set; }
        public List<WorkflowStepDto> Steps { get; set; }
    }

    public class CreateWorkflowTemplateDto : CreateDtoBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string RequestType { get; set; }
        public string Configuration { get; set; }
        public List<CreateWorkflowStepDto> Steps { get; set; }
    }

    public class UpdateWorkflowTemplateDto : UpdateDtoBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool? IsActive { get; set; }
        public string Configuration { get; set; }
    }
}