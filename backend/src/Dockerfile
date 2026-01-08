# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files (skip solution to avoid test project references)
COPY nuget.config .
COPY Kayden.Commons/Kayden.Commons.csproj Kayden.Commons/
COPY KaydenTools.Core/KaydenTools.Core.csproj KaydenTools.Core/
COPY KaydenTools.Models/KaydenTools.Models.csproj KaydenTools.Models/
COPY KaydenTools.Repositories/KaydenTools.Repositories.csproj KaydenTools.Repositories/
COPY KaydenTools.Services/KaydenTools.Services.csproj KaydenTools.Services/
COPY KaydenTools.Migration/KaydenTools.Migration.csproj KaydenTools.Migration/
COPY KaydenTools.Api/KaydenTools.Api.csproj KaydenTools.Api/

# Restore dependencies (only API project, skip tests)
RUN dotnet restore KaydenTools.Api/KaydenTools.Api.csproj

# Copy everything else
COPY . .

# Build and publish (use csproj directly, not solution)
RUN dotnet publish KaydenTools.Api/KaydenTools.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "KaydenTools.Api.dll"]
