# Runtime base, which Visual Studio's fast mode debugging also runs from
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80
EXPOSE 443


# Project files are copied first so the restore layer stays cached
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["InventoryManagement.Web/InventoryManagement.Web.csproj", "InventoryManagement.Web/"]
COPY ["NotificationService.Application/NotificationService.Application.csproj", "NotificationService.Application/"]
COPY ["NotificationService.Domain/NotificationService.Domain.csproj", "NotificationService.Domain/"]
COPY ["SharedServices/SharedServices.csproj", "SharedServices/"]
RUN dotnet restore "./InventoryManagement.Web/InventoryManagement.Web.csproj"
COPY . .
WORKDIR "/src/InventoryManagement.Web"
RUN dotnet build "./InventoryManagement.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./InventoryManagement.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Production image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY InventoryManagement.Web/nginx/ /app/nginx/

ENTRYPOINT ["dotnet", "InventoryManagement.Web.dll"]