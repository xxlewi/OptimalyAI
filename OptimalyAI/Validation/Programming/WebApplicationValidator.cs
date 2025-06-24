using FluentValidation;
using OAI.Core.DTOs.Programming;
using OptimalyAI.Validation;
using System.IO;

namespace OptimalyAI.Validation.Programming
{
    /// <summary>
    /// Validator pro vytvoření webové aplikace
    /// </summary>
    public class CreateWebApplicationDtoValidator : SimpleBaseValidator<CreateWebApplicationDto>
    {
        public CreateWebApplicationDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název aplikace je povinný")
                .MaximumLength(200).WithMessage("Název aplikace může mít maximálně 200 znaků");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Popis může mít maximálně 1000 znaků");

            RuleFor(x => x.ProjectPath)
                .NotEmpty().WithMessage("Cesta k projektu je povinná")
                .MaximumLength(500).WithMessage("Cesta k projektu může mít maximálně 500 znaků")
                .Must(BeValidPath).WithMessage("Cesta k projektu musí existovat");

            RuleFor(x => x.Url)
                .MaximumLength(500).WithMessage("URL může mít maximálně 500 znaků")
                .Must(BeValidUrlOrEmpty).WithMessage("URL musí být ve správném formátu");

            RuleFor(x => x.ProgrammingLanguage)
                .NotEmpty().WithMessage("Programovací jazyk je povinný")
                .MaximumLength(50).WithMessage("Programovací jazyk může mít maximálně 50 znaků");

            RuleFor(x => x.Framework)
                .NotEmpty().WithMessage("Framework je povinný")
                .MaximumLength(100).WithMessage("Framework může mít maximálně 100 znaků");

            RuleFor(x => x.Architecture)
                .MaximumLength(100).WithMessage("Architektura může mít maximálně 100 znaků");

            RuleFor(x => x.Database)
                .MaximumLength(100).WithMessage("Databáze může mít maximálně 100 znaků");

            RuleFor(x => x.Version)
                .NotEmpty().WithMessage("Verze je povinná")
                .MaximumLength(20).WithMessage("Verze může mít maximálně 20 znaků");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status je povinný")
                .Must(BeValidStatus).WithMessage("Status musí být Development, Testing, Production nebo Maintenance");

            RuleFor(x => x.GitRepository)
                .MaximumLength(500).WithMessage("Git repository může mít maximálně 500 znaků");

            RuleFor(x => x.Tags)
                .MaximumLength(500).WithMessage("Tagy mohou mít maximálně 500 znaků");

            RuleFor(x => x.Priority)
                .NotEmpty().WithMessage("Priorita je povinná")
                .Must(BeValidPriority).WithMessage("Priorita musí být Low, Medium, High nebo Critical");
        }

        private static bool BeValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                return Directory.Exists(path) || File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static bool BeValidUrlOrEmpty(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }

        private static bool BeValidStatus(string status)
        {
            var validStatuses = new[] { "Development", "Testing", "Production", "Maintenance" };
            return validStatuses.Contains(status);
        }

        private static bool BeValidPriority(string priority)
        {
            var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
            return validPriorities.Contains(priority);
        }
    }

    /// <summary>
    /// Validator pro úpravu webové aplikace
    /// </summary>
    public class UpdateWebApplicationDtoValidator : SimpleBaseValidator<UpdateWebApplicationDto>
    {
        public UpdateWebApplicationDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Název aplikace je povinný")
                .MaximumLength(200).WithMessage("Název aplikace může mít maximálně 200 znaků");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Popis může mít maximálně 1000 znaků");

            RuleFor(x => x.ProjectPath)
                .NotEmpty().WithMessage("Cesta k projektu je povinná")
                .MaximumLength(500).WithMessage("Cesta k projektu může mít maximálně 500 znaků")
                .Must(BeValidPath).WithMessage("Cesta k projektu musí existovat");

            RuleFor(x => x.Url)
                .MaximumLength(500).WithMessage("URL může mít maximálně 500 znaků")
                .Must(BeValidUrlOrEmpty).WithMessage("URL musí být ve správném formátu");

            RuleFor(x => x.ProgrammingLanguage)
                .NotEmpty().WithMessage("Programovací jazyk je povinný")
                .MaximumLength(50).WithMessage("Programovací jazyk může mít maximálně 50 znaků");

            RuleFor(x => x.Framework)
                .NotEmpty().WithMessage("Framework je povinný")
                .MaximumLength(100).WithMessage("Framework může mít maximálně 100 znaků");

            RuleFor(x => x.Architecture)
                .MaximumLength(100).WithMessage("Architektura může mít maximálně 100 znaků");

            RuleFor(x => x.Database)
                .MaximumLength(100).WithMessage("Databáze může mít maximálně 100 znaků");

            RuleFor(x => x.Version)
                .NotEmpty().WithMessage("Verze je povinná")
                .MaximumLength(20).WithMessage("Verze může mít maximálně 20 znaků");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status je povinný")
                .Must(BeValidStatus).WithMessage("Status musí být Development, Testing, Production nebo Maintenance");

            RuleFor(x => x.GitRepository)
                .MaximumLength(500).WithMessage("Git repository může mít maximálně 500 znaků");

            RuleFor(x => x.Tags)
                .MaximumLength(500).WithMessage("Tagy mohou mít maximálně 500 znaků");

            RuleFor(x => x.Priority)
                .NotEmpty().WithMessage("Priorita je povinná")
                .Must(BeValidPriority).WithMessage("Priorita musí být Low, Medium, High nebo Critical");
        }

        private static bool BeValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                return Directory.Exists(path) || File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static bool BeValidUrlOrEmpty(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }

        private static bool BeValidStatus(string status)
        {
            var validStatuses = new[] { "Development", "Testing", "Production", "Maintenance" };
            return validStatuses.Contains(status);
        }

        private static bool BeValidPriority(string priority)
        {
            var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
            return validPriorities.Contains(priority);
        }
    }
}