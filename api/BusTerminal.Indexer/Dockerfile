# Spec 006 / T045. Containerized Azure Functions image for the registry
# indexer. Targets the v2 native Functions-on-Container-Apps hosting model
# (research §4) — the runtime base image is the Microsoft-published Functions
# image for .NET 10 isolated workers.
#
# The container has no public ingress. The change-feed trigger is the only
# execution surface; CAE health probes use the internal ready endpoint
# exposed by the Functions host.
#
# NOTE: As of authoring (June 2026) Microsoft publishes Functions base images
# under mcr.microsoft.com/azure-functions/dotnet-isolated. The `10.0` tag is
# pinned at build time; CI updates it via Renovate.

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BusTerminal.Indexer.csproj", "./"]
RUN dotnet restore "BusTerminal.Indexer.csproj"

COPY . .
RUN dotnet publish "BusTerminal.Indexer.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS runtime
WORKDIR /home/site/wwwroot

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=build /app/publish .

EXPOSE 8080
