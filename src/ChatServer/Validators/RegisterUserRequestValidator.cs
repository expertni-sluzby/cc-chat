using FluentValidation;
using ChatServer.Models.DTOs;

namespace ChatServer.Validators;

/// <summary>
/// Validator for user registration requests
/// </summary>
public class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty()
            .WithMessage("Nickname is required")
            .Length(3, 20)
            .WithMessage("Nickname must be between 3 and 20 characters")
            .Matches("^[a-zA-Z0-9_]+$")
            .WithMessage("Nickname can only contain letters, numbers, and underscores");
    }
}
