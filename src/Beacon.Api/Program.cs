using System.Text.Json.Serialization;
using Asp.Versioning;
using Beacon.Api.Common;
using Beacon.Api.Features.Admin;
using Beacon.Api.Features.Analytics;
using Beacon.Api.Features.Branding;
using Beacon.Api.Features.Folders;
using Beacon.Api.Features.Links;
using Beacon.Api.Features.Me;
using Beacon.Api.Features.Redirect;
using Beacon.Api.Features.Sharing;
using Beacon.Api.Features.Workspaces;
using Beacon.Api.Infrastructure.Analytics;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Branding;
using Beacon.Api.Infrastructure.Caching;
using Beacon.Api.Infrastructure.Persistence;

const string SpaCorsPolicy = "spa";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// PostgreSQL (Aspire pooled) — connection name matches the AppHost database resource.
builder.AddNpgsqlDbContext<AppDbContext>("beacon", configureDbContextOptions: AppDbContext.ConfigureOptions);

// Redis + FusionCache (L1 memory + L2 Redis + backplane).
builder.AddAppCaching();

// Pluggable authentication (Entra first; Dev provider for the POC).
builder.Services.AddAppAuthentication(builder.Configuration, builder.Environment);

builder.Services.Configure<ShortUrlOptions>(builder.Configuration.GetSection(ShortUrlOptions.SectionName));
builder.Services.Configure<SharingOptions>(builder.Configuration.GetSection(SharingOptions.SectionName));

// Serialise enums (e.g. roles) as strings both ways.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
});

builder.Services
    .AddMe()
    .AddWorkspaces()
    .AddFolders()
    .AddLinks()
    .AddAnalytics()
    .AddBranding()
    .AddSharing()
    .AddAdmin()
    .AddClickAnalytics();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy(SpaCorsPolicy, policy =>
{
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    if (corsOrigins.Length > 0)
    {
        policy.WithOrigins(corsOrigins);
    }

    if (builder.Environment.IsDevelopment())
    {
        // Any localhost origin in dev so a standalone Vite server on any port works.
        policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback);
    }
}));

builder.Services.AddOpenApi();

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(SpaCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

// Explicit, always-on liveness endpoint (ServiceDefaults only maps /health in Development).
// "health" is a reserved code, so this never collides with a redirect.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .ExcludeFromDescription();

var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();
var api = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .RequireAuthorization();

api.MapMeEndpoints();
api.MapWorkspaceEndpoints();
api.MapFolderEndpoints();
api.MapLinkEndpoints();
api.MapAnalyticsEndpoints();
api.MapBrandingAdminEndpoints();
api.MapSharingEndpoints();
api.MapAdminEndpoints();

// Public branding (read) for the SPA + the branded not-found page.
app.MapPublicBrandingEndpoints();

// Public redirect at the root + branded 404 fallback. Mapped last so /api, /health, /openapi win.
app.MapRedirectEndpoints();

await app.RunAsync();
