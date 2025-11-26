FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files
COPY HelloDotNet.sln .
COPY HelloDotNet/HelloDotNet.csproj HelloDotNet/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY HelloDotNet/ HelloDotNet/

# Build the application
RUN dotnet build -c Release --no-restore

# Run the application
WORKDIR /app/HelloDotNet
EXPOSE 5000
ENTRYPOINT ["dotnet", "run", "--no-build", "-c", "Release"]
