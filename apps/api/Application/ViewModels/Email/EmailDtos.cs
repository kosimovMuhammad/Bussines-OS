using FluentValidation;

namespace BusinessOS.Application.ViewModels.Email;

public record SendEmailRequest(string ContactId, string Subject, string Body);

public class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    public SendEmailRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty();
        RuleFor(x => x.Body).NotEmpty();
    }
}
