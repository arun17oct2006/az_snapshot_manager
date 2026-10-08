using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace AzureSnapshotManager.Infrastructure.Security;

/// <summary>
/// WBS 1.6.1 User-Assigned Managed Identity.
/// Central place that hands out an Azure credential. In Azure Marketplace
/// SaaS deployments this should resolve to the User-Assigned Managed Identity
/// configured on the App Service / Container App via ARM (see deploy/mainTemplate.json),
/// with a client-secret fallback only for local development.
/// </summary>
public class ManagedIdentityAuthProvider
{
    private readonly TokenCredential _credential;

    public ManagedIdentityAuthProvider(IConfiguration configuration)
    {
        var userAssignedClientId = configuration["Azure:ManagedIdentityClientId"];

        _credential = string.IsNullOrWhiteSpace(userAssignedClientId)
            // DefaultAzureCredential: falls back through env vars / VS / Azure CLI
            // for local dev, and system-assigned MI when no client id is set.
            ? new DefaultAzureCredential()
            // Explicit user-assigned MI, matching WBS 1.6.1 + least-privilege
            // RBAC role assignment in WBS 1.6.2.
            : new ManagedIdentityCredential(userAssignedClientId);
    }

    public TokenCredential GetCredential() => _credential;
}
