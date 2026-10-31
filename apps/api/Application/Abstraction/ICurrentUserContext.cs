using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Abstraction;

public interface ICurrentUserContext
{
    string? UserId { get; }
    string? CompanyId { get; }
    Role? Role { get; }
    string? BranchId { get; }
}
