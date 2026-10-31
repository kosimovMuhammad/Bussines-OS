using FluentValidation;

namespace BusinessOS.Application.ViewModels.WhatsApp;

public record SendPlatformWhatsAppTextRequest(string To, string Message);

public record SendPlatformWhatsAppTemplateRequest(string To, string TemplateName, string LanguageCode);

public record WhatsAppSendResult(bool Success, string? MessageId);

public class SendPlatformWhatsAppTextRequestValidator : AbstractValidator<SendPlatformWhatsAppTextRequest>
{
    public SendPlatformWhatsAppTextRequestValidator()
    {
        RuleFor(x => x.To).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}

public class SendPlatformWhatsAppTemplateRequestValidator : AbstractValidator<SendPlatformWhatsAppTemplateRequest>
{
    public SendPlatformWhatsAppTemplateRequestValidator()
    {
        RuleFor(x => x.To).NotEmpty();
        RuleFor(x => x.TemplateName).NotEmpty();
        RuleFor(x => x.LanguageCode).NotEmpty();
    }
}
