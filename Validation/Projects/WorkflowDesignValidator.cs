using FluentValidation;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Projects;
using System.Text.Json;

namespace OptimalyAI.Validation.Projects
{
    /// <summary>
    /// Validátor pro ukládání workflow designu
    /// </summary>
    public class SaveProjectWorkflowValidator : SimpleBaseValidator<SaveProjectWorkflowDto>
    {
        private readonly IToolRegistry _toolRegistry;

        public SaveProjectWorkflowValidator(IToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;

            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ID projektu je povinné");

            RuleFor(x => x.TriggerType)
                .NotEmpty().WithMessage("Typ triggeru je povinný")
                .Must(x => new[] { "Manual", "Schedule", "Event" }.Contains(x))
                .WithMessage("Neplatný typ triggeru. Povolené hodnoty: Manual, Schedule, Event");

            RuleFor(x => x.Schedule)
                .NotEmpty().When(x => x.TriggerType == "Schedule")
                .WithMessage("Plán je povinný pro plánované spouštění")
                .Must(BeValidCronExpression).When(x => x.TriggerType == "Schedule" && !string.IsNullOrEmpty(x.Schedule))
                .WithMessage("Neplatný cron výraz");

            RuleFor(x => x.Stages)
                .NotEmpty().WithMessage("Workflow musí obsahovat alespoň jeden krok")
                .Must(stages => HaveUniqueNames(stages)).WithMessage("Názvy kroků musí být unikátní")
                .Must(stages => HaveValidOrder(stages)).WithMessage("Pořadí kroků musí být souvislé (1, 2, 3...)");

            RuleForEach(x => x.Stages)
                .ChildRules(stage => {
                    stage.RuleFor(s => s.Name).NotEmpty().WithMessage("Název stage je povinný");
                    stage.RuleFor(s => s.OrchestratorType).NotEmpty().WithMessage("Orchestrátor je povinný");
                });
        }

        private bool BeValidCronExpression(string cron)
        {
            if (string.IsNullOrWhiteSpace(cron))
                return false;

            var parts = cron.Split(' ');
            // Základní cron má 5 částí, rozšířený může mít 6
            return parts.Length == 5 || parts.Length == 6;
        }

        private bool HaveUniqueNames(List<SaveProjectStageDto> stages)
        {
            if (stages == null || !stages.Any())
                return true;

            var names = stages.Select(s => s.Name?.ToLower()).Where(n => !string.IsNullOrEmpty(n)).ToList();
            return names.Count == names.Distinct().Count();
        }

        private bool HaveValidOrder(List<SaveProjectStageDto> stages)
        {
            if (stages == null || !stages.Any())
                return true;

            var orders = stages.Select(s => s.Order).OrderBy(o => o).ToList();
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != i + 1)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Validátor pro jednotlivé stage ve workflow
    /// </summary>
    public class WorkflowStageValidator : AbstractValidator<SaveProjectStageDto>
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly string[] _validOrchestrators = new[] { "ConversationOrchestrator", "ToolChainOrchestrator", "CustomOrchestrator" };
        private readonly string[] _validReActAgents = new[] { "ConversationReActAgent", "AnalysisReActAgent", "ProcessingReActAgent" };

        public WorkflowStageValidator(IToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název kroku je povinný")
                .MaximumLength(200).WithMessage("Název může mít maximálně 200 znaků");

            RuleFor(x => x.OrchestratorType)
                .NotEmpty().WithMessage("Každý krok musí mít orchestrátor")
                .Must(x => _validOrchestrators.Contains(x))
                .WithMessage($"Neplatný orchestrátor. Povolené hodnoty: {string.Join(", ", _validOrchestrators)}");

            RuleFor(x => x.ReActAgentType)
                .Must(x => string.IsNullOrEmpty(x) || _validReActAgents.Contains(x))
                .WithMessage($"Neplatný ReAct agent. Povolené hodnoty: {string.Join(", ", _validReActAgents)}");

            RuleFor(x => x)
                .Must(HaveToolsOrReActAgent)
                .WithMessage("Každý krok musí mít alespoň jeden nástroj nebo ReAct agenta");

            RuleFor(x => x.OrchestratorConfiguration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.OrchestratorConfiguration))
                .WithMessage("Konfigurace orchestrátoru musí být validní JSON");

            RuleFor(x => x.ReActAgentConfiguration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.ReActAgentConfiguration))
                .WithMessage("Konfigurace ReAct agenta musí být validní JSON");

            When(x => x.Tools != null && x.Tools.Any(), () =>
            {
                RuleForEach(x => x.Tools)
                    .MustAsync(async (tool, cancellation) => await ValidateToolAsync(tool))
                    .WithMessage("Nástroj nebyl nalezen nebo je neplatný");
            });
        }

        private bool HaveToolsOrReActAgent(SaveProjectStageDto stage)
        {
            return !string.IsNullOrEmpty(stage.ReActAgentType) || 
                   (stage.Tools != null && stage.Tools.Any());
        }

        private async Task<bool> ValidateToolAsync(WorkflowStageToolDto toolDto)
        {
            if (string.IsNullOrEmpty(toolDto.ToolId))
                return false;

            var tool = await _toolRegistry.GetToolAsync(toolDto.ToolId);
            return tool != null;
        }

        private bool BeValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return true;

            try
            {
                using var doc = JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Validátor pro testování workflow
    /// </summary>
    public class TestProjectWorkflowValidator : SimpleBaseValidator<TestProjectWorkflowDto>
    {
        public TestProjectWorkflowValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ID projektu je povinné");

            RuleFor(x => x.TestData)
                .NotEmpty().WithMessage("Testovací data jsou povinná")
                .Must(BeValidJson).WithMessage("Testovací data musí být validní JSON");
        }

        private bool BeValidJson(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return true;

            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Validátor pro vytvoření projektu ze šablony
    /// </summary>
    public class CreateProjectFromTemplateValidator : SimpleBaseValidator<CreateProjectDto>
    {
        public CreateProjectFromTemplateValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název projektu je povinný")
                .MaximumLength(200).WithMessage("Název může mít maximálně 200 znaků");

            RuleFor(x => x.CustomerId)
                .NotEmpty().When(x => !string.IsNullOrEmpty(x.CustomerName))
                .WithMessage("ID zákazníka je povinné při zadání jména zákazníka");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Popis může mít maximálně 500 znaků");
        }
    }
}