using BusinessOS.Application.Constants;

namespace BusinessOS.WebApi.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireModuleAttribute(ModuleName module) : Attribute
{
    public ModuleName Module { get; } = module;
}

[AttributeUsage(AttributeTargets.Method)]
public class RequireActionAttribute(PermissionAction action) : Attribute
{
    public PermissionAction Action { get; } = action;
}
