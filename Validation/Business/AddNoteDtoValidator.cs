using FluentValidation;
using OAI.Core.DTOs.Business;
using OptimalyAI.Validation;

namespace OptimalyAI.Validation.Business
{
    public class AddNoteDtoValidator : SimpleBaseValidator<AddNoteDto>
    {
        public AddNoteDtoValidator()
        {
            RuleFor(x => x.Content)
                .NotEmpty()
                .WithMessage("Obsah poznámky je povinný")
                .MaximumLength(2000)
                .WithMessage("Obsah poznámky nesmí překročit 2000 znaků");

            RuleFor(x => x.Author)
                .NotEmpty()
                .WithMessage("Autor poznámky je povinný")
                .MaximumLength(100)
                .WithMessage("Autor nesmí překročit 100 znaků");

            RuleFor(x => x.Type)
                .IsInEnum()
                .WithMessage("Neplatný typ poznámky");
        }
    }
}