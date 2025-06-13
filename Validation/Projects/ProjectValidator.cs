using FluentValidation;
using OAI.Core.DTOs.Projects;
using OptimalyAI.Validation;

namespace OptimalyAI.Validation.Projects
{
    public class CreateProjectValidator : SimpleBaseValidator<CreateProjectDto>
    {
        public CreateProjectValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název projektu je povinný")
                .MaximumLength(200).WithMessage("Název projektu může mít maximálně 200 znaků");

            // Při submitování musí být vybrán zákazník nebo označen jako interní projekt
            // Ale nevyžaduje se validace pokud jsou všechna pole prázdná (uživatel ještě nevybral)
            RuleFor(x => x)
                .Must(x => {
                    // Pokud jsou všechna pole prázdná, nekontrolujeme (uživatel ještě nevybral)
                    if (!x.CustomerId.HasValue && string.IsNullOrEmpty(x.CustomerName))
                        return true;
                    
                    // Jinak musí být splněna jedna z podmínek
                    return x.CustomerId.HasValue || x.CustomerName == "Interní projekt" || !string.IsNullOrEmpty(x.CustomerName);
                })
                .WithMessage("Musíte vybrat existujícího zákazníka, vytvořit nového nebo označit projekt jako interní");

            // CustomerName je povinné pouze pokud se vybere možnost "nový zákazník"
            // (tj. CustomerId není vyplněno, ale CustomerName je neprázdný a není "Interní projekt")
            RuleFor(x => x.CustomerName)
                .NotEmpty().When(x => !x.CustomerId.HasValue && !string.IsNullOrEmpty(x.CustomerName) && x.CustomerName != "Interní projekt")
                .WithMessage("Jméno zákazníka je povinné při vytváření nového zákazníka")
                .MaximumLength(200).WithMessage("Jméno zákazníka může mít maximálně 200 znaků");

            RuleFor(x => x.CustomerEmail)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.CustomerEmail))
                .WithMessage("Neplatný formát emailu")
                .MaximumLength(100).WithMessage("Email může mít maximálně 100 znaků");

            RuleFor(x => x.CustomerPhone)
                .Matches(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{4,6}$")
                .When(x => !string.IsNullOrEmpty(x.CustomerPhone))
                .WithMessage("Neplatný formát telefonu")
                .MaximumLength(50).WithMessage("Telefon může mít maximálně 50 znaků");

            // CustomerRequirement je nepovinné
            RuleFor(x => x.CustomerRequirement)
                .MinimumLength(10).When(x => !string.IsNullOrEmpty(x.CustomerRequirement))
                .WithMessage("Požadavek zákazníka musí být podrobnější (min. 10 znaků)");

            RuleFor(x => x.EstimatedHours)
                .InclusiveBetween(0, 10000).When(x => x.EstimatedHours.HasValue)
                .WithMessage("Odhadovaný počet hodin musí být mezi 0 a 10000");

            RuleFor(x => x.HourlyRate)
                .InclusiveBetween(0, 100000).When(x => x.HourlyRate.HasValue)
                .WithMessage("Hodinová sazba musí být mezi 0 a 100000");

            RuleFor(x => x.DueDate)
                .GreaterThan(DateTime.Now.AddDays(-1)).When(x => x.DueDate.HasValue)
                .WithMessage("Termín dokončení nemůže být v minulosti");

            RuleFor(x => x.Configuration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.Configuration))
                .WithMessage("Konfigurace musí být platný JSON");

            RuleFor(x => x.ProjectType)
                .MaximumLength(50).WithMessage("Typ projektu může mít maximálně 50 znaků");
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

    public class UpdateProjectValidator : SimpleBaseValidator<UpdateProjectDto>
    {
        public UpdateProjectValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název projektu je povinný")
                .MaximumLength(200).WithMessage("Název projektu může mít maximálně 200 znaků");

            // CustomerName je povinné pouze pokud se vybere možnost "nový zákazník"
            // (tj. CustomerId není vyplněno, ale CustomerName je neprázdný a není "Interní projekt")
            RuleFor(x => x.CustomerName)
                .NotEmpty().When(x => !x.CustomerId.HasValue && !string.IsNullOrEmpty(x.CustomerName) && x.CustomerName != "Interní projekt")
                .WithMessage("Jméno zákazníka je povinné při vytváření nového zákazníka")
                .MaximumLength(200).WithMessage("Jméno zákazníka může mít maximálně 200 znaků");

            RuleFor(x => x.CustomerEmail)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.CustomerEmail))
                .WithMessage("Neplatný formát emailu")
                .MaximumLength(100).WithMessage("Email může mít maximálně 100 znaků");

            RuleFor(x => x.CustomerPhone)
                .Matches(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{4,6}$")
                .When(x => !string.IsNullOrEmpty(x.CustomerPhone))
                .WithMessage("Neplatný formát telefonu")
                .MaximumLength(50).WithMessage("Telefon může mít maximálně 50 znaků");

            // CustomerRequirement je nepovinné
            RuleFor(x => x.CustomerRequirement)
                .MinimumLength(10).When(x => !string.IsNullOrEmpty(x.CustomerRequirement))
                .WithMessage("Požadavek zákazníka musí být podrobnější (min. 10 znaků)");

            RuleFor(x => x.EstimatedHours)
                .InclusiveBetween(0, 10000).When(x => x.EstimatedHours.HasValue)
                .WithMessage("Odhadovaný počet hodin musí být mezi 0 a 10000");

            RuleFor(x => x.ActualHours)
                .InclusiveBetween(0, 10000).When(x => x.ActualHours.HasValue)
                .WithMessage("Skutečný počet hodin musí být mezi 0 a 10000");

            RuleFor(x => x.HourlyRate)
                .InclusiveBetween(0, 100000).When(x => x.HourlyRate.HasValue)
                .WithMessage("Hodinová sazba musí být mezi 0 a 100000");

            RuleFor(x => x.CompletedDate)
                .GreaterThan(x => x.StartDate).When(x => x.CompletedDate.HasValue && x.StartDate.HasValue)
                .WithMessage("Datum dokončení musí být po datu zahájení");

            RuleFor(x => x.Configuration)
                .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.Configuration))
                .WithMessage("Konfigurace musí být platný JSON");
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

    public class UpdateProjectStatusValidator : SimpleBaseValidator<UpdateProjectStatusDto>
    {
        public UpdateProjectStatusValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ID projektu je povinné");

            RuleFor(x => x.NewStatus)
                .IsInEnum().WithMessage("Neplatný status projektu");

            RuleFor(x => x.Reason)
                .MaximumLength(500).WithMessage("Důvod může mít maximálně 500 znaků");
        }
    }
}