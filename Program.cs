using TOPqwWeb_Core.Infrastructure.Configuration;
using TOPqwWeb_Core.Services.Administration;
using TOPqwWeb_Core.Services.Common;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<AssistOptionenService>();
builder.Services.AddScoped<FehlermeldungenService>();
builder.Services.AddScoped<SystemNewsService>();
builder.Services.AddScoped<DbCheckService>();
builder.Services.AddScoped<LeittextService>();
builder.Services.AddScoped<TOPqwWeb_Core.Services.Modules.KalkulationService>();
builder.Services.AddScoped<TOPqwWeb_Core.Services.Modules.AnbieterManagementService>();
builder.Services.AddScoped<TOPqwWeb_Core.Legacy.Adapters.KalkulationAdapter>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TOPqwWeb_Core.Infrastructure.Session.LegacySessionContext>();
builder.Services.AddScoped<TOPqwWeb_Core.Infrastructure.Session.LegacySessionPersistenceService>();
builder.Services.AddAuthentication("LegacyCookie")
    .AddCookie("LegacyCookie", options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.Name = "TOPqwWeb.Auth";
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "TOPqwWeb.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddPublicThemeServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.Use(async (context, next) =>
{
    var sessionPersistence = context.RequestServices
        .GetRequiredService<TOPqwWeb_Core.Infrastructure.Session.LegacySessionPersistenceService>();
    await sessionPersistence.LoadAsync(context.Session, context.RequestAborted);

    try
    {
        await next();
    }
    finally
    {
        if (!context.Session.TryGetValue(
                TOPqwWeb_Core.Infrastructure.Session.LegacySessionPersistenceService.SkipSaveKey,
                out _))
        {
            await sessionPersistence.SaveAsync(context.Session, context.RequestAborted);
        }
    }
});
app.Use(async (context, next) =>
{
    const string dbCheckCompletedKey = "DBCheck.Completed";
    if (context.Request.Path.StartsWithSegments("/DBCheck") ||
        context.Session.GetString(dbCheckCompletedKey) == bool.TrueString)
    {
        await next();
        return;
    }

    context.Response.Redirect("/DBCheck");
});
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();