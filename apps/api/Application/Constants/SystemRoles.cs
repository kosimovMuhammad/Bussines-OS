using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Constants;

// System roles seeded into every company (existing ones via the AddRbacTables migration,
// new ones via IRoleSeedingService at registration time). Owner/Admin get every permission
// in the catalog; the rest get read-only - preserving the old hardcoded RolePermissions
// matrix's behavior as day-one defaults that admins can now edit per role/module/action.
public static class SystemRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Member = "Member";
    public const string ReadOnly = "ReadOnly";

    public static readonly IReadOnlyList<(string Name, string Description, bool FullAccess)> Defaults =
    [
        (Owner, "Full access to everything, including billing and ownership transfer", true),
        (Admin, "Full access to everything except ownership transfer", true),
        (Manager, "Read-only access across all modules by default - editable per role", false),
        (Member, "Read-only access across all modules by default - editable per role", false),
        (ReadOnly, "Read-only access across all modules", false),
    ];

    // Legacy Domain.Enums.Role -> seeded system role name. Sales/Support/Employee collapse
    // onto "Member", matching the old RolePermissions matrix's read-only catch-all for every
    // non-Owner/Admin role.
    public static string ForLegacyRole(Role role) => role switch
    {
        Role.Owner => Owner,
        Role.Admin => Admin,
        Role.Manager => Manager,
        Role.ReadOnly => ReadOnly,
        _ => Member,
    };
}
