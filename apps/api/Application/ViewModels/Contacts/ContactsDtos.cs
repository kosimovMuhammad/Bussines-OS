using FluentValidation;

namespace BusinessOS.Application.ViewModels.Contacts;

// BranchId is only honored for the caller when they're Role.Owner - see
// ContactsService.CreateAsync/UpdateAsync. Any other caller has it silently ignored
// (forced to their own branch on create, left unchanged on update).
public record CreateContactRequest(
    string FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Position,
    string? CompanyName,
    string? Notes,
    string? OwnerId,
    List<string>? Tags,
    string? BranchId = null);

public record UpdateContactRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Position,
    string? CompanyName,
    string? Notes,
    string? OwnerId,
    List<string>? Tags,
    string? BranchId = null);

public record QueryContactsRequest(string? Search, string? Tag, string? OwnerId, bool IncludeArchived = false, int Page = 1, int Limit = 20);

public record ContactDto(
    string Id,
    string CompanyId,
    string FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Position,
    string? CompanyName,
    string? Notes,
    string? OwnerId,
    string? OwnerName,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false,
    string? BranchId = null,
    string? BranchName = null);

public record BulkUpdateContactsRequest(List<string> Ids, UpdateContactRequest Patch);

public record ContactDealSummary(string Id, string Title, string Stage, decimal Value, string Currency);

public record ContactTaskSummary(string Id, string Title, string Status, DateTime? DueDate);

public record ContactCommunicationSummary(
    string Id,
    string Channel,
    string Direction,
    string Content,
    string Status,
    DateTime Timestamp,
    string? AiSummary);

public record ContactDetailDto(
    string Id,
    string CompanyId,
    string FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Position,
    string? CompanyName,
    string? Notes,
    string? OwnerId,
    string? OwnerName,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ContactDealSummary> Deals,
    IReadOnlyList<ContactTaskSummary> Tasks,
    IReadOnlyList<ContactCommunicationSummary> Communications,
    bool IsArchived = false,
    string? BranchId = null,
    string? BranchName = null)
    : ContactDto(Id, CompanyId, FirstName, LastName, Email, Phone, Position, CompanyName, Notes, OwnerId, OwnerName, Tags, CreatedAt, UpdatedAt, IsArchived, BranchId, BranchName);

public static class PhoneUtil
{
    private static readonly System.Text.RegularExpressions.Regex E164 = new(@"^\+[1-9]\d{1,14}$");

    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return phone;
        }

        var trimmed = phone.Trim();
        return E164.IsMatch(trimmed) ? trimmed : trimmed;
    }

    public static bool IsValid(string? phone) => phone is null || E164.IsMatch(phone.Trim());
}

public class CreateContactRequestValidator : AbstractValidator<CreateContactRequest>
{
    public CreateContactRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.Phone).Must(PhoneUtil.IsValid).WithMessage("Phone must be in E.164 format").When(x => x.Phone is not null);
    }
}

public class UpdateContactRequestValidator : AbstractValidator<UpdateContactRequest>
{
    public UpdateContactRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().When(x => x.FirstName is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.Phone).Must(PhoneUtil.IsValid).WithMessage("Phone must be in E.164 format").When(x => x.Phone is not null);
    }
}

public class BulkUpdateContactsRequestValidator : AbstractValidator<BulkUpdateContactsRequest>
{
    public BulkUpdateContactsRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(ids => ids.Count <= 200).WithMessage("A bulk operation is limited to 200 ids at a time");
        RuleFor(x => x.Patch).SetValidator(new UpdateContactRequestValidator());
    }
}
