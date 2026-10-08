# Azure Snapshot Manager

[![CI](https://github.com/<your-username>/AzureSnapshotManager/actions/workflows/ci.yml/badge.svg)](https://github.com/<your-username>/AzureSnapshotManager/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Finds orphaned Azure disk snapshots — snapshots left behind after their
source disk is deleted — that are silently costing money, and surfaces them
in a dashboard with one-click portal links, CSV export, and configurable
retention rules. Built as an Azure Marketplace SaaS offer: ARM/`createUiDefinition`
packaging, Managed Identity auth, per-tenant Table Storage isolation, and
Partner Center metered billing are all part of the design, not bolted on
after.

## Contents
- [Features](#features)
- [Architecture](#architecture)
- [How the code maps to the design](#how-the-code-maps-to-the-design)
- [Tech stack](#tech-stack)
- [Screenshots](#screenshots)
- [Local development](#local-development)
- [Testing](#testing)
- [Deploying to Azure Marketplace](#deploying-to-azure-marketplace)
- [Known limitations](#known-limitations)

## Features

- **Automated discovery** — queries Azure Resource Graph for every disk and
  snapshot in a subscription, and traces snapshot → parent-disk lineage to
  flag orphans
- **Live cost estimation** — prices orphaned snapshots against the real
  Azure Retail Prices API, cached per SKU/region
- **Grouped findings** — clusters orphans by shared parent disk or resource
  group, so a whole abandoned VM's worth of snapshots shows up as one
  finding, not twenty
- **Configurable retention rules** — exclude snapshots by resource group,
  name pattern, or a keep-until date, so the audit never flags something
  intentionally kept
- **One-click remediation** — every finding links straight to the resource
  in the Azure Portal
- **CSV export** for reporting outside the dashboard
- **Multi-tenant by design** — every Table Storage read/write is scoped by
  subscription ID as the partition key, enforced in one place
  ([`TableStorageRepository`](src/AzureSnapshotManager.Infrastructure/Storage/TableStorageRepository.cs))

## Architecture

```
+------------------------------+
|   AzureSnapshotManager.Api   |  Controllers, auth, DI wiring, static dashboard
+---------------+---------------+
                | depends on
+---------------v---------------+
| AzureSnapshotManager.Infra-   |  Resource Graph, Retail Prices API, Table
| structure                     |  Storage, Advisory engine, CSV export,
|                                |  Managed Identity, Marketplace metering
+---------------+---------------+
                | depends on
+---------------v---------------+
|  AzureSnapshotManager.Core    |  Domain models + interfaces - zero Azure
|                                |  SDK dependency, fully unit-testable
+--------------------------------+
```

`Core` has no outgoing dependencies. That's what lets
[`AdvisoryEngineTests`](tests/AzureSnapshotManager.Tests/AdvisoryEngineTests.cs)
exercise the real orphan-detection and retention-rule logic with mocked
discovery/pricing/storage — no Azure credentials or network access needed
to validate the business rules.

## How the code maps to the design

| Module | Code |
|---|---|
| Marketplace packaging | [`deploy/mainTemplate.json`](deploy/mainTemplate.json), [`deploy/createUiDefinition.json`](deploy/createUiDefinition.json), [`MarketplaceMeteringService.cs`](src/AzureSnapshotManager.Infrastructure/Billing/MarketplaceMeteringService.cs) |
| Architecture & data storage | [`TableStorageRepository.cs`](src/AzureSnapshotManager.Infrastructure/Storage/TableStorageRepository.cs) — partition-per-tenant boundary |
| Inventory & discovery | [`ResourceGraphDiscoveryEngine.cs`](src/AzureSnapshotManager.Infrastructure/Discovery/ResourceGraphDiscoveryEngine.cs), [`RetailPricesApiService.cs`](src/AzureSnapshotManager.Infrastructure/Pricing/RetailPricesApiService.cs) |
| Advisory analytics | [`AdvisoryEngine.cs`](src/AzureSnapshotManager.Infrastructure/Advisory/AdvisoryEngine.cs) |
| UI & reporting | [`wwwroot/index.html`](src/AzureSnapshotManager.Api/wwwroot/index.html), [`DashboardController.cs`](src/AzureSnapshotManager.Api/Controllers/DashboardController.cs), [`RetentionController.cs`](src/AzureSnapshotManager.Api/Controllers/RetentionController.cs), [`CsvExportService.cs`](src/AzureSnapshotManager.Infrastructure/Export/CsvExportService.cs) |
| Security & access control | [`ManagedIdentityAuthProvider.cs`](src/AzureSnapshotManager.Infrastructure/Security/ManagedIdentityAuthProvider.cs), Entra ID wiring in [`Program.cs`](src/AzureSnapshotManager.Api/Program.cs), RBAC role assignment in `mainTemplate.json` |

Domain entities (`Subscription`, `ParentDisk`, `Snapshot`, `UserPreference`,
`ScanLog`) live under [`Core/Models`](src/AzureSnapshotManager.Core/Models).

## Tech stack

.NET 8 · ASP.NET Core Web API · Azure Resource Graph SDK · Azure.Identity
(Managed Identity) · Azure Table Storage · Microsoft Entra ID (Microsoft.Identity.Web)
· xUnit + Moq · vanilla HTML/CSS/JS dashboard (no frontend framework/build step)

## Screenshots

<!--
Add screenshots here before publishing, e.g.:
![Dashboard](docs/screenshots/dashboard.png)
![Swagger](docs/screenshots/swagger.png)
-->
_Add a screenshot of the dashboard (`docs/screenshots/dashboard.png`) and a
`dotnet test` passing run here before sharing this repo publicly._

## Local development

### 1. Run the tests first (no Azure resources needed)
The advisory logic — orphan detection, grouping, retention-rule matching,
deep-link generation — is fully unit tested with mocked dependencies.

```bash
dotnet restore
dotnet test
```

### 2. Run the API against a real subscription

```bash
az login
az account set --subscription "<your-subscription-id>"
```

```bash
cd src/AzureSnapshotManager.Api
dotnet run
```

This opens the dashboard at `http://localhost:5080/`. In `Development`,
[`DevelopmentAuthHandler`](src/AzureSnapshotManager.Api/Authentication/DevelopmentAuthHandler.cs)
auto-authenticates every request and
[`InMemorySnapshotRepository`](src/AzureSnapshotManager.Api/Development/InMemorySnapshotRepository.cs)
replaces Table Storage — so the only real Azure dependency locally is your
`az login` session having Reader access on the subscription you're testing.

Paste a real subscription ID into the dashboard's top bar and click **Run
audit**. Swagger is also available at `/swagger` if you'd rather call the
API directly.

### 3. Configuring real auth / storage
Never put real secrets in `appsettings.json`. Use user-secrets:

```bash
dotnet user-secrets set "AzureAd:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<app-registration-client-id>"
dotnet user-secrets set "Azure:TableStorageEndpoint" "https://<yourstorageaccount>.table.core.windows.net/"
```

## Testing

```bash
dotnet test AzureSnapshotManager.sln
```

Covers [`AdvisoryEngine`](src/AzureSnapshotManager.Infrastructure/Advisory/AdvisoryEngine.cs):
orphan detection, multi-resource grouping, retention-rule exclusion, and
portal deep-link generation — see
[`AdvisoryEngineTests.cs`](tests/AzureSnapshotManager.Tests/AdvisoryEngineTests.cs).

## Deploying to Azure Marketplace

### 1. Partner Center setup
1. Create your offer (Azure Application / Managed App, or SaaS) in [Partner Center](https://partner.microsoft.com/dashboard).
2. Upload [`deploy/mainTemplate.json`](deploy/mainTemplate.json) + [`deploy/createUiDefinition.json`](deploy/createUiDefinition.json) as your ARM package.
3. If billing on usage, define metering dimensions matching what [`MarketplaceMeteringService`](src/AzureSnapshotManager.Infrastructure/Billing/MarketplaceMeteringService.cs) sends.
4. Register a multi-tenant Entra ID app for dashboard sign-in.

### 2. Provision + publish
```bash
az deployment group create \
  --resource-group rg-snapshotmgr-test \
  --template-file deploy/mainTemplate.json \
  --parameters appName=snapmgrtest aadTenantId=<tenant-id> aadClientId=<client-id>

dotnet publish src/AzureSnapshotManager.Api -c Release -o ./publish
cd publish && zip -r ../app.zip . && cd ..
az webapp deploy --resource-group rg-snapshotmgr-test --name snapmgrtest-app --src-path app.zip --type zip
```

### 3. Submit
Once the deployed app responds correctly behind real Entra ID auth, follow
Partner Center's **Preview → Go live** flow.

## Known limitations

- **Cross-tenant RBAC**: the ARM template grants the Managed Identity
  `Storage Table Data Contributor` on its own storage account, but reading a
  *customer's* subscription via Resource Graph needs a Reader grant at their
  scope — normally handled via the Marketplace Managed Application's
  built-in Service Provider role, not something this template self-grants.
- **Metered billing**: `MarketplaceMeteringService` needs each customer's
  Marketplace `resourceId`, captured via the SaaS fulfillment webhook — not
  included here since it depends on which offer type you choose.
- **SDK version drift**: `Azure.ResourceManager.ResourceGraph`'s API surface
  has changed across versions; if `dotnet build` errors on
  `ResourceGraphDiscoveryEngine.cs`, check the resolved package version.

## License
[MIT](LICENSE)
