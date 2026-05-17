# syntax=docker/dockerfile:1.7

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BusTerminal.slnx ./
COPY BusTerminal.Api/BusTerminal.Api.csproj BusTerminal.Api/
COPY BusTerminal.Api.Tests/BusTerminal.Api.Tests.csproj BusTerminal.Api.Tests/
RUN dotnet restore BusTerminal.slnx

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
