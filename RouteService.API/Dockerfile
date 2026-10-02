# Visual Studio runs Debug builds from this stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RouteService.API/RouteService.API.csproj", "RouteService.API/"]
COPY ["IdentityService.Domain/IdentityService.Domain.csproj", "IdentityService.Domain/"]
COPY ["RouteService.Application/RouteService.Application.csproj", "RouteService.Application/"]
COPY ["RouteService.Domain/RouteService.Domain.csproj", "RouteService.Domain/"]
COPY ["SharedServices/SharedServices.csproj", "SharedServices/"]
COPY ["RouteService.Infrastructure/RouteService.Infrastructure.csproj", "RouteService.Infrastructure/"]
RUN dotnet restore "./RouteService.API/RouteService.API.csproj"
COPY . .
WORKDIR "/src/RouteService.API"
RUN dotnet build "./RouteService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RouteService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Production image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "RouteService.API.dll"]