FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files
COPY HelloDotNet.sln .
COPY HelloDotNet/HelloDotNet.csproj HelloDotNet/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY HelloDotNet/ HelloDotNet/

# Build and publish the application
RUN dotnet publish HelloDotNet/HelloDotNet.csproj -c Release -o out --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 5000
ENTRYPOINT ["dotnet", "HelloDotNet.dll"]
