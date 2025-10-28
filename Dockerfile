# Multi-stage Dockerfile for ChatServer

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["src/ChatServer/ChatServer.csproj", "ChatServer/"]
COPY ["src/ChatServer.Tests/ChatServer.Tests.csproj", "ChatServer.Tests/"]

# Restore dependencies
RUN dotnet restore "ChatServer/ChatServer.csproj"
RUN dotnet restore "ChatServer.Tests/ChatServer.Tests.csproj"

# Copy source code
COPY src/ .

# Build the application
WORKDIR "/src/ChatServer"
RUN dotnet build "ChatServer.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "ChatServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Create non-root user for security
RUN useradd -m -u 1000 chatuser && chown -R chatuser:chatuser /app
USER chatuser

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "ChatServer.dll"]
