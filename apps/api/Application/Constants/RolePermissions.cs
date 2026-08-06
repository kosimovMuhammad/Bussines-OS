using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Constants;

// Reconstructed for the 3 modules in this phase - the original role-permissions.policy.ts
// matrix values weren't available from the source repo exploration (only its structure and
// mechanism were), so this is a reasonable default, not a byte-for-byte port. The exact
// role-gates on individual mutating endpoints (documented precisely) are enforced separately
// via [Authorize(Roles = "...")] on those actions, same two-layer design as the original.
public static class RolePermissions
{
    private static readonly Dictionary<Role, Dictionary<ModuleName, HashSet<PermissionAction>>> Matrix = new()
    {
        [Role.Owner] = AllModules(PermissionAction.Read, PermissionAction.Create, PermissionAction.Update, PermissionAction.Delete),
        [Role.Admin] = AllModules(PermissionAction.Read, PermissionAction.Create, PermissionAction.Update, PermissionAction.Delete),
        [Role.Manager] = AllModules(PermissionAction.Read),
        [Role.Sales] = AllModules(PermissionAction.Read),
        [Role.Support] = AllModules(PermissionAction.Read),
        [Role.Employee] = AllModules(PermissionAction.Read),
        [Role.ReadOnly] = AllModules(PermissionAction.Read),
    };

    public static bool HasPermission(Role role, ModuleName module, PermissionAction action) =>
        Matrix.TryGetValue(role, out var modules) &&
        modules.TryGetValue(module, out var actions) &&
        actions.Contains(action);

    private static Dictionary<ModuleName, HashSet<PermissionAction>> AllModules(params PermissionAction[] actions)
    {
        var actionSet = new HashSet<PermissionAction>(actions);
        return Enum.GetValues<ModuleName>().ToDictionary(m => m, _ => actionSet);
    }
}
