using FluentValidation;
using OAI.Core.DTOs.Projects;
using OptimalyAI.Validation;

namespace OptimalyAI.Validation.Projects
{
    public class CreateProjectMetricValidator : SimpleBaseValidator<CreateProjectMetricDto>
    {
        public CreateProjectMetricValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ID projektu je povinné");

            RuleFor(x => x.MetricType)
                .NotEmpty().WithMessage("Typ metriky je povinný")
                .MaximumLength(50).WithMessage("Typ metriky může mít maximálně 50 znaků")
                .Must(BeValidMetricType).WithMessage("Neplatný typ metriky. Povolené hodnoty: Performance, Cost, Quality, Usage, ToolUsage, ApiCall");

            RuleFor(x => x.MetricName)
                .NotEmpty().WithMessage("Název metriky je povinný")
                .MaximumLength(100).WithMessage("Název metriky může mít maximálně 100 znaků");

            RuleFor(x => x.Value)
                .GreaterThanOrEqualTo(0).WithMessage("Hodnota metriky musí být nezáporná");

            RuleFor(x => x.Unit)
                .MaximumLength(20).WithMessage("Jednotka může mít maximálně 20 znaků");

            RuleFor(x => x.Period)
                .MaximumLength(20).WithMessage("Perioda může mít maximálně 20 znaků")
                .Must(BeValidPeriod).When(x => !string.IsNullOrEmpty(x.Period))
                .WithMessage("Neplatná perioda. Povolené hodnoty: Hour, Day, Week, Month, Year, Execution");

            RuleFor(x => x.BillingRate)
                .GreaterThanOrEqualTo(0).When(x => x.BillingRate.HasValue)
                .WithMessage("Sazba pro fakturaci musí být nezáporná")
                .LessThanOrEqualTo(1000000).When(x => x.BillingRate.HasValue)
                .WithMessage("Sazba pro fakturaci je příliš vysoká");

            RuleFor(x => x.Metadata)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.Metadata))
                .WithMessage("Metadata musí být platný JSON");
        }

        private bool BeValidMetricType(string type)
        {
            var validTypes = new[] { "Performance", "Cost", "Quality", "Usage", "ToolUsage", "ApiCall" };
            return validTypes.Contains(type);
        }

        private bool BeValidPeriod(string period)
        {
            var validPeriods = new[] { "Hour", "Day", "Week", "Month", "Year", "Execution" };
            return validPeriods.Contains(period);
        }

        private bool BeValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return true;
            try
            {
                System.Text.Json.JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}