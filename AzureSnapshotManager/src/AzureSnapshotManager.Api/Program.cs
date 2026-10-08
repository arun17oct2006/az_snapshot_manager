using AzureSnapshotManager.Api.Authentication;
using AzureSnapshotManager.Api.Development;
using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Infrastructure.Advisory;
using AzureSnapshotManager.Infrastructure.Discovery;
using AzureSnapshotManager.Infrastructure.Export;
using AzureSnapshotManager.Infrastructure.Pricing;
using AzureSnapshotManager.Infrastructure.Security;
using AzureSnapshotManager.Infrastructure.Storage;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// WBS 1.6 Security & Access Control
// "Authenticate & Access Dashboard" use case: Microsoft Entra ID (Azure AD)
// bearer-token auth in every real environment.
//
// In Development ONLY, swap in DevelopmentAuthHandler so [Authorize]
// endpoints can be exercised locally (e.g. via Swagger) without a JWT —
// every request is auto-authenticated as a fake local user. The
// [Authorize] attributes on controllers are never removed; only which
// scheme satisfies them changes.
// ---------------------------------------------------------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(DevelopmentAuthHandler.SchemeName)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevelopmentAuthHandler>(
            DevelopmentAuthHandler.SchemeName, _ => { });
}
else
{
    builder.Services
        .AddAuthentication(Microsoft.Identity.Web.Constants.Bearer)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}

builder.Services.AddAuthorization();

// WBS 1.6.1 User-Assigned Managed Identity (shared credential provider).
builder.Services.AddSingleton<ManagedIdentityAuthProvider>();

// WBS 1.2 Architecture & Data Storage.
// In Development, use an in-memory repository so you can test discovery
// against a real Azure subscription without provisioning Table Storage
// first. Every other environment uses the real TableStorageRepository.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ISnapshotRepository, InMemorySnapshotRepository>();
}
else
{
    builder.Services.AddSingleton<ISnapshotRepository, TableStorageRepository>();
}

// WBS 1.3 Inventory & Discovery Engine
builder.Services.AddSingleton<IDiscoveryEngine, ResourceGraphDiscoveryEngine>();

// WBS 1.3.2 Pricing (typed HttpClient + in-memory cache)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IPricingService, RetailPricesApiService>();

// WBS 1.4 Advisory Analytics Engine
builder.Services.AddSingleton<IAdvisoryEngine, AdvisoryEngine>();

// WBS 1.5.2 Export
builder.Services.AddSingleton<IExportService, CsvExportService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Azure Snapshot Manager API",
        Version = "v1",
        Description = "Backend for the Azure Snapshot Manager Marketplace offer (WBS 1.1–1.6)."
    });
});

// CORS for the dashboard SPA (WBS 1.5.1). Restrict AllowedOrigins in
// appsettings/App Service config before going to production.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Dashboard");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Lightweight liveness endpoint for App Service health checks / Partner
// Center Marketplace validation.
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();
