using Microsoft.AspNetCore.Authentication;
using TOPqwWeb_Core.Infrastructure.Session;

namespace TOPqwWeb_Core.Infrastructure.Security
{
    public class LegacySignOutService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LegacySessionContext _legacySessionContext;

        public LegacySignOutService(IHttpContextAccessor httpContextAccessor, LegacySessionContext legacySessionContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _legacySessionContext = legacySessionContext;
        }

        public async Task LogoutAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return;
            }

            httpContext.Session.Clear();
            _legacySessionContext.LogOutWanted = true;
            await httpContext.SignOutAsync("LegacyCookie");
        }
    }
}
