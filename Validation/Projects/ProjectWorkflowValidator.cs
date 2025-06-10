using FluentValidation;
using OAI.Core.DTOs.Projects;
using System.Text.RegularExpressions;
using OptimalyAI.Validation;

namespace OptimalyAI.Validation.Projects
{
    public class CreateProjectWorkflowValidator : SimpleBaseValidator<CreateProjectWorkflowDto>
    {
        public CreateProjectWorkflowValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ID projektu je povinné");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název workflow je povinný")
                .MaximumLength(200).WithMessage("Název může mít maximálně 200 znaků");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Popis může mít maximálně 500 znaků");

            RuleFor(x => x.WorkflowType)
                .NotEmpty().WithMessage("Typ workflow je povinný")
                .Must(BeValidWorkflowType).WithMessage("Neplatný typ workflow. Povolené hodnoty: Sequential, Parallel, Conditional");

            RuleFor(x => x.TriggerType)
                .NotEmpty().WithMessage("Typ triggeru je povinný")
                .Must(BeValidTriggerType).WithMessage("Neplatný typ triggeru. Povolené hodnoty: Manual, Scheduled, Event");

            RuleFor(x => x.CronExpression)
                .Must(BeValidCronExpression).When(x => x.TriggerType == "Scheduled")
                .WithMessage("Neplatný CRON výraz")
                .NotEmpty().When(x => x.TriggerType == "Scheduled")
                .WithMessage("CRON výraz je povinný pro plánované workflow");

            RuleFor(x => x.Steps)
                .NotEmpty().WithMessage("Workflow musí obsahovat alespoň jeden krok")
                .Must(HaveUniqueStepOrders).WithMessage("Pořadí kroků musí být unikátní");

            RuleForEach(x => x.Steps).SetValidator(new CreateWorkflowStepValidator());
        }

        private bool BeValidWorkflowType(string type)
        {
            var validTypes = new[] { "Sequential", "Parallel", "Conditional" };
            return validTypes.Contains(type);
        }

        private bool BeValidTriggerType(string type)
        {
            var validTypes = new[] { "Manual", "Scheduled", "Event" };
            return validTypes.Contains(type);
        }

        private bool BeValidCronExpression(string cron)
        {
            if (string.IsNullOrWhiteSpace(cron)) return true;
            
            // Základní validace CRON výrazu (5 nebo 6 částí)
            var parts = cron.Split(' ');
            return parts.Length >= 5 && parts.Length <= 6;
        }

        private bool HaveUniqueStepOrders(List<CreateWorkflowStepDto> steps)
        {
            if (steps == null || !steps.Any()) return true;
            var orders = steps.Select(s => s.Order).ToList();
            return orders.Count == orders.Distinct().Count();
        }
    }

    public class CreateWorkflowStepValidator : AbstractValidator<CreateWorkflowStepDto>
    {
        public CreateWorkflowStepValidator()
        {
            RuleFor(x => x.Order)
                .GreaterThanOrEqualTo(1).WithMessage("Pořadí kroku musí být alespoň 1");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název kroku je povinný")
                .MaximumLength(200).WithMessage("Název kroku může mít maximálně 200 znaků");

            RuleFor(x => x.Type)
                .NotEmpty().WithMessage("Typ kroku je povinný")
                .Must(BeValidStepType).WithMessage("Neplatný typ kroku. Povolené hodnoty: Tool, Orchestrator, Condition, Wait, Script");

            RuleFor(x => x.Action)
                .NotEmpty().WithMessage("Akce kroku je povinná");

            RuleFor(x => x.TimeoutSeconds)
                .InclusiveBetween(1, 3600).When(x => x.TimeoutSeconds.HasValue)
                .WithMessage("Timeout musí být mezi 1 a 3600 sekund");

            RuleFor(x => x.RetryCount)
                .InclusiveBetween(0, 5).When(x => x.RetryCount.HasValue)
                .WithMessage("Počet opakování musí být mezi 0 a 5");

            RuleFor(x => x.Condition)
                .Must(BeValidCondition).When(x => !string.IsNullOrEmpty(x.Condition))
                .WithMessage("Neplatná podmínka");
        }

        private bool BeValidStepType(string type)
        {
            var validTypes = new[] { "Tool", "Orchestrator", "Condition", "Wait", "Script" };
            return validTypes.Contains(type);
        }

        private bool BeValidCondition(string condition)
        {
            // Základní validace podmínky - můžeme rozšířit
            return !string.IsNullOrWhiteSpace(condition);
        }
    }
}