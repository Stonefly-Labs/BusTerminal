# syntax=docker/dockerfile:1.7

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore at the project level — not at the slnx level. Slice 004 added a
# sibling project reference (`../tools/load-fixtures/LoadFixtures.csproj`) to
# `BusTerminal.slnx` so `dotnet test BusTerminal.slnx` picks up the CLI's
# tests too. That path lives OUTSIDE this Dockerfile's build context (which
# is `api/` per docker-compose and the CD workflow's `docker build … api`),
# so a slnx-level restore inside the container errors with MSB3202: project
# file `/tools/load-fixtures/LoadFixtures.csproj` not found. Restoring the API
# csproj directly is the right scope for the runtime image anyway — the
# container has no business pulling test or CLI dependencies.
COPY BusTerminal.Api/BusTerminal.Api.csproj BusTerminal.Api/
RUN dotnet restore BusTerminal.Api/BusTerminal.Api.csproj

COPY BusTerminal.Api/ BusTerminal.Api/
RUN dotnet publish BusTerminal.Api/BusTerminal.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# --- Runtime stage (chiseled, non-root by default) ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "BusTerminal.Api.dll"]
