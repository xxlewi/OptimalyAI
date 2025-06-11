using FluentValidation;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using System.Text.Json;

namespace OptimalyAI.Validation.Projects
{
    public class CreateProjectStageValidator : SimpleBaseValidator<CreateProjectStageDto>
    {
        public CreateProjectStageValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ID projektu je povinné");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název kroku je povinný")
                .MaximumLength(200).WithMessage("Název může mít maximálně 200 znaků");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Popis může mít maximálně 1000 znaků");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Neplatný typ kroku");

            RuleFor(x => x.OrchestratorType)
                .NotEmpty().WithMessage("Typ orchestrátoru je povinný")
                .MaximumLength(100).WithMessage("Typ orchestrátoru může mít maximálně 100 znaků");

            RuleFor(x => x.ExecutionStrategy)
                .IsInEnum().WithMessage("Neplatná strategie vykonávání");

            RuleFor(x => x.OrchestratorConfiguration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.OrchestratorConfiguration))
                .WithMessage("Konfigurace orchestrátoru musí být validní JSON");

            RuleFor(x => x.ReActAgentConfiguration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.ReActAgentConfiguration))
                .WithMessage("Konfigurace ReAct agenta musí být validní JSON");

            RuleFor(x => x.Tools)
                .Must(x => x == null || x.All(t => !string.IsNullOrEmpty(t.ToolId)))
                .WithMessage("Všechny nástroje musí mít ID");

            RuleFor(x => x)
                .Must(HaveToolsOrReActAgent)
                .WithMessage("Krok musí mít alespoň jeden nástroj nebo ReAct agenta");
        }

        private bool BeValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return true;

            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool HaveToolsOrReActAgent(CreateProjectStageDto dto)
        {
            return !string.IsNullOrEmpty(dto.ReActAgentType) || 
                   (dto.Tools != null && dto.Tools.Any());
        }
    }

    public class UpdateProjectStageValidator : SimpleBaseValidator<UpdateProjectStageDto>
    {
        public UpdateProjectStageValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název kroku je povinný")
                .MaximumLength(200).WithMessage("Název může mít maximálně 200 znaků");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Popis může mít maximálně 1000 znaků");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Neplatný typ kroku");

            RuleFor(x => x.OrchestratorType)
                .NotEmpty().WithMessage("Typ orchestrátoru je povinný")
                .MaximumLength(100).WithMessage("Typ orchestrátoru může mít maximálně 100 znaků");

            RuleFor(x => x.ExecutionStrategy)
                .IsInEnum().WithMessage("Neplatná strategie vykonávání");

            RuleFor(x => x.OrchestratorConfiguration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.OrchestratorConfiguration))
                .WithMessage("Konfigurace orchestrátoru musí být validní JSON");

            RuleFor(x => x.ReActAgentConfiguration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.ReActAgentConfiguration))
                .WithMessage("Konfigurace ReAct agenta musí být validní JSON");

            RuleFor(x => x.MaxRetries)
                .InclusiveBetween(0, 10).WithMessage("Maximální počet pokusů musí být mezi 0 a 10");

            RuleFor(x => x.TimeoutSeconds)
                .InclusiveBetween(1, 3600).WithMessage("Timeout musí být mezi 1 a 3600 sekund");

            RuleFor(x => x.Metadata)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.Metadata))
                .WithMessage("Metadata musí být validní JSON");
        }

        private bool BeValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return true;

            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CreateProjectStageToolValidator : SimpleBaseValidator<CreateProjectStageToolDto>
    {
        public CreateProjectStageToolValidator()
        {
            RuleFor(x => x.ToolId)
                .NotEmpty().WithMessage("ID nástroje je povinné")
                .MaximumLength(100).WithMessage("ID nástroje může mít maximálně 100 znaků");

            RuleFor(x => x.ToolName)
                .NotEmpty().WithMessage("Název nástroje je povinný")
                .MaximumLength(200).WithMessage("Název nástroje může mít maximálně 200 znaků");

            RuleFor(x => x.Configuration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.Configuration))
                .WithMessage("Konfigurace nástroje musí být validní JSON");

            RuleFor(x => x.InputMapping)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.InputMapping))
                .WithMessage("Mapování vstupu musí být validní JSON");

            RuleFor(x => x.OutputMapping)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.OutputMapping))
                .WithMessage("Mapování výstupu musí být validní JSON");

            RuleFor(x => x.MaxRetries)
                .InclusiveBetween(0, 10).WithMessage("Maximální počet pokusů musí být mezi 0 a 10");

            RuleFor(x => x.TimeoutSeconds)
                .InclusiveBetween(1, 300).WithMessage("Timeout musí být mezi 1 a 300 sekund");
        }

        private bool BeValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return true;

            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}