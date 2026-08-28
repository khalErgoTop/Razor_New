using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Infrastructure.Theming;
using TOPqwWeb_Core.Infrastructure.Session;
using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Services.Account;
using TOPqwWeb_Core.Services.Public;
using TOPqwWeb_Core.Services.Auth;
using TOPqwWeb_Core.Services.Administration;
using TOPqwWeb_Core.Services.Layout;
using TOPqwWeb_Core.Services.Modules;

namespace TOPqwWeb_Core.Infrastructure.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPublicThemeServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ThemeSettings>(configuration.GetSection(ThemeSettings.SectionName));
            services.AddScoped<ThemeResolver>();
            services.AddScoped<ThemeAssetService>();
            services.AddScoped<IndexPageService>();
            services.AddScoped<SuchartenPageService>();
            services.AddScoped<PublicSearchAdapter>();
            services.AddScoped<AuflistungPageService>();
            services.AddScoped<PublicStammdatenAdapter>();
            services.AddScoped<StammdatenPageService>();
            services.AddScoped<SuchePageService>();
            services.AddScoped<SuchAssistentPageService>();
            services.AddScoped<AuthAdapter>();
            services.AddScoped<LoginPageService>();
            services.AddScoped<ClaimsPrincipalFactory>();
            services.AddHttpContextAccessor();
            services.AddScoped<CurrentUserAccessor>();
            services.AddScoped<SearchStateStore>();
            services.AddScoped<LegacySignOutService>();
            services.AddScoped<LegacyPermissionService>();
            services.AddScoped<QwStammdatenPageService>();
            services.AddScoped<StammdatenAnalysisAdapter>();
            services.AddScoped<StammdatenKostenAdapter>();
            services.AddScoped<StammdatenKostenPageService>();
            services.AddScoped<StammdatenAnalysisPageService>();
            services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, LegacyPermissionHandler>();
            services.AddScoped<PasswordRequestPageService>();
            services.AddScoped<PasswortAendernPageService>();
            services.AddScoped<OptionenService>();
            services.AddScoped<BenutzerManagementService>();
            services.AddScoped<BenutzerManagementRechteService>();
            services.AddScoped<LayoutNavigationService>();
            services.AddScoped<LvService>();
            services.AddScoped<QArcAdapter>();
            services.AddScoped<QmAdapter>();
            services.AddScoped<QmService>();

            return services;
        }
    }
}
