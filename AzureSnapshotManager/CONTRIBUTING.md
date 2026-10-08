# Contributing

## Local setup
```bash
dotnet restore AzureSnapshotManager.sln
dotnet build AzureSnapshotManager.sln
dotnet test AzureSnapshotManager.sln
```
See [README.md](README.md#local-development) for running against a real Azure subscription.

## Before opening a PR
- `dotnet test` passes locally
- New logic in `AdvisoryEngine`, `ResourceGraphDiscoveryEngine`, or other `Infrastructure` classes has a corresponding test in `tests/AzureSnapshotManager.Tests`, following the existing mock-based pattern in `AdvisoryEngineTests.cs`
- No secrets, connection strings, or real tenant/subscription IDs added to `appsettings.json` — use `dotnet user-secrets` locally instead
- Keep `Core` free of Azure SDK references — domain logic belongs there; anything touching Azure belongs in `Infrastructure`

## Commit style
Short, imperative subject line (`Add retention rule for tag-based exclusions`, not `Added` / `Adding`). Reference the WBS item or issue number where relevant.

## Reporting bugs / requesting features
Use the issue templates under `.github/ISSUE_TEMPLATE/`.
