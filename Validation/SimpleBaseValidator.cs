using FluentValidation;

namespace OptimalyAI.Validation;

// Jednoduchá verze bez reflexe
public abstract class SimpleBaseValidator<T> : AbstractValidator<T>
{
    protected SimpleBaseValidator()
    {
        // Společné validační pravidla
    }
}

// Příklad použití - vytvořte si konkrétní validátory podle potřeby
// public class CreateUserDtoValidator : SimpleBaseValidator<CreateUserDto>
// {
//     public CreateUserDtoValidator()
//     {
//         RuleFor(x => x.Name)
//             .NotEmpty()
//             .WithMessage("Jméno je povinné")
//             .MaximumLength(100)
//             .WithMessage("Jméno nesmí být delší než 100 znaků");
//
//         RuleFor(x => x.Email)
//             .NotEmpty()
//             .WithMessage("Email je povinný")
//             .EmailAddress()
//             .WithMessage("Email není v platném formátu");
//     }
// }