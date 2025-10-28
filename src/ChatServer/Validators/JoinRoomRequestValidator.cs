using FluentValidation;
using ChatServer.Models.DTOs;

namespace ChatServer.Validators;

/// <summary>
/// Validator for join room requests
/// </summary>
public class JoinRoomRequestValidator : AbstractValidator<JoinRoomRequest>
{
    public JoinRoomRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty()
            .WithMessage("Nickname is required");
    }
}
