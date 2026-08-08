using FluentValidation;

namespace BusinessOS.Application.ViewModels.Finance;

public record TemplateDto(string Id, string Name, string Type, string Content, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public record CreateTemplateRequest(string Name, TemplateType Type, string Content, bool IsActive = true);

public record UpdateTemplateRequest(string? Name, TemplateType? Type, string? Content, bool? IsActive);

public record QueryTemplatesRequest(TemplateType? Type, bool? IsActive);

public class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    public CreateTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Content).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
    }
}

public class UpdateTemplateRequestValidator : AbstractValidator<UpdateTemplateRequest>
{
    public UpdateTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Content).NotEmpty().When(x => x.Content is not null);
        RuleFor(x => x.Type!.Value).IsInEnum().When(x => x.Type is not null);
    }
}
