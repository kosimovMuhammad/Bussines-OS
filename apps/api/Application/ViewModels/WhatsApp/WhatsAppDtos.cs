using FluentValidation;

namespace BusinessOS.Application.ViewModels.WhatsApp;

public record ConnectWhatsAppAccountRequest(string PhoneNumberId, string WabaId, string AccessToken, string? VerifiedName);

public record SendWhatsAppRequest(string ContactId, string Message);

public record WhatsAppAccountDto(string Id, string CompanyId, string PhoneNumberId, string WabaId, string? VerifiedName, DateTime CreatedAt);

public record IncomingWhatsAppMessage(string ProviderMessageId, string PhoneNumberId, string From, string Text, DateTime Timestamp);

public class ConnectWhatsAppAccountRequestValidator : AbstractValidator<ConnectWhatsAppAccountRequest>
{
    public ConnectWhatsAppAccountRequestValidator()
    {
        RuleFor(x => x.PhoneNumberId).NotEmpty();
        RuleFor(x => x.WabaId).NotEmpty();
        RuleFor(x => x.AccessToken).NotEmpty();
    }
}

public class SendWhatsAppRequestValidator : AbstractValidator<SendWhatsAppRequest>
{
    public SendWhatsAppRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}
