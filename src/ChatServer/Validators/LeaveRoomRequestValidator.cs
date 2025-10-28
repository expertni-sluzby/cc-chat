using FluentValidation;
using ChatServer.Models.DTOs;

namespace ChatServer.Validators;

/// <summary>
/// Validator for leave room requests
/// </summary>
public class LeaveRoomRequestValidator : AbstractValidator<LeaveRoomRequest>
{
    public LeaveRoomRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty()
            .WithMessage("Nickname is required");
    }
}
