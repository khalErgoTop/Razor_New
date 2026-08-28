using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using TOPqwWeb_Core.Infrastructure.Session;

namespace TOPqwWeb_Core.Infrastructure.Security
{
    public class LegacySignOutService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LegacySessionContext _legacySessionContext;
        private readonly LegacySessionPersistenceService _sessionPersistence;

        public LegacySignOutService(
            IHttpContextAccessor httpContextAccessor,
            LegacySessionContext legacySessionContext,
            LegacySessionPersistenceService sessionPersistence)
        {
            _httpContextAccessor = httpContextAccessor;
            _legacySessionContext = legacySessionContext;
            _sessionPersistence = sessionPersistence;
        }

        public async Task LogoutAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return;
            }

            await _sessionPersistence.DeleteAsync(httpContext.Session, httpContext.RequestAborted);
            await httpContext.SignOutAsync("LegacyCookie");
            httpContext.Session.Clear();
            httpContext.Response.Cookies.Delete("TOPqwWeb.Auth");
            httpContext.Response.Cookies.Delete("TOPqwWeb.Session");
        }
    }
}
