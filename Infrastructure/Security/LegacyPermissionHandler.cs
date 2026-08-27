using Microsoft.AspNetCore.Authorization;

namespace TOPqwWeb_Core.Infrastructure.Security;

public sealed class LegacyPermissionHandler : AuthorizationHandler<LegacyPermissionRequirement>
{
    private readonly CurrentUserAccessor _currentUserAccessor;
    private readonly LegacyPermissionService _permissionService;

    public LegacyPermissionHandler(CurrentUserAccessor currentUserAccessor, LegacyPermissionService permissionService)
    {
        _currentUserAccessor = currentUserAccessor;
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, LegacyPermissionRequirement requirement)
    {
        var beid = _currentUserAccessor.BEID;
        if (beid.HasValue && await _permissionService.HasPermissionAsync(beid.Value, requirement.Permission, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
