FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY FinFlow.sln ./
COPY src/Api/FinFlow.Api.csproj src/Api/
COPY src/Application/FinFlow.Application.csproj src/Application/
COPY src/Domain/FinFlow.Domain.csproj src/Domain/
COPY src/Infrastructure/FinFlow.Infrastructure.csproj src/Infrastructure/

# Restore dependencies (cached layer)
RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet publish src/Api/FinFlow.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image (slim)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create uploads directory for file storage
RUN mkdir -p /app/wwwroot/uploads

COPY --from=build /app/publish .

# Render uses PORT env variable
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "FinFlow.Api.dll"]
