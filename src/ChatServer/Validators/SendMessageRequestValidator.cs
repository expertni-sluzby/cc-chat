using FluentValidation;
using ChatServer.Models.DTOs;

namespace ChatServer.Validators;

/// <summary>
/// Validator for send message requests
/// </summary>
public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Author)
            .NotEmpty()
            .WithMessage("Author (nickname) is required");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Message content is required")
            .MaximumLength(1000)
            .WithMessage("Message content cannot exceed 1000 characters");
    }
}
