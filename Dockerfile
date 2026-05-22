# Base dotnet image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt update && \
    apt upgrade -y && \
    apt install curl -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Build stage image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG DEFRA_NUGET_PAT
ENV DEFRA_NUGET_PAT=${DEFRA_NUGET_PAT}

# Restore tools
COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY .csharpierrc .csharpierrc
COPY .csharpierignore .csharpierignore

RUN dotnet tool restore

# Copy solution and project files for restore

COPY src/TradeGatewayPublisher/TradeGatewayPublisher.csproj src/TradeGatewayPublisher/TradeGatewayPublisher.csproj
COPY tests/TradeGatewayPublisher.Tests/TradeGatewayPublisher.Tests.csproj tests/TradeGatewayPublisher.Tests/TradeGatewayPublisher.Tests.csproj
COPY tests/TradeGatewayPublisher.IntegrationTests/*.csproj tests/TradeGatewayPublisher.IntegrationTests/
#COPY tests/TestFixtures/TestFixtures.csproj tests/TestFixtures/TestFixtures.csproj

COPY TradeGatewayPublisher.sln TradeGatewayPublisher.sln
COPY Directory.Build.props Directory.Build.props
COPY NuGet.config NuGet.config


RUN dotnet restore

# Copy source code
COPY src/TradeGatewayPublisher src/TradeGatewayPublisher
COPY tests/TradeGatewayPublisher.Tests tests/TradeGatewayPublisher.Tests
COPY tests/TradeGatewayPublisher.IntegrationTests tests/TradeGatewayPublisher.IntegrationTests
#COPY tests/TestFixtures tests/TestFixtures

# Check code formatting
RUN dotnet csharpier check .

# unit test and code coverage (exclude integration tests)
RUN dotnet test --no-restore --filter "Category!=IntegrationTests"

FROM build AS publish
RUN dotnet publish src/TradeGatewayPublisher -c Release -o /app/publish /p:UseAppHost=false


ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Final production image
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8085
ENTRYPOINT ["dotnet", "TradeGatewayPublisher.dll"]

