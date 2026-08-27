using Microsoft.AspNetCore.Authorization;

namespace TOPqwWeb_Core.Infrastructure.Security;

public sealed class LegacyPermissionRequirement : IAuthorizationRequirement
{
    public LegacyPermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}
