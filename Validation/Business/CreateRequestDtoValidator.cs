using FluentValidation;
using OAI.Core.DTOs.Business;

namespace OptimalyAI.Validation.Business
{
    public class CreateRequestDtoValidator : SimpleBaseValidator<CreateRequestDto>
    {
        public CreateRequestDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Název požadavku je povinný")
                .MaximumLength(200).WithMessage("Název požadavku může mít maximálně 200 znaků");

            RuleFor(x => x.RequestType)
                .MaximumLength(50).WithMessage("Typ požadavku může mít maximálně 50 znaků")
                .When(x => !string.IsNullOrEmpty(x.RequestType));

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Popis může mít maximálně 1000 znaků")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.ClientId)
                .MaximumLength(50).WithMessage("ID klienta může mít maximálně 50 znaků")
                .When(x => !string.IsNullOrEmpty(x.ClientId));

            RuleFor(x => x.ClientName)
                .MaximumLength(200).WithMessage("Jméno klienta může mít maximálně 200 znaků")
                .When(x => !string.IsNullOrEmpty(x.ClientName));

            RuleFor(x => x.ProjectName)
                .MaximumLength(200).WithMessage("Název projektu může mít maximálně 200 znaků")
                .When(x => !string.IsNullOrEmpty(x.ProjectName));

            RuleFor(x => x.Priority)
                .IsInEnum().WithMessage("Neplatná priorita");

            RuleFor(x => x.Deadline)
                .GreaterThan(System.DateTime.Now.AddHours(-1))
                .WithMessage("Deadline musí být v budoucnosti")
                .When(x => x.Deadline.HasValue);

            RuleFor(x => x.EstimatedCost)
                .GreaterThanOrEqualTo(0).WithMessage("Odhadovaná cena nesmí být záporná")
                .When(x => x.EstimatedCost.HasValue);
        }
    }
}