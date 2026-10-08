# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AzureSnapshotManager.sln .
COPY src/AzureSnapshotManager.Core/AzureSnapshotManager.Core.csproj src/AzureSnapshotManager.Core/
COPY src/AzureSnapshotManager.Infrastructure/AzureSnapshotManager.Infrastructure.csproj src/AzureSnapshotManager.Infrastructure/
COPY src/AzureSnapshotManager.Api/AzureSnapshotManager.Api.csproj src/AzureSnapshotManager.Api/
RUN dotnet restore src/AzureSnapshotManager.Api/AzureSnapshotManager.Api.csproj

COPY src/ src/
RUN dotnet publish src/AzureSnapshotManager.Api/AzureSnapshotManager.Api.csproj -c Release -o /app --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Container hosts (Container Apps, Render, Fly.io) route traffic to whatever
# port you expose — 8080 is the common convention for non-root containers.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AzureSnapshotManager.Api.dll"]

