# Multi-stage Dockerfile for WorkFlowPro ASP.NET Core API (.NET 9)
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy solution and project definitions for optimized Docker layer caching
COPY WorkFlowPro.sln ./
COPY src/WorkFlowPro.Domain/WorkFlowPro.Domain.csproj src/WorkFlowPro.Domain/
COPY src/WorkFlowPro.Application/WorkFlowPro.Application.csproj src/WorkFlowPro.Application/
COPY src/WorkFlowPro.Infrastructure/WorkFlowPro.Infrastructure.csproj src/WorkFlowPro.Infrastructure/
COPY src/WorkFlowPro.Api/WorkFlowPro.Api.csproj src/WorkFlowPro.Api/

# Restore dependencies
RUN dotnet restore src/WorkFlowPro.Api/WorkFlowPro.Api.csproj

# Copy the complete source tree
COPY src/ src/

# Build and publish in Release mode
WORKDIR /src/src/WorkFlowPro.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage using unprivileged ASP.NET 9 Alpine runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Non-root user for hardened container execution
USER $APP_UID

# Copy compiled artifacts
COPY --from=build /app/publish .

# Expose standard port
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "WorkFlowPro.Api.dll"]
