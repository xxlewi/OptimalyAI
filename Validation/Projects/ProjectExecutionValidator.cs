using FluentValidation;
using OAI.Core.DTOs.Projects;
using OptimalyAI.Validation;

namespace OptimalyAI.Validation.Projects
{
    public class StartProjectExecutionValidator : SimpleBaseValidator<StartProjectExecutionDto>
    {
        public StartProjectExecutionValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ID projektu je povinné");

            RuleFor(x => x.InitiatedBy)
                .MaximumLength(100).WithMessage("Iniciátor může mít maximálně 100 znaků");

            RuleFor(x => x.Parameters)
                .Must(BeValidParameterDictionary).When(x => x.Parameters != null && x.Parameters.Any())
                .WithMessage("Neplatné parametry");
        }

        private bool BeValidParameterDictionary(Dictionary<string, object> parameters)
        {
            if (parameters == null) return true;

            // Kontrola, že klíče nejsou prázdné
            return parameters.All(kvp => !string.IsNullOrWhiteSpace(kvp.Key));
        }
    }
}