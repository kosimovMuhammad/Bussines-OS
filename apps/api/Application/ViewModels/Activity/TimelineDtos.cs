namespace BusinessOS.Application.ViewModels.Activity;

// Source distinguishes an ActivityLog-derived row (Action = "Created"/"Updated"/"Deleted",
// Description = the ChangesJson diff) from a Communication-derived one (Action = e.g.
// "Whatsapp Inbound", Description = the message content) - both get normalized into this
// one shape so a merged timeline can be sorted chronologically without the caller needing
// to know which source each entry came from.
public record TimelineEventDto(
    string Source,
    string EntityType,
    string EntityId,
    string Action,
    string? Description,
    string? UserId,
    string? UserName,
    DateTime Timestamp);
