using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureSnapshotManager.Api.Authentication;

/// <summary>
/// LOCAL DEVELOPMENT ONLY. Auto-authenticates every incoming request as a
/// fake "local-dev-user" — no bearer token required. This lets you exercise
/// [Authorize]-protected endpoints (AdvisoryController, DashboardController,
/// RetentionController, ExportController) against Swagger without first
/// setting up an Entra ID app registration.
///
/// Wired up in Program.cs ONLY when builder.Environment.IsDevelopment() is
/// true. In every other environment, real Microsoft Entra ID bearer-token
/// auth (AddMicrosoftIdentityWebApi) is used instead — the [Authorize]
/// attributes on your controllers are never removed or weakened.
///
/// NEVER enable this scheme outside Development. It grants access to
/// anyone who can reach the app with zero credentials.
/// </summary>
public class DevelopmentAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevelopmentBypass";

    public DevelopmentAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Fake claims standing in for what a real Entra ID token would carry.
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "local-dev-user"),
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000000"),
            new Claim("oid", "00000000-0000-0000-0000-000000000000")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
