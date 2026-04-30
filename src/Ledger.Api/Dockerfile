# syntax=docker/dockerfile:1.6
# Multi-stage Dockerfile for the Ledger.Api host.
# Build stage uses the official .NET 10 SDK image; runtime is the
# slim ASP.NET 10 image. Layer caching matches the central package
# manifest so a code change does not invalidate package restore.

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY .editorconfig ./
COPY src/Ledger.Domain/Ledger.Domain.csproj src/Ledger.Domain/
COPY src/Ledger.Contracts/Ledger.Contracts.csproj src/Ledger.Contracts/
COPY src/Ledger.Application/Ledger.Application.csproj src/Ledger.Application/
COPY src/Ledger.Infrastructure/Ledger.Infrastructure.csproj src/Ledger.Infrastructure/
COPY src/Ledger.Api/Ledger.Api.csproj src/Ledger.Api/

RUN dotnet restore src/Ledger.Api/Ledger.Api.csproj

COPY src/ src/

RUN dotnet publish src/Ledger.Api/Ledger.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Ledger.Api.dll"]
