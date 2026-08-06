using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Calendar;

public record CreateMeetingRequest(
    string Title,
    string? ContactId,
    string? DealId,
    DateTime StartAt,
    DateTime EndAt,
    string? Location,
    string? Description,
    List<string>? AttendeeUserIds);

public record UpdateMeetingRequest(
    string? Title,
    string? ContactId,
    string? DealId,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location,
    string? Description,
    List<string>? AttendeeUserIds);

public record UpdateMeetingStatusRequest(MeetingStatus Status);

public record QueryMeetingsRequest(DateTime? From, DateTime? To, string? ContactId, string? DealId, string? OrganizerId);

public record MeetingDto(
    string Id,
    string CompanyId,
    string? ContactId,
    string? ContactName,
    string? DealId,
    string? DealTitle,
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime EndAt,
    string? Location,
    string OrganizerId,
    string OrganizerName,
    IReadOnlyList<string> AttendeeUserIds,
    string Status,
    string SyncStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CalendarStatusDto(bool Connected, DateTime? ChannelExpiration);

public class CreateMeetingRequestValidator : AbstractValidator<CreateMeetingRequest>
{
    public CreateMeetingRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt).WithMessage("endAt must be after startAt");
    }
}

public class UpdateMeetingRequestValidator : AbstractValidator<UpdateMeetingRequest>
{
    public UpdateMeetingRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().When(x => x.Title is not null);
        RuleFor(x => x).Must(x => x.StartAt is null || x.EndAt is null || x.EndAt > x.StartAt)
            .WithMessage("endAt must be after startAt");
    }
}

public class UpdateMeetingStatusRequestValidator : AbstractValidator<UpdateMeetingStatusRequest>
{
    public UpdateMeetingStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
